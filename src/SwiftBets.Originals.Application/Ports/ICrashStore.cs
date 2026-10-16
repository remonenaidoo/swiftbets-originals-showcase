namespace SwiftBets.Originals.Application.Ports;

public static class CrashPhase
{
    public const string Betting = "betting";
    public const string Flying = "flying";
    public const string Crashed = "crashed";

    /// <summary>Interrupted (the service stopped mid-round): its open bets were refunded.</summary>
    public const string Void = "void";
}

/// <summary>A shared Crash round. <paramref name="Seed"/> is secret until the round has crashed.</summary>
public sealed record CrashRound(Guid Id, long Seq, string Seed, string SeedHash, long PointBp, string Phase, DateTimeOffset BettingUntil, DateTimeOffset? CrashedAt);

/// <summary>A player's bet in a shared round: their own round row, and their auto cash-out target if set.</summary>
public sealed record CrashBetRecord(RoundRecord Round, Guid Table, long? AutoBp);

public interface ICrashStore
{
    Task<long> LastSeqAsync(CancellationToken cancellationToken);

    Task InsertAsync(CrashRound round, CancellationToken cancellationToken);

    Task SetPhaseAsync(Guid id, string phase, DateTimeOffset? crashedAt, CancellationToken cancellationToken);

    /// <summary>Rounds left betting or flying: only after a stop mid-round, since one loop runs them.</summary>
    Task<IReadOnlyList<CrashRound>> UnfinishedAsync(CancellationToken cancellationToken);

    /// <summary>The last crashed rounds, newest first.</summary>
    Task<IReadOnlyList<CrashRound>> HistoryAsync(int count, CancellationToken cancellationToken);

    /// <summary>Stores a bet's round and first spin with its table and auto target, before any money moves.</summary>
    Task InsertBetAsync(RoundRecord round, SpinRecord spin, Guid table, long? autoBp, CancellationToken cancellationToken);

    /// <summary>The bets of a shared round still open (stake taken, not cashed out).</summary>
    Task<IReadOnlyList<CrashBetRecord>> OpenBetsAsync(Guid table, CancellationToken cancellationToken);

    Task<CrashBetRecord?> BetAsync(Guid roundId, CancellationToken cancellationToken);

    /// <summary>This player's bets in a shared round, in any state but refused (a bet still taking its stake counts).</summary>
    Task<IReadOnlyList<CrashBetRecord>> PlayerBetsAsync(Guid table, string playerRef, CancellationToken cancellationToken);
}

/// <summary>Pushes the shared round to every viewer (SignalR). Never carries anything that identifies a player.</summary>
public interface ICrashBroadcast
{
    Task PublishAsync(string kind, object payload, CancellationToken cancellationToken);
}
