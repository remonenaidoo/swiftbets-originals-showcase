namespace SwiftBets.Originals.Application.Ports;

public static class RoundStatus
{
    public const string Pending = "pending";
    public const string Refused = "refused";
    public const string Voided = "voided";
    public const string WinPending = "win_pending";
    public const string Feature = "feature";
    public const string Settled = "settled";
}

public sealed record RoundRecord(
    Guid RoundId,
    string PlayerRef,
    string SessionToken,
    string GameId,
    long Stake,
    string IdempotencyKey,
    string Status,
    int FreeSpinsLeft,
    long PaidUnits,
    DateTimeOffset CreatedAt);

/// <summary>One spin of a round. <paramref name="OutcomeJson"/> holds lines and scatters so the spin can be replayed.</summary>
public sealed record SpinRecord(Guid RoundId, int Seq, int[] Stops, string OutcomeJson, long Win, string? BetTx, string? WinTx, bool WinPaid);

public interface IRoundStore
{
    Task<RoundRecord?> FindByKeyAsync(string playerRef, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>The player's round with free spins still to play, if any.</summary>
    Task<RoundRecord?> FindOpenFeatureAsync(string playerRef, CancellationToken cancellationToken);

    Task<RoundRecord?> GetAsync(Guid roundId, CancellationToken cancellationToken);

    /// <summary>Inserts the round and its first spin together, before any money moves.</summary>
    Task InsertAsync(RoundRecord round, SpinRecord spin, CancellationToken cancellationToken);

    Task InsertSpinAsync(SpinRecord spin, CancellationToken cancellationToken);

    Task UpdateAsync(Guid roundId, string status, int freeSpinsLeft, long paidUnits, CancellationToken cancellationToken);

    Task MarkWinPaidAsync(Guid roundId, int seq, CancellationToken cancellationToken);

    Task<IReadOnlyList<SpinRecord>> SpinsAsync(Guid roundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoundRecord>> RecentAsync(string playerRef, int count, CancellationToken cancellationToken);

    /// <summary>Spins whose win is owed and not yet confirmed by the casino, oldest first.</summary>
    Task<IReadOnlyList<(RoundRecord Round, SpinRecord Spin)>> UnpaidWinsAsync(int limit, CancellationToken cancellationToken);
}
