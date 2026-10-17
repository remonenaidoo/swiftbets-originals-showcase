using System.Text.Json;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

/// <summary>A settled one-shot round: the multiplier paid (0 for a loss), the win, the game's own detail, and the draw's seeds.</summary>
public sealed record OneShotView(Guid RoundId, string GameId, long Stake, long MultiplierBp, long Win, JsonElement Detail, long Nonce, string ClientSeed, string ServerSeedHash, long? Balance);

/// <summary>
/// Dice, Limbo, Peg Drop and Keno: one draw from the player's seeds, stored with its result before money moves,
/// then the stake, then the win, exactly as a slot spin.
/// </summary>
public sealed class OneShotHandler(IRoundStore store, IWallet wallet, ISeedStore seeds, TimeProvider time)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<OneShotView>> PlayAsync(string gameId, string sessionToken, long stake, JsonElement choices, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!OneShotGames.All.TryGetValue(gameId, out var game))
        {
            return SpinError.GameNotFound;
        }

        if (Array.IndexOf(game.Stakes, stake) < 0 || !game.Valid(choices) || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
        {
            return SpinError.InvalidStake;
        }

        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await store.FindByKeyAsync(playerRef!, idempotencyKey, cancellationToken) is { } repeat)
        {
            if (repeat.GameId != gameId)
            {
                return SpinError.KeyInUse;
            }

            return repeat.Status == RoundStatus.Pending ? SpinError.RoundInProgress
                : repeat.Status is RoundStatus.Refused or RoundStatus.Voided ? SpinError.WalletUnavailable
                : View(repeat, (await store.SpinsAsync(repeat.RoundId, cancellationToken))[0], balance);
        }

        var draw = await seeds.TakeNonceAsync(playerRef!, cancellationToken);
        var result = game.Resolve(Fair.Floats(draw.ServerSeed, draw.ClientSeed, draw.Nonce, game.FloatsNeeded(choices)), choices);
        var win = Mult.Pay(stake, result.MultiplierBp);
        var roundId = Guid.NewGuid();
        var round = new RoundRecord(roundId, playerRef!, sessionToken, gameId, stake, idempotencyKey, RoundStatus.Pending, 0, 0, time.GetUtcNow());
        var outcome = new Stored(result.MultiplierBp, JsonSerializer.SerializeToElement(result.Detail, Json), choices, draw.Nonce, draw.ClientSeed, draw.ServerSeedHash);
        var spin = new SpinRecord(roundId, 0, [], JsonSerializer.Serialize(outcome, Json), win, Play.BetTx(roundId), win > 0 ? Play.WinTx(roundId, 0) : null, false);
        await store.InsertAsync(round, spin, cancellationToken);

        (balance, error) = await Stake.TakeAsync(store, wallet, round, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var status = win > 0 ? RoundStatus.WinPending : RoundStatus.Settled;
        await store.UpdateAsync(roundId, status, 0, win, cancellationToken);
        round = round with { Status = status, PaidUnits = win };
        balance = await Stake.PayAsync(store, wallet, round, spin, balance, cancellationToken);
        return View(round, spin, balance);
    }

    private static OneShotView View(RoundRecord round, SpinRecord spin, long? balance)
    {
        var o = JsonSerializer.Deserialize<Stored>(spin.OutcomeJson, Json)!;
        return new OneShotView(round.RoundId, round.GameId, round.Stake, o.MultiplierBp, spin.Win, o.Detail, o.Nonce, o.ClientSeed, o.ServerSeedHash, balance);
    }

    /// <summary>The round's draw and result, stored before the stake is taken.</summary>
    private sealed record Stored(long MultiplierBp, JsonElement Detail, JsonElement Choices, long Nonce, string ClientSeed, string ServerSeedHash);
}
