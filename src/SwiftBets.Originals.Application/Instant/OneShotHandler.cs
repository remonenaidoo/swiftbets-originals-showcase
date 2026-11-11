using System.Text.Json;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

/// <summary>A settled one-shot round: the multiplier paid (0 for a loss), the win, the game's own detail and the draw's seeds.</summary>
public sealed record OneShotView(Guid RoundId, string GameId, long Stake, long MultiplierBp, long Win, JsonElement Detail, long Nonce, string ClientSeed, string ServerSeedHash, long? Balance);

/// <summary>Games settled in one draw: stored with the result before money moves, then the stake, then the win.</summary>
public sealed class OneShotHandler(IRoundStore store, IWallet wallet, ISeedStore seeds, TimeProvider time, IGameRates rates)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Cashier cashier = new(store, wallet);

    public async Task<Result<OneShotView>> PlayAsync(string gameId, string sessionToken, long stake, JsonElement choices, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!OneShotGames.All.TryGetValue(gameId, out var game))
        {
            return GameError.GameNotFound;
        }

        if (Array.IndexOf(game.Stakes, stake) < 0 || !game.Valid(choices) || !IdempotencyKey.IsValid(idempotencyKey))
        {
            return GameError.InvalidStake;
        }

        var identity = await wallet.IdentifyAsync(sessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        var player = identity.Value;
        if (await store.FindByKeyAsync(player.Ref, idempotencyKey, cancellationToken) is { } repeat)
        {
            if (repeat.GameId != gameId)
            {
                return GameError.KeyInUse;
            }

            return repeat.Status == RoundStatus.Pending ? GameError.RoundInProgress
                : repeat.Status is RoundStatus.Refused or RoundStatus.Voided ? GameError.WalletUnavailable
                : View(repeat, (await store.SpinsAsync(repeat.RoundId, cancellationToken))[0], player.Balance);
        }

        var draw = await seeds.TakeNonceAsync(player.Ref, cancellationToken);
        var result = game.Resolve(Fair.Floats(draw.ServerSeed, draw.ClientSeed, draw.Nonce, game.FloatsNeeded(choices)), choices, rates.For(gameId));
        var win = Multipliers.Pay(stake, result.MultiplierBp);
        var roundId = Guid.NewGuid();
        var round = new RoundRecord(roundId, player.Ref, sessionToken, gameId, stake, idempotencyKey, RoundStatus.Pending, 0, 0, time.GetUtcNow());
        var outcome = new StoredOutcome(result.MultiplierBp, JsonSerializer.SerializeToElement(result.Detail, Json), choices, draw.Nonce, draw.ClientSeed, draw.ServerSeedHash);
        var spin = new SpinRecord(roundId, 0, [], JsonSerializer.Serialize(outcome, Json), win, TransactionIds.Bet(roundId), win > 0 ? TransactionIds.Win(roundId, 0) : null, false);
        await store.InsertAsync(round, spin, cancellationToken);

        var staked = await cashier.TakeStakeAsync(round, cancellationToken);
        if (staked.Failed)
        {
            return staked.Error;
        }

        var status = win > 0 ? RoundStatus.WinPending : RoundStatus.Settled;
        await store.UpdateAsync(roundId, status, 0, win, cancellationToken);
        round = round with { Status = status, PaidUnits = win };
        var balance = await cashier.PayWinAsync(round, spin, staked.Value, cancellationToken);
        return View(round, spin, balance);
    }

    private static OneShotView View(RoundRecord round, SpinRecord spin, long? balance)
    {
        var o = JsonSerializer.Deserialize<StoredOutcome>(spin.OutcomeJson, Json)!;
        return new OneShotView(round.RoundId, round.GameId, round.Stake, o.MultiplierBp, spin.Win, o.Detail, o.Nonce, o.ClientSeed, o.ServerSeedHash, balance);
    }

    private sealed record StoredOutcome(long MultiplierBp, JsonElement Detail, JsonElement Choices, long Nonce, string ClientSeed, string ServerSeedHash);
}
