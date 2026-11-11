using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

/// <summary>One free spin of an open feature, drawn under the round's lock so the cap is checked against the true total; paid after the commit.</summary>
public sealed class FeatureHandler(IRoundStore store, IWallet wallet, SlotEngines games, IGameRates rates)
{
    public async Task<Result<SpinView>> NextAsync(string gameId, string sessionToken, Guid roundId, CancellationToken cancellationToken)
    {
        if (games.Find(gameId) is not { } slot)
        {
            return GameError.GameNotFound;
        }

        var game = slot.Game.Definition;
        var identity = await wallet.IdentifyAsync(sessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        var player = identity.Value;
        var rate = rates.For(game.GameId);
        SpinOutcome? outcome = null;
        var capped = false;
        var claimed = await store.ClaimFreeSpinAsync(roundId, player.Ref, game.GameId, (round, last) =>
        {
            var seq = last.Seq + 1;
            outcome = slot.Engine.PlaySpin(free: true, last.Seq == 0 ? [] : SlotSpins.LocksOf(game, last.OutcomeJson));
            var (paidUnits, reached) = Cap.Apply(round.PaidUnits, outcome.TotalUnits, game.CapUnits);
            capped = reached;
            var win = SlotSpins.Money(paidUnits, round.Stake, game.LineCount, rate);

            // Reaching the cap forfeits the remaining spins.
            var left = reached ? 0 : slot.Engine.SpinsLeftAfter(round.FreeSpinsLeft, outcome);
            var spin = new SpinRecord(roundId, seq, outcome.Stops, SlotSpins.OutcomeJson(game, outcome), win, null, win > 0 ? TransactionIds.Win(roundId, seq) : null, false);
            return new FreeSpinStep(spin, left, round.PaidUnits + paidUnits);
        }, cancellationToken);

        if (claimed is null)
        {
            var round = await store.GetAsync(roundId, cancellationToken);
            return round is null || round.PlayerRef != player.Ref || round.GameId != game.GameId ? GameError.RoundNotFound : GameError.NoFeature;
        }

        var (updated, played) = claimed;
        var balance = player.Balance;
        var winPaid = true;
        if (played.Win > 0)
        {
            var paid = await wallet.WinAsync(updated.SessionToken, game.GameId, played.WinTx!, roundId.ToString(), played.Win, cancellationToken);
            winPaid = paid.Status == WalletStatus.Ok;
            if (winPaid)
            {
                balance = paid.Balance;
                await store.MarkWinPaidAsync(roundId, played.Seq, cancellationToken);
            }
        }

        if (updated.FreeSpinsLeft == 0)
        {
            await store.SettleIfPaidAsync(roundId, cancellationToken);
        }

        return SlotSpins.View(updated, played with { WinPaid = winPaid }, game, balance, rate) with
        {
            FeatureTriggered = outcome!.TriggersFeature && game.Feature.Retrigger && !capped,
            CapReached = capped,
        };
    }
}
