using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Application;

/// <summary>Puts money right after failures: settles or rolls back rounds a crash left pending, retries unconfirmed rollbacks, pays owed wins.</summary>
public sealed class RoundRecovery(IRoundStore store, IWallet wallet, TimeProvider time)
{
    public static readonly TimeSpan PendingGrace = TimeSpan.FromMinutes(2);

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var round in await store.NeedingRollbackAsync(time.GetUtcNow() - PendingGrace, cancellationToken))
        {
            await RollBackAsync(round, cancellationToken);
        }

        foreach (var (round, spin) in await store.UnpaidWinsAsync(100, cancellationToken))
        {
            var reply = await wallet.WinAsync(round.SessionToken, round.GameId, spin.WinTx!, round.RoundId.ToString(), spin.Win, cancellationToken);
            if (reply.Status != WalletStatus.Ok)
            {
                continue;
            }

            await store.MarkWinPaidAsync(round.RoundId, spin.Seq, cancellationToken);
            await store.SettleIfPaidAsync(round.RoundId, cancellationToken);
        }
    }

    private async Task RollBackAsync(RoundRecord round, CancellationToken cancellationToken)
    {
        if (round.Status == RoundStatus.Pending)
        {
            // A paid first win proves the bet was taken: the round only missed its final update.
            var spins = await store.SpinsAsync(round.RoundId, cancellationToken);
            if (spins.Count > 0 && spins[0].WinPaid)
            {
                await store.UpdateAsync(round.RoundId, RoundStatus.Settled, 0, round.PaidUnits, cancellationToken);
                return;
            }

            if (!await store.VoidIfAsync(round.RoundId, RoundStatus.Pending, cancellationToken))
            {
                return;
            }
        }

        var reply = await wallet.RollbackAsync(round.SessionToken, round.GameId, TransactionIds.Rollback(round.RoundId), round.RoundId.ToString(), TransactionIds.Bet(round.RoundId), cancellationToken);
        if (reply.Status == WalletStatus.Ok)
        {
            await store.SetRollbackPendingAsync(round.RoundId, false, cancellationToken);
        }
    }
}
