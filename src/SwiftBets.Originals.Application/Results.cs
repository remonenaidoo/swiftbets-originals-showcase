namespace SwiftBets.Originals.Application;

/// <summary>A refusal the API returns as an error envelope with <paramref name="Http"/> and <paramref name="Code"/>.</summary>
public sealed record SpinError(int Http, string Code)
{
    public static readonly SpinError InvalidStake = new(400, "invalid_stake");
    public static readonly SpinError SessionInvalid = new(401, "session_invalid");
    public static readonly SpinError InsufficientFunds = new(402, "insufficient_funds");
    public static readonly SpinError LimitExceeded = new(403, "limit_exceeded");
    public static readonly SpinError AccountRestricted = new(403, "account_restricted");
    public static readonly SpinError GameNotFound = new(404, "game_not_found");
    public static readonly SpinError RoundNotFound = new(404, "round_not_found");
    public static readonly SpinError FeatureInProgress = new(409, "feature_in_progress");
    public static readonly SpinError NoFeature = new(409, "no_feature");
    public static readonly SpinError WalletUnavailable = new(503, "wallet_unavailable");
}

public sealed record Result<T>(T? Value, SpinError? Error)
{
    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(SpinError error) => new(default, error);
}

/// <summary>A paying line as the client shows it: the symbol by name, its total count, and how many reels the run covers.</summary>
public sealed record LineView(int Line, string Symbol, int Count, int Reels, long Units);

/// <summary>A cell locked for the rest of the feature.</summary>
public sealed record LockView(int Reel, int Row, string Symbol);

/// <summary>What the client animates: the server's stops, pays and locked cells, never its own.</summary>
public sealed record SpinView(
    Guid RoundId,
    int Seq,
    int[] Stops,
    IReadOnlyList<LineView> Lines,
    int Scatters,
    long Win,
    long RoundPaid,
    int FreeSpinsLeft,
    bool FeatureTriggered,
    bool CapReached,
    long? Balance,
    IReadOnlyList<LockView> Locked);

public sealed record RoundSummary(Guid RoundId, long Stake, long Paid, string Status, DateTimeOffset At);

public sealed record SymbolInfo(string Name, string Kind, int Weight, string PaysAs);

public sealed record GameInfo(
    string GameId,
    string Name,
    decimal Rtp,
    int MaxWinStakes,
    long[] Stakes,
    int Lines,
    IReadOnlyList<SymbolInfo> Symbols,
    IReadOnlyDictionary<string, int[]> Pays,
    int[] ScatterPays,
    int FreeSpins,
    int FreeSpinMultiplier,
    bool Retrigger,
    bool LockSplits,
    IReadOnlyList<int[]> Paylines,
    IReadOnlyList<string[]> BaseReels,
    IReadOnlyList<string[]> FreeReels);

public sealed record StateView(long? Balance, SpinView? OpenFeature, IReadOnlyList<RoundSummary> Recent, GameInfo Info);
