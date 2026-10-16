using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Games;

/// <summary>
/// Gilded Savanna: 5 reels × 3 rows, 20 fixed lines, Protea Wild on reels 2–4, Baobab Scatter (3+ = 10 free
/// spins at ×2, retriggerable), max win 5,000× stake. The reel counts below are the tuned design; see
/// docs/par-sheet/gilded-savanna.md for the RTP they produce.
/// </summary>
public static class GildedSavanna
{
    public const string GameId = "gilded-savanna";

    /// <summary>The symbol table; its order is the order the definition hash and the strip layout were made with.</summary>
    public static readonly SymbolDef[] Symbols =
    [
        new("Lion"), new("Elephant"), new("Rhino"), new("Buffalo"),
        new("Ace"), new("King"), new("Queen"), new("Jack"), new("Ten"),
        new("Wild", SymbolKind.Wild), new("Scatter", SymbolKind.Scatter),
    ];

    private static readonly int[][] Paylines =
    [
        [1, 1, 1, 1, 1], [0, 0, 0, 0, 0], [2, 2, 2, 2, 2], [0, 1, 2, 1, 0], [2, 1, 0, 1, 2],
        [0, 0, 1, 2, 2], [2, 2, 1, 0, 0], [1, 0, 0, 0, 1], [1, 2, 2, 2, 1], [0, 1, 1, 1, 0],
        [2, 1, 1, 1, 2], [1, 0, 1, 2, 1], [1, 2, 1, 0, 1], [0, 1, 0, 1, 0], [2, 1, 2, 1, 2],
        [1, 1, 0, 1, 1], [1, 1, 2, 1, 1], [0, 0, 2, 0, 0], [2, 2, 0, 2, 2], [0, 2, 0, 2, 0],
    ];

    private static readonly Dictionary<string, int[]> Pays = new()
    {
        ["Lion"] = [25, 100, 500],
        ["Elephant"] = [20, 75, 300],
        ["Rhino"] = [15, 50, 200],
        ["Buffalo"] = [10, 40, 150],
        ["Ace"] = [5, 20, 100],
        ["King"] = [5, 15, 75],
        ["Queen"] = [4, 12, 60],
        ["Jack"] = [3, 10, 50],
        ["Ten"] = [2, 8, 40],
    };

    /// <summary>Symbol counts per base reel (each reel 36 stops).</summary>
    public static readonly Dictionary<string, int>[] BaseCounts =
    [
        Counts(wild: 0, lion: 3, elephant: 3, rhino: 4, buffalo: 4, ace: 4, king: 4, queen: 6, jack: 4, ten: 3, scatter: 1),
        Counts(wild: 4, lion: 3, elephant: 3, rhino: 3, buffalo: 4, ace: 4, king: 4, queen: 4, jack: 3, ten: 3, scatter: 1),
        Counts(wild: 4, lion: 3, elephant: 3, rhino: 3, buffalo: 4, ace: 4, king: 4, queen: 4, jack: 3, ten: 3, scatter: 1),
        Counts(wild: 4, lion: 3, elephant: 3, rhino: 3, buffalo: 4, ace: 4, king: 4, queen: 4, jack: 3, ten: 3, scatter: 1),
        Counts(wild: 0, lion: 3, elephant: 3, rhino: 4, buffalo: 4, ace: 4, king: 4, queen: 6, jack: 4, ten: 3, scatter: 1),
    ];

    /// <summary>Symbol counts per free-spin reel (each reel 36 stops).</summary>
    public static readonly Dictionary<string, int>[] FreeCounts =
    [
        Counts(wild: 0, lion: 5, elephant: 5, rhino: 5, buffalo: 4, ace: 4, king: 2, queen: 3, jack: 4, ten: 3, scatter: 1),
        Counts(wild: 7, lion: 5, elephant: 5, rhino: 4, buffalo: 3, ace: 2, king: 2, queen: 3, jack: 2, ten: 2, scatter: 1),
        Counts(wild: 7, lion: 5, elephant: 5, rhino: 4, buffalo: 3, ace: 2, king: 2, queen: 3, jack: 2, ten: 2, scatter: 1),
        Counts(wild: 7, lion: 5, elephant: 5, rhino: 4, buffalo: 3, ace: 2, king: 2, queen: 3, jack: 2, ten: 2, scatter: 1),
        Counts(wild: 0, lion: 5, elephant: 5, rhino: 5, buffalo: 4, ace: 4, king: 2, queen: 3, jack: 4, ten: 3, scatter: 1),
    ];

    public static GameDefinition Definition { get; } = Build(BaseCounts, FreeCounts);

    public static SlotGame Game { get; } = new(Definition, new GameProfile("Gilded Savanna", 96.00m, [100, 200, 500, 1000, 2000, 5000]));

    /// <summary>The game with other reel counts; the maths tool uses it to try strips while tuning.</summary>
    public static GameDefinition Build(IReadOnlyList<IReadOnlyDictionary<string, int>> baseCounts, IReadOnlyList<IReadOnlyDictionary<string, int>> freeCounts) => new(
        GameId,
        Symbols,
        [.. baseCounts.Select(c => Strip.Build(Symbols, c))],
        [.. freeCounts.Select(c => Strip.Build(Symbols, c))],
        Paylines,
        Pays.ToDictionary(p => (byte)Array.FindIndex(Symbols, s => s.Name == p.Key), p => p.Value),
        ScatterPays: [2, 10, 50],
        new FeatureRules(Awarded: 10, Multiplier: 2),
        MaxWinStakes: 5000);

    private static Dictionary<string, int> Counts(int wild, int lion, int elephant, int rhino, int buffalo, int ace, int king, int queen, int jack, int ten, int scatter) => new()
    {
        ["Lion"] = lion, ["Elephant"] = elephant, ["Rhino"] = rhino, ["Buffalo"] = buffalo,
        ["Ace"] = ace, ["King"] = king, ["Queen"] = queen, ["Jack"] = jack, ["Ten"] = ten,
        ["Wild"] = wild, ["Scatter"] = scatter,
    };
}
