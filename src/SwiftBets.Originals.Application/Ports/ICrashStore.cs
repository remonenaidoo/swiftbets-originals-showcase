namespace SwiftBets.Originals.Application.Ports;

public static class CrashPhase
{
    public const string Betting = "betting";
    public const string Flying = "flying";
    public const string Crashed = "crashed";

    /// <summary>Interrupted by a stop; its open bets were refunded.</summary>
    public const string Void = "void";
}

/// <summary>A shared Crash round. <paramref name="Seed"/> stays secret until the round has crashed.</summary>
public sealed record CrashRound(Guid Id, long Seq, string Seed, string SeedHash, long PointBp, string Phase, DateTimeOffset BettingUntil, DateTimeOffset? CrashedAt);

public sealed record CrashBetRecord(RoundRecord Round, Guid Table, long? AutoBp);

public interface ICrashStore
{
    Task<long> LastSeqAsync(CancellationToken cancellationToken);

    Task InsertAsync(CrashRound round, CancellationToken cancellationToken);

    /// <summary>Sets the phase; <paramref name="bettingUntil"/> moves the flight's start when take-off was late.</summary>
    Task SetPhaseAsync(Guid id, string phase, DateTimeOffset? crashedAt, DateTimeOffset? bettingUntil, CancellationToken cancellationToken);

    /// <summary>Rounds a stop left unfinished: betting, flying, or crashed with bets not yet settled.</summary>
    Task<IReadOnlyList<CrashRound>> UnfinishedAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CrashRound>> HistoryAsync(int count, CancellationToken cancellationToken);

    Task InsertBetAsync(RoundRecord round, SpinRecord spin, Guid table, long? autoBp, CancellationToken cancellationToken);

    /// <summary>Bets of a shared round that have taken their stake and not cashed out.</summary>
    Task<IReadOnlyList<CrashBetRecord>> OpenBetsAsync(Guid table, CancellationToken cancellationToken);

    Task<CrashBetRecord?> BetAsync(Guid roundId, CancellationToken cancellationToken);

    /// <summary>This player's bets in a shared round in any state but refused.</summary>
    Task<IReadOnlyList<CrashBetRecord>> PlayerBetsAsync(Guid table, string playerRef, CancellationToken cancellationToken);
}

/// <summary>Pushes the shared round to every viewer; never carries anything that identifies a player.</summary>
public interface ICrashBroadcast
{
    Task PublishAsync(string kind, object payload, CancellationToken cancellationToken);
}
