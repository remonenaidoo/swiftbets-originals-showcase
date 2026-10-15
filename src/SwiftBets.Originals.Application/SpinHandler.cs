using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

public sealed record SpinRequest(string SessionToken, long Stake, string IdempotencyKey);

/// <summary>
/// A paid base spin. The result is drawn and stored (pending) before any money moves; then the casino takes the
/// stake and pays the win under transaction ids derived from the round, so a retry is applied once. A refused bet
/// moves nothing; a bet with no answer is rolled back; a win with no answer stays owed for the retry job.
/// </summary>
public sealed class SpinHandler(IRoundStore store, IWallet wallet, RoundEngine engine, TimeProvider time)
{
    public async Task<Result<SpinView>> SpinAsync(SpinRequest request, CancellationToken cancellationToken)
    {
        var game = engine.Game;
        if (Array.IndexOf(Play.Stakes, request.Stake) < 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100)
        {
            return SpinError.InvalidStake;
        }

        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, request.SessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await store.FindByKeyAsync(playerRef!, request.IdempotencyKey, cancellationToken) is { } repeat)
        {
            var first = (await store.SpinsAsync(repeat.RoundId, cancellationToken))[0];
            return Play.View(repeat, first, game, balance);
        }

        if (await store.FindOpenFeatureAsync(playerRef!, cancellationToken) is not null)
        {
            return SpinError.FeatureInProgress;
        }

        var outcome = engine.PlaySpin(free: false);
        var (paidUnits, capped) = Cap.Apply(0, outcome.TotalUnits, game.CapUnits);
        var win = Play.Money(paidUnits, request.Stake, game.LineCount);
        var roundId = Guid.NewGuid();
        var round = new RoundRecord(roundId, playerRef!, request.SessionToken, game.GameId, request.Stake, request.IdempotencyKey, RoundStatus.Pending, 0, 0, time.GetUtcNow());
        var spin = new SpinRecord(roundId, 0, outcome.Stops, Play.OutcomeJson(outcome), win, Play.BetTx(roundId), win > 0 ? Play.WinTx(roundId, 0) : null, false);
        await store.InsertAsync(round, spin, cancellationToken);

        var bet = await wallet.BetAsync(request.SessionToken, Play.BetTx(roundId), roundId.ToString(), request.Stake, cancellationToken);
        if (bet.Status == WalletStatus.Unreachable)
        {
            await wallet.RollbackAsync(request.SessionToken, Play.RollbackTx(roundId), roundId.ToString(), Play.BetTx(roundId), cancellationToken);
            await store.UpdateAsync(roundId, RoundStatus.Voided, 0, 0, cancellationToken);
            return SpinError.WalletUnavailable;
        }

        if (bet.Status != WalletStatus.Ok)
        {
            await store.UpdateAsync(roundId, RoundStatus.Refused, 0, 0, cancellationToken);
            return Play.Refusal(bet.Status);
        }

        balance = bet.Balance;
        var winPaid = true;
        if (win > 0)
        {
            var paid = await wallet.WinAsync(request.SessionToken, Play.WinTx(roundId, 0), roundId.ToString(), win, cancellationToken);
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
        return Play.View(round, spin with { WinPaid = winPaid }, game, balance);
    }
}
