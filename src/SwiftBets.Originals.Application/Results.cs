using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

/// <summary>A refusal the API returns as an error envelope with <paramref name="Http"/> and <paramref name="Code"/>.</summary>
public sealed record SpinError(int Http, string Code)
{
    public static readonly SpinError InvalidStake = new(400, "invalid_stake");
    public static readonly SpinError SessionInvalid = new(401, "session_invalid");
    public static readonly SpinError InsufficientFunds = new(402, "insufficient_funds");
    public static readonly SpinError LimitExceeded = new(403, "limit_exceeded");
    public static readonly SpinError AccountRestricted = new(403, "account_restricted");
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

/// <summary>What the client animates: the server's stops and pays, never its own.</summary>
public sealed record SpinView(
    Guid RoundId,
    int Seq,
    int[] Stops,
    IReadOnlyList<LineWin> Lines,
    int Scatters,
    long Win,
    long RoundPaid,
    int FreeSpinsLeft,
    bool FeatureTriggered,
    bool CapReached,
    long? Balance);

public sealed record RoundSummary(Guid RoundId, long Stake, long Paid, string Status, DateTimeOffset At);

public sealed record GameInfo(string GameId, string Name, decimal Rtp, int MaxWinStakes, long[] Stakes, int Lines, IReadOnlyDictionary<string, int[]> Pays, int[] ScatterPays, int FreeSpins, int FreeSpinMultiplier, IReadOnlyList<int[]> Paylines, IReadOnlyList<IReadOnlyList<Symbol>> BaseReels, IReadOnlyList<IReadOnlyList<Symbol>> FreeReels);

public sealed record StateView(long? Balance, SpinView? OpenFeature, IReadOnlyList<RoundSummary> Recent, GameInfo Info);
