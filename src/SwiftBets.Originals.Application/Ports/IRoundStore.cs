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

    /// <summary>The player's round of this game with free spins still to play, if any.</summary>
    Task<RoundRecord?> FindOpenFeatureAsync(string playerRef, string gameId, CancellationToken cancellationToken);

    Task<RoundRecord?> GetAsync(Guid roundId, CancellationToken cancellationToken);

    /// <summary>Inserts the round and its first spin together, before any money moves.</summary>
    Task InsertAsync(RoundRecord round, SpinRecord spin, CancellationToken cancellationToken);

    Task InsertSpinAsync(SpinRecord spin, CancellationToken cancellationToken);

    Task UpdateAsync(Guid roundId, string status, int freeSpinsLeft, long paidUnits, CancellationToken cancellationToken);

    Task MarkWinPaidAsync(Guid roundId, int seq, CancellationToken cancellationToken);

    Task<IReadOnlyList<SpinRecord>> SpinsAsync(Guid roundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoundRecord>> RecentAsync(string playerRef, string gameId, int count, CancellationToken cancellationToken);

    /// <summary>
    /// Plays one free spin atomically: locks the round, checks it still has spins, lets <paramref name="play"/> draw
    /// the spin after the round's last spin (passed in, read under the lock, so a feature's locked cells are never
    /// stale), stores it and the round's new spins-left and paid units, and commits. Null when the round is not this
    /// player's, not this game's, or has no free spin left. Two concurrent calls therefore each consume a spin.
    /// </summary>
    Task<(RoundRecord Round, SpinRecord Spin)?> ClaimFreeSpinAsync(Guid roundId, string playerRef, string gameId, Func<RoundRecord, SpinRecord, (SpinRecord Spin, int Left, long PaidUnits)> play, CancellationToken cancellationToken);

    /// <summary>Moves a <c>win_pending</c> round to <c>settled</c> when nothing is owed any more (one atomic statement).</summary>
    Task SettleIfPaidAsync(Guid roundId, CancellationToken cancellationToken);

    /// <summary>Records whether a voided round's rollback still needs the casino's confirmation.</summary>
    Task SetRollbackPendingAsync(Guid roundId, bool pending, CancellationToken cancellationToken);

    /// <summary>Rounds whose stake may have been taken and must be rolled back: stuck <c>pending</c> (a crash), or voided with an unconfirmed rollback.</summary>
    Task<IReadOnlyList<RoundRecord>> NeedingRollbackAsync(DateTimeOffset pendingBefore, CancellationToken cancellationToken);

    /// <summary>Spins whose win is owed and not yet confirmed by the casino, oldest first.</summary>
    Task<IReadOnlyList<(RoundRecord Round, SpinRecord Spin)>> UnpaidWinsAsync(int limit, CancellationToken cancellationToken);
}
