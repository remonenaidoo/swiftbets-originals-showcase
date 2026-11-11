namespace SwiftBets.Originals.Application;

/// <summary>A paying line: its total count can exceed its reels when split symbols are on it.</summary>
public sealed record LineView(int Line, string Symbol, int Count, int Reels, long Units);

public sealed record LockView(int Reel, int Row, string Symbol);

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
    long RateBp,
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
