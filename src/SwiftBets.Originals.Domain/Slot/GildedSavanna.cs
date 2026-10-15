namespace SwiftBets.Originals.Domain.Slot;

/// <summary>
/// Gilded Savanna: 5 reels × 3 rows, 20 fixed lines, Protea Wild on reels 2–4, Baobab Scatter (3+ = 10 free
/// spins at ×2, retriggerable), max win 5,000× stake. The reel counts below are the tuned design; see
/// docs/par-sheet/gilded-savanna.md for the RTP they produce.
/// </summary>
public static class GildedSavanna
{
    public const string GameId = "gilded-savanna";

    private static readonly int[][] Paylines =
    [
        [1, 1, 1, 1, 1], [0, 0, 0, 0, 0], [2, 2, 2, 2, 2], [0, 1, 2, 1, 0], [2, 1, 0, 1, 2],
        [0, 0, 1, 2, 2], [2, 2, 1, 0, 0], [1, 0, 0, 0, 1], [1, 2, 2, 2, 1], [0, 1, 1, 1, 0],
        [2, 1, 1, 1, 2], [1, 0, 1, 2, 1], [1, 2, 1, 0, 1], [0, 1, 0, 1, 0], [2, 1, 2, 1, 2],
        [1, 1, 0, 1, 1], [1, 1, 2, 1, 1], [0, 0, 2, 0, 0], [2, 2, 0, 2, 2], [0, 2, 0, 2, 0],
    ];

    private static readonly Dictionary<Symbol, int[]> Pays = new()
    {
        [Symbol.Lion] = [25, 100, 500],
        [Symbol.Elephant] = [20, 75, 300],
        [Symbol.Rhino] = [15, 50, 200],
        [Symbol.Buffalo] = [10, 40, 150],
        [Symbol.Ace] = [5, 20, 100],
        [Symbol.King] = [5, 15, 75],
        [Symbol.Queen] = [4, 12, 60],
        [Symbol.Jack] = [3, 10, 50],
        [Symbol.Ten] = [2, 8, 40],
    };

    /// <summary>Symbol counts per base reel (each reel 36 stops).</summary>
    internal static readonly Dictionary<Symbol, int>[] BaseCounts =
    [
        Counts(wild: 0, lion: 3, elephant: 3, rhino: 4, buffalo: 4, ace: 4, king: 4, queen: 6, jack: 4, ten: 3, scatter: 1),
        Counts(wild: 4, lion: 3, elephant: 3, rhino: 3, buffalo: 4, ace: 4, king: 4, queen: 4, jack: 3, ten: 3, scatter: 1),
        Counts(wild: 4, lion: 3, elephant: 3, rhino: 3, buffalo: 4, ace: 4, king: 4, queen: 4, jack: 3, ten: 3, scatter: 1),
        Counts(wild: 4, lion: 3, elephant: 3, rhino: 3, buffalo: 4, ace: 4, king: 4, queen: 4, jack: 3, ten: 3, scatter: 1),
        Counts(wild: 0, lion: 3, elephant: 3, rhino: 4, buffalo: 4, ace: 4, king: 4, queen: 6, jack: 4, ten: 3, scatter: 1),
    ];

    /// <summary>Symbol counts per free-spin reel (each reel 36 stops).</summary>
    internal static readonly Dictionary<Symbol, int>[] FreeCounts =
    [
        Counts(wild: 0, lion: 5, elephant: 5, rhino: 5, buffalo: 4, ace: 4, king: 2, queen: 3, jack: 4, ten: 3, scatter: 1),
        Counts(wild: 7, lion: 5, elephant: 5, rhino: 4, buffalo: 3, ace: 2, king: 2, queen: 3, jack: 2, ten: 2, scatter: 1),
        Counts(wild: 7, lion: 5, elephant: 5, rhino: 4, buffalo: 3, ace: 2, king: 2, queen: 3, jack: 2, ten: 2, scatter: 1),
        Counts(wild: 7, lion: 5, elephant: 5, rhino: 4, buffalo: 3, ace: 2, king: 2, queen: 3, jack: 2, ten: 2, scatter: 1),
        Counts(wild: 0, lion: 5, elephant: 5, rhino: 5, buffalo: 4, ace: 4, king: 2, queen: 3, jack: 4, ten: 3, scatter: 1),
    ];

    public static GameDefinition Definition { get; } = Build(BaseCounts, FreeCounts);

    /// <summary>The game with other reel counts; the maths tool uses it to try strips while tuning.</summary>
    public static GameDefinition Build(IReadOnlyList<IReadOnlyDictionary<Symbol, int>> baseCounts, IReadOnlyList<IReadOnlyDictionary<Symbol, int>> freeCounts) => new(
        GameId,
        [.. baseCounts.Select(Strip.Build)],
        [.. freeCounts.Select(Strip.Build)],
        Paylines,
        Pays,
        ScatterPays: [2, 10, 50],
        FreeSpinsAwarded: 10,
        FreeSpinMultiplier: 2,
        MaxWinStakes: 5000);

    private static Dictionary<Symbol, int> Counts(int wild, int lion, int elephant, int rhino, int buffalo, int ace, int king, int queen, int jack, int ten, int scatter)
    {
        var counts = new Dictionary<Symbol, int>
        {
            [Symbol.Lion] = lion, [Symbol.Elephant] = elephant, [Symbol.Rhino] = rhino, [Symbol.Buffalo] = buffalo,
            [Symbol.Ace] = ace, [Symbol.King] = king, [Symbol.Queen] = queen, [Symbol.Jack] = jack, [Symbol.Ten] = ten,
            [Symbol.Wild] = wild, [Symbol.Scatter] = scatter,
        };
        return counts.Where(c => c.Value > 0).ToDictionary();
    }
}
