using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Application;

/// <summary>Moves a stored round's money: the stake first, then each win as it is owed.</summary>
internal sealed class Cashier(IRoundStore store, IWallet wallet)
{
    public async Task<Result<long?>> TakeStakeAsync(RoundRecord round, CancellationToken cancellationToken)
    {
        var id = round.RoundId;
        var bet = await wallet.BetAsync(round.SessionToken, round.GameId, TransactionIds.Bet(id), id.ToString(), round.Stake, cancellationToken);
        if (RefusalOf(bet.Status) is { } refusal)
        {
            await store.UpdateAsync(id, RoundStatus.Refused, 0, 0, cancellationToken);
            return refusal;
        }

        if (bet.Status != WalletStatus.Ok)
        {
            // No clear answer: the stake may have been taken, so roll it back; RoundRecovery retries until confirmed.
            await store.SetRollbackPendingAsync(id, true, cancellationToken);
            await store.UpdateAsync(id, RoundStatus.Voided, 0, 0, cancellationToken);
            var rollback = await wallet.RollbackAsync(round.SessionToken, round.GameId, TransactionIds.Rollback(id), id.ToString(), TransactionIds.Bet(id), cancellationToken);
            if (rollback.Status == WalletStatus.Ok)
            {
                await store.SetRollbackPendingAsync(id, false, cancellationToken);
            }

            return GameError.WalletUnavailable;
        }

        return bet.Balance;
    }

    /// <summary>Pays a stored win and settles the round; an unanswered win stays owed for RoundRecovery.</summary>
    public async Task<long?> PayWinAsync(RoundRecord round, SpinRecord spin, long? balance, CancellationToken cancellationToken)
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

    private static GameError? RefusalOf(string status) => status switch
    {
        WalletStatus.InsufficientFunds => GameError.InsufficientFunds,
        WalletStatus.LimitExceeded => GameError.LimitExceeded,
        WalletStatus.AccountRestricted => GameError.AccountRestricted,
        WalletStatus.SessionInvalid => GameError.SessionInvalid,
        _ => null,
    };
}
