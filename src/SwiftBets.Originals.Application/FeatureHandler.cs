using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

/// <summary>
/// One free spin of an open feature: no stake; its win (×2, under the round's cap) is paid as it lands. 3+ Scatters
/// add spins; the feature ends when the spins run out or the cap is reached (remaining spins are forfeited).
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

        var round = await store.GetAsync(roundId, cancellationToken);
        if (round is null || round.PlayerRef != playerRef)
        {
            return SpinError.RoundNotFound;
        }

        if (round.Status != RoundStatus.Feature || round.FreeSpinsLeft <= 0)
        {
            return SpinError.NoFeature;
        }

        var seq = (await store.SpinsAsync(roundId, cancellationToken)).Count;
        var outcome = engine.PlaySpin(free: true);
        var (paidUnits, capped) = Cap.Apply(round.PaidUnits, outcome.TotalUnits, game.CapUnits);
        var win = Play.Money(paidUnits, round.Stake, game.LineCount);
        var spin = new SpinRecord(roundId, seq, outcome.Stops, Play.OutcomeJson(outcome), win, null, win > 0 ? Play.WinTx(roundId, seq) : null, false);
        try
        {
            await store.InsertSpinAsync(spin, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Another request played this spin first (a double tap): the caller re-reads state.
            return SpinError.FeatureInProgress;
        }

        var winPaid = true;
        if (win > 0)
        {
            var paid = await wallet.WinAsync(round.SessionToken, Play.WinTx(roundId, seq), roundId.ToString(), win, cancellationToken);
            winPaid = paid.Status == WalletStatus.Ok;
            if (winPaid)
            {
                balance = paid.Balance;
                await store.MarkWinPaidAsync(roundId, seq, cancellationToken);
            }
        }

        var left = capped ? 0 : round.FreeSpinsLeft - 1 + (outcome.TriggersFeature ? game.FreeSpinsAwarded : 0);
        var owed = (await store.SpinsAsync(roundId, cancellationToken)).Any(s => s.Win > 0 && !s.WinPaid);
        var status = left > 0 ? RoundStatus.Feature : owed ? RoundStatus.WinPending : RoundStatus.Settled;
        var total = round.PaidUnits + paidUnits;
        await store.UpdateAsync(roundId, status, left, total, cancellationToken);
        return Play.View(round with { Status = status, FreeSpinsLeft = left, PaidUnits = total }, spin with { WinPaid = winPaid }, game, balance) with
        {
            FeatureTriggered = outcome.TriggersFeature,
            CapReached = capped,
        };
    }
}
