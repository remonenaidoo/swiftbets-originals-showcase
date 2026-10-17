namespace SwiftBets.Originals.Domain.Slot;

/// <summary>
/// One winning payline: <paramref name="Count"/> is the total count (split symbols count 2 or 3) over the first
/// <paramref name="Reels"/> reels; <paramref name="Units"/> is in line-bet units, before any multiplier.
/// </summary>
public sealed record LineWin(int Line, byte Symbol, int Count, int Reels, long Units);

/// <summary>
/// A spin's result. <paramref name="TotalUnits"/> is in line-bet units with the multiplier applied;
/// <paramref name="Locked"/> is the feature's locked cells after this spin (empty when the game does not lock).
/// </summary>
public sealed record SpinOutcome(
    int[] Stops,
    byte[,] Window,
    IReadOnlyList<LineWin> Lines,
    int Scatters,
    long ScatterUnits,
    long TotalUnits,
    bool TriggersFeature,
    IReadOnlyList<LockedCell> Locked);
