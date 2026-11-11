namespace SwiftBets.Originals.Application;

/// <summary>Wallet transaction ids derived from the round, so a retried call is applied once by the casino.</summary>
internal static class TransactionIds
{
    public static string Bet(Guid round) => $"{round}:bet";

    public static string Win(Guid round, int seq) => $"{round}:win:{seq}";

    public static string Rollback(Guid round) => $"{round}:rollback";
}
