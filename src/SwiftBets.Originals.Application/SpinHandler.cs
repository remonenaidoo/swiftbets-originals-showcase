using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

public sealed record SpinRequest(string GameId, string SessionToken, long Stake, string IdempotencyKey);

/// <summary>A paid base spin: drawn and stored before any money moves, then the stake, then the win.</summary>
public sealed class SpinHandler(IRoundStore store, IWallet wallet, SlotEngines games, TimeProvider time, IGameRates rates)
{
    private readonly Cashier cashier = new(store, wallet);

    public async Task<Result<SpinView>> SpinAsync(SpinRequest request, CancellationToken cancellationToken)
    {
        if (games.Find(request.GameId) is not { } slot)
        {
            return GameError.GameNotFound;
        }

        var game = slot.Game.Definition;
        if (Array.IndexOf(slot.Game.Profile.Stakes, request.Stake) < 0 || !IdempotencyKey.IsValid(request.IdempotencyKey))
        {
            return GameError.InvalidStake;
        }

        var identity = await wallet.IdentifyAsync(request.SessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        var player = identity.Value;
        var rate = rates.For(game.GameId);
        if (await store.FindByKeyAsync(player.Ref, request.IdempotencyKey, cancellationToken) is { } repeat)
        {
            // Keys are unique per player across games: the same key in another game is a client bug, not a repeat.
            if (repeat.GameId != game.GameId)
            {
                return GameError.KeyInUse;
            }

            var first = (await store.SpinsAsync(repeat.RoundId, cancellationToken))[0];
            return SlotSpins.View(repeat, first, game, player.Balance, rate);
        }

        if (await store.FindOpenFeatureAsync(player.Ref, game.GameId, cancellationToken) is not null)
        {
            return GameError.FeatureInProgress;
        }

        var outcome = slot.Engine.PlaySpin(free: false);
        var (paidUnits, capped) = Cap.Apply(0, outcome.TotalUnits, game.CapUnits);
        var win = SlotSpins.Money(paidUnits, request.Stake, game.LineCount, rate);
        var roundId = Guid.NewGuid();
        var round = new RoundRecord(roundId, player.Ref, request.SessionToken, game.GameId, request.Stake, request.IdempotencyKey, RoundStatus.Pending, 0, 0, time.GetUtcNow());
        var spin = new SpinRecord(roundId, 0, outcome.Stops, SlotSpins.OutcomeJson(game, outcome), win, TransactionIds.Bet(roundId), win > 0 ? TransactionIds.Win(roundId, 0) : null, false);
        await store.InsertAsync(round, spin, cancellationToken);

        var staked = await cashier.TakeStakeAsync(round, cancellationToken);
        if (staked.Failed)
        {
            return staked.Error;
        }

        var balance = staked.Value;
        var winPaid = true;
        if (win > 0)
        {
            var paid = await wallet.WinAsync(request.SessionToken, game.GameId, TransactionIds.Win(roundId, 0), roundId.ToString(), win, cancellationToken);
            winPaid = paid.Status == WalletStatus.Ok;
            if (winPaid)
            {
                balance = paid.Balance;
                await store.MarkWinPaidAsync(roundId, 0, cancellationToken);
            }
        }

        var freeSpins = outcome.TriggersFeature && !capped ? game.FreeSpinsAwarded : 0;
        var status = freeSpins > 0 ? RoundStatus.Feature : winPaid ? RoundStatus.Settled : RoundStatus.WinPending;
        await store.UpdateAsync(roundId, status, freeSpins, paidUnits, cancellationToken);
        round = round with { Status = status, FreeSpinsLeft = freeSpins, PaidUnits = paidUnits };
        return SlotSpins.View(round, spin with { WinPaid = winPaid }, game, balance, rate);
    }
}
