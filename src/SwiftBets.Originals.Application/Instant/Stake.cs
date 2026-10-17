using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Application.Instant;

/// <summary>
/// Takes an instant round's stake once the round is stored: a refusal marks it refused and moves nothing; no clear
/// answer voids it and rolls the stake back (RoundRecovery retries until confirmed), exactly as a slot spin does.
/// </summary>
internal static class Stake
{
    public static async Task<(long? Balance, SpinError? Error)> TakeAsync(IRoundStore store, IWallet wallet, RoundRecord round, CancellationToken cancellationToken)
    {
        var id = round.RoundId;
        var bet = await wallet.BetAsync(round.SessionToken, round.GameId, Play.BetTx(id), id.ToString(), round.Stake, cancellationToken);
        if (bet.Status is WalletStatus.InsufficientFunds or WalletStatus.LimitExceeded or WalletStatus.AccountRestricted or WalletStatus.SessionInvalid)
        {
            await store.UpdateAsync(id, RoundStatus.Refused, 0, 0, cancellationToken);
            return (null, Play.Refusal(bet.Status));
        }

        if (bet.Status != WalletStatus.Ok)
        {
            await store.SetRollbackPendingAsync(id, true, cancellationToken);
            await store.UpdateAsync(id, RoundStatus.Voided, 0, 0, cancellationToken);
            var rollback = await wallet.RollbackAsync(round.SessionToken, round.GameId, Play.RollbackTx(id), id.ToString(), Play.BetTx(id), cancellationToken);
            if (rollback.Status == WalletStatus.Ok)
            {
                await store.SetRollbackPendingAsync(id, false, cancellationToken);
            }

            return (null, SpinError.WalletUnavailable);
        }

        return (bet.Balance, null);
    }

    /// <summary>Pays a stored win spin; unpaid, it stays owed and RoundRecovery pays it under the same transaction id.</summary>
    public static async Task<long?> PayAsync(IRoundStore store, IWallet wallet, RoundRecord round, SpinRecord spin, long? balance, CancellationToken cancellationToken)
    {
        if (spin.Win <= 0)
        {
            return balance;
        }

        var paid = await wallet.WinAsync(round.SessionToken, round.GameId, spin.WinTx!, round.RoundId.ToString(), spin.Win, cancellationToken);
        if (paid.Status != WalletStatus.Ok)
        {
            return balance;
        }

        await store.MarkWinPaidAsync(round.RoundId, spin.Seq, cancellationToken);
        await store.SettleIfPaidAsync(round.RoundId, cancellationToken);
        return paid.Balance;
    }
}
