namespace SwiftBets.Originals.Application.Ports;

public sealed record WalletReply(string Status, long? Balance, string? PlayerRef);

public static class WalletStatus
{
    public const string Ok = "ok";
    public const string Unreachable = "unreachable";
    public const string SessionInvalid = "session_invalid";
    public const string InsufficientFunds = "insufficient_funds";
    public const string LimitExceeded = "limit_exceeded";
    public const string AccountRestricted = "account_restricted";
}

/// <summary>The casino's seamless-wallet API. Transaction ids are ours, so a retry is applied once.</summary>
public interface IWallet
{
    Task<WalletReply> BalanceAsync(string sessionToken, CancellationToken cancellationToken);

    Task<WalletReply> BetAsync(string sessionToken, string gameId, string transactionId, string roundId, long amount, CancellationToken cancellationToken);

    Task<WalletReply> WinAsync(string sessionToken, string gameId, string transactionId, string roundId, long amount, CancellationToken cancellationToken);

    Task<WalletReply> RollbackAsync(string sessionToken, string gameId, string transactionId, string roundId, string betTransactionId, CancellationToken cancellationToken);
}
