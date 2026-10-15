namespace SwiftBets.Originals.Domain.Slot;

/// <summary>One winning payline: <paramref name="Units"/> is in line-bet units, before any multiplier.</summary>
public sealed record LineWin(int Line, Symbol Symbol, int Count, long Units);

/// <summary>A spin's result. <paramref name="TotalUnits"/> is in line-bet units with the multiplier applied.</summary>
public sealed record SpinOutcome(
    int[] Stops,
    Symbol[,] Window,
    IReadOnlyList<LineWin> Lines,
    int Scatters,
    long ScatterUnits,
    long TotalUnits,
    bool TriggersFeature);
