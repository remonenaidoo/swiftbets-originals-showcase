using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

/// <summary>
/// One free spin of an open feature: no stake; its win (×2, under the round's cap) is paid as it lands. 3+ Scatters
/// add spins; the feature ends when the spins run out or the cap is reached (remaining spins are forfeited).
/// The spin is drawn and the round updated inside one locked transaction, so concurrent requests each consume a
/// spin and the cap is always checked against the true total; the win is paid after the commit.
/// </summary>
public sealed class FeatureHandler(IRoundStore store, IWallet wallet, RoundEngine engine)
{
    public async Task<Result<SpinView>> NextAsync(string sessionToken, Guid roundId, CancellationToken cancellationToken)
    {
        var game = engine.Game;
        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        SpinOutcome? outcome = null;
        var capped = false;
        var claim = await store.ClaimFreeSpinAsync(roundId, playerRef!, (round, seq) =>
        {
            outcome = engine.PlaySpin(free: true);
            var (paidUnits, reached) = Cap.Apply(round.PaidUnits, outcome.TotalUnits, game.CapUnits);
            capped = reached;
            var win = Play.Money(paidUnits, round.Stake, game.LineCount);
            var left = reached ? 0 : round.FreeSpinsLeft - 1 + (outcome.TriggersFeature ? game.FreeSpinsAwarded : 0);
            var spin = new SpinRecord(roundId, seq, outcome.Stops, Play.OutcomeJson(outcome), win, null, win > 0 ? Play.WinTx(roundId, seq) : null, false);
            return (spin, left, round.PaidUnits + paidUnits);
        }, cancellationToken);

        if (claim is not { } claimed)
        {
            var round = await store.GetAsync(roundId, cancellationToken);
            return round is null || round.PlayerRef != playerRef ? SpinError.RoundNotFound : SpinError.NoFeature;
        }

        var (updated, played) = claimed;
        var winPaid = true;
        if (played.Win > 0)
        {
            var paid = await wallet.WinAsync(updated.SessionToken, played.WinTx!, roundId.ToString(), played.Win, cancellationToken);
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

        return Play.View(updated, played with { WinPaid = winPaid }, game, balance) with
        {
            FeatureTriggered = outcome!.TriggersFeature && !capped,
            CapReached = capped,
        };
    }
}
