namespace SwiftBets.Originals.Domain.Slot;

/// <summary><paramref name="Count"/> is the total count (split symbols count 2 or 3); units are before any multiplier.</summary>
public sealed record LineWin(int Line, byte Symbol, int Count, int Reels, long Units);

/// <summary><paramref name="TotalUnits"/> includes the multiplier; <paramref name="Locked"/> is the feature's locked cells after this spin.</summary>
public sealed record SpinOutcome(
    int[] Stops,
    byte[,] Window,
    IReadOnlyList<LineWin> Lines,
    int Scatters,
    long ScatterUnits,
    long TotalUnits,
    bool TriggersFeature,
    IReadOnlyList<LockedCell> Locked);
