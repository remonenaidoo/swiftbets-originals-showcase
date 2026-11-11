namespace SwiftBets.Originals.Application.Ports;

public static class RoundStatus
{
    public const string Pending = "pending";
    public const string Refused = "refused";
    public const string Voided = "voided";
    public const string WinPending = "win_pending";
    public const string Feature = "feature";
    public const string Settled = "settled";

    /// <summary>An instant round that has taken its stake and is still being played.</summary>
    public const string Open = "open";
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

public sealed record SpinRecord(Guid RoundId, int Seq, int[] Stops, string OutcomeJson, long Win, string? BetTx, string? WinTx, bool WinPaid);

/// <summary>A spin as stored, with its round as it stands after the spin.</summary>
public sealed record PlayedSpin(RoundRecord Round, SpinRecord Spin);

/// <summary>What one locked step of an open round stores: its spin and the round's new status and paid amount.</summary>
public sealed record RoundStep(SpinRecord Spin, string Status, long Paid);

public sealed record FreeSpinStep(SpinRecord Spin, int SpinsLeft, long PaidUnits);

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

    /// <summary>Under the round's row lock, plays one free spin after the last; null when the round has none left for this player and game.</summary>
    Task<PlayedSpin?> ClaimFreeSpinAsync(Guid roundId, string playerRef, string gameId, Func<RoundRecord, SpinRecord, FreeSpinStep> play, CancellationToken cancellationToken);

    /// <summary>The player's unfinished instant round of this game (open, or still taking its stake), if any.</summary>
    Task<RoundRecord?> FindOpenAsync(string playerRef, string gameId, CancellationToken cancellationToken);

    /// <summary>Under the round's row lock, applies one step to an open round; null when it is not open for this player and game, or the step declines.</summary>
    Task<PlayedSpin?> StepAsync(Guid roundId, string playerRef, string gameId, Func<RoundRecord, IReadOnlyList<SpinRecord>, RoundStep?> step, CancellationToken cancellationToken);

    /// <summary>Moves a <c>win_pending</c> round to <c>settled</c> once nothing is owed.</summary>
    Task SettleIfPaidAsync(Guid roundId, CancellationToken cancellationToken);

    /// <summary>Voids the round and marks its rollback owed only if it is still <paramref name="fromStatus"/>, so a paid round is never voided.</summary>
    Task<bool> VoidIfAsync(Guid roundId, string fromStatus, CancellationToken cancellationToken);

    Task SetRollbackPendingAsync(Guid roundId, bool pending, CancellationToken cancellationToken);

    /// <summary>Rounds stuck <c>pending</c> since before <paramref name="pendingBefore"/>, or voided with an unconfirmed rollback.</summary>
    Task<IReadOnlyList<RoundRecord>> NeedingRollbackAsync(DateTimeOffset pendingBefore, CancellationToken cancellationToken);

    /// <summary>Spins whose win the casino has not confirmed, oldest first.</summary>
    Task<IReadOnlyList<PlayedSpin>> UnpaidWinsAsync(int limit, CancellationToken cancellationToken);
}
