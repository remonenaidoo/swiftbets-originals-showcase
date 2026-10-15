using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Application;

/// <summary>
/// Puts money right after failures, run every 30 seconds:
/// <list type="bullet">
/// <item>a round left <c>pending</c> by a crash is settled if its win was already paid (so the bet was taken), and otherwise voided and rolled back;</item>
/// <item>a voided round whose rollback the casino has not confirmed is rolled back again (the casino applies it once);</item>
/// <item>owed wins are paid under their original transaction id, and a round waiting only on them is settled.</item>
/// </list>
/// </summary>
public sealed class RoundRecovery(IRoundStore store, IWallet wallet, TimeProvider time)
{
    public static readonly TimeSpan PendingGrace = TimeSpan.FromMinutes(2);

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var round in await store.NeedingRollbackAsync(time.GetUtcNow() - PendingGrace, cancellationToken))
        {
            var spins = await store.SpinsAsync(round.RoundId, cancellationToken);
            if (round.Status == RoundStatus.Pending && spins.Count > 0 && spins[0].WinPaid)
            {
                await store.UpdateAsync(round.RoundId, RoundStatus.Settled, 0, round.PaidUnits, cancellationToken);
                continue;
            }

            await store.SetRollbackPendingAsync(round.RoundId, true, cancellationToken);
            await store.UpdateAsync(round.RoundId, RoundStatus.Voided, 0, 0, cancellationToken);
            var reply = await wallet.RollbackAsync(round.SessionToken, Play.RollbackTx(round.RoundId), round.RoundId.ToString(), Play.BetTx(round.RoundId), cancellationToken);
            if (reply.Status == WalletStatus.Ok)
            {
                await store.SetRollbackPendingAsync(round.RoundId, false, cancellationToken);
            }
        }

        foreach (var (round, spin) in await store.UnpaidWinsAsync(100, cancellationToken))
        {
            var reply = await wallet.WinAsync(round.SessionToken, spin.WinTx!, round.RoundId.ToString(), spin.Win, cancellationToken);
            if (reply.Status != WalletStatus.Ok)
            {
                continue;
            }

            await store.MarkWinPaidAsync(round.RoundId, spin.Seq, cancellationToken);
            await store.SettleIfPaidAsync(round.RoundId, cancellationToken);
        }
    }
}
