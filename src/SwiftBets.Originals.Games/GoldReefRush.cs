using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Games;

/// <summary>
/// Gold Reef Rush: a Witwatersrand gold-rush mine. 5 reels × 3 rows, 15 fixed lines, stake 15 coins. Split symbols:
/// Gold Bar comes single, double and triple (a line counts to 15); Diamond, Mine Cart and Lantern single and double
/// (to 10). Miner Wild and Double Miner (counts 2) on reels 2–4. Dynamite Scatter pays anywhere and 3+ award 8 free
/// spins on richer strips, where every split symbol that lands locks in place for the rest of the feature; no
/// retrigger. Max win 5,000× stake. See docs/par-sheet/gold-reef-rush.md for the RTP these strips produce.
/// </summary>
public static class GoldReefRush
{
    public const string GameId = "gold-reef-rush";

    /// <summary>The symbol table; its order is the order the definition hash and the strip layout are made with.</summary>
    public static readonly SymbolDef[] Symbols =
    [
        new("GoldBar"), new("GoldBar2", Weight: 2, PaysAs: "GoldBar"), new("GoldBar3", Weight: 3, PaysAs: "GoldBar"),
        new("Diamond"), new("Diamond2", Weight: 2, PaysAs: "Diamond"),
        new("Cart"), new("Cart2", Weight: 2, PaysAs: "Cart"),
        new("Lantern"), new("Lantern2", Weight: 2, PaysAs: "Lantern"),
        new("Copper"), new("Silver"), new("Quartz"),
        new("Miner", SymbolKind.Wild), new("Miner2", SymbolKind.Wild, Weight: 2),
        new("Dynamite", SymbolKind.Scatter),
    ];

    private static readonly int[][] Paylines =
    [
        [1, 1, 1, 1, 1], [0, 0, 0, 0, 0], [2, 2, 2, 2, 2], [0, 1, 2, 1, 0], [2, 1, 0, 1, 2],
        [0, 0, 1, 0, 0], [2, 2, 1, 2, 2], [1, 0, 0, 0, 1], [1, 2, 2, 2, 1], [1, 0, 1, 0, 1],
        [1, 2, 1, 2, 1], [0, 1, 1, 1, 0], [2, 1, 1, 1, 2], [0, 1, 0, 1, 0], [2, 1, 2, 1, 2],
    ];

    /// <summary>Line pays for a total count of 3, 4, 5, …; a count above the last entry pays the last.</summary>
    private static readonly Dictionary<string, int[]> Pays = new()
    {
        ["GoldBar"] = [10, 20, 40, 60, 100, 150, 200, 300, 400, 600, 1000, 2000, 5000],
        ["Diamond"] = [8, 15, 30, 50, 80, 120, 200, 400],
        ["Cart"] = [6, 12, 25, 40, 60, 90, 140, 300],
        ["Lantern"] = [5, 10, 20, 30, 50, 70, 120, 240],
        ["Copper"] = [4, 8, 15, 25, 32, 40],
        ["Silver"] = [3, 6, 12, 20, 28, 36],
        ["Quartz"] = [2, 5, 10, 16, 24, 30],
    };

    /// <summary>Symbol counts per base reel (each reel 36 stops). Reels 1 and 5 have no Wilds.</summary>
    public static readonly Dictionary<string, int>[] BaseCounts =
    [
        Outer(gold: 2, gold2: 1, gold3: 1, diamond: 3, diamond2: 1, cart: 2, cart2: 2, lantern: 3, lantern2: 2, copper: 6, silver: 6, quartz: 6),
        Middle(miner: 3, miner2: 1, gold: 1, gold2: 1, gold3: 1, diamond: 2, diamond2: 1, cart: 2, cart2: 1, lantern: 3, lantern2: 0, copper: 6, silver: 6, quartz: 7),
        Middle(miner: 3, miner2: 1, gold: 1, gold2: 1, gold3: 1, diamond: 2, diamond2: 1, cart: 2, cart2: 1, lantern: 3, lantern2: 0, copper: 6, silver: 6, quartz: 7),
        Middle(miner: 3, miner2: 1, gold: 1, gold2: 1, gold3: 1, diamond: 2, diamond2: 1, cart: 2, cart2: 1, lantern: 3, lantern2: 0, copper: 6, silver: 6, quartz: 7),
        Outer(gold: 2, gold2: 1, gold3: 1, diamond: 3, diamond2: 1, cart: 2, cart2: 2, lantern: 3, lantern2: 2, copper: 6, silver: 6, quartz: 6),
    ];

    /// <summary>Symbol counts per free-spin reel (each reel 36 stops): more splits, so more locks.</summary>
    public static readonly Dictionary<string, int>[] FreeCounts =
    [
        Outer(gold: 0, gold2: 2, gold3: 1, diamond: 1, diamond2: 0, cart: 4, cart2: 4, lantern: 3, lantern2: 3, copper: 6, silver: 6, quartz: 5),
        Middle(miner: 2, miner2: 2, gold: 0, gold2: 2, gold3: 1, diamond: 4, diamond2: 3, cart: 1, cart2: 2, lantern: 2, lantern2: 3, copper: 5, silver: 5, quartz: 3),
        Middle(miner: 2, miner2: 2, gold: 0, gold2: 2, gold3: 1, diamond: 4, diamond2: 3, cart: 1, cart2: 2, lantern: 2, lantern2: 3, copper: 5, silver: 5, quartz: 3),
        Middle(miner: 2, miner2: 2, gold: 0, gold2: 2, gold3: 1, diamond: 4, diamond2: 3, cart: 1, cart2: 2, lantern: 2, lantern2: 3, copper: 5, silver: 5, quartz: 3),
        Outer(gold: 0, gold2: 2, gold3: 1, diamond: 1, diamond2: 0, cart: 4, cart2: 4, lantern: 3, lantern2: 3, copper: 6, silver: 6, quartz: 5),
    ];

    public static GameDefinition Definition { get; } = Build(BaseCounts, FreeCounts);

    /// <summary>15 coins at R0.10, R0.20, R0.50, R1 and R2: every stake divides into whole cents per line.</summary>
    public static SlotGame Game { get; } = new(Definition, new GameProfile("Gold Reef Rush", 96.00m, [150, 300, 750, 1500, 3000]));

    /// <summary>The game with other reel counts; the maths tool uses it to try strips while tuning.</summary>
    public static GameDefinition Build(IReadOnlyList<IReadOnlyDictionary<string, int>> baseCounts, IReadOnlyList<IReadOnlyDictionary<string, int>> freeCounts) => new(
        GameId,
        Symbols,
        [.. baseCounts.Select(c => Strip.Build(Symbols, c))],
        [.. freeCounts.Select(c => Strip.Build(Symbols, c))],
        Paylines,
        Pays.ToDictionary(p => (byte)Array.FindIndex(Symbols, s => s.Name == p.Key), p => p.Value),
        ScatterPays: [1, 5, 25],
        new FeatureRules(Awarded: 8, Multiplier: 1, Retrigger: false, LockSplits: true),
        MaxWinStakes: 5000);

    private static Dictionary<string, int> Outer(int gold, int gold2, int gold3, int diamond, int diamond2, int cart, int cart2, int lantern, int lantern2, int copper, int silver, int quartz) =>
        Middle(0, 0, gold, gold2, gold3, diamond, diamond2, cart, cart2, lantern, lantern2, copper, silver, quartz);

    private static Dictionary<string, int> Middle(int miner, int miner2, int gold, int gold2, int gold3, int diamond, int diamond2, int cart, int cart2, int lantern, int lantern2, int copper, int silver, int quartz) => new()
    {
        ["GoldBar"] = gold, ["GoldBar2"] = gold2, ["GoldBar3"] = gold3, ["Diamond"] = diamond, ["Diamond2"] = diamond2,
        ["Cart"] = cart, ["Cart2"] = cart2, ["Lantern"] = lantern, ["Lantern2"] = lantern2,
        ["Copper"] = copper, ["Silver"] = silver, ["Quartz"] = quartz, ["Miner"] = miner, ["Miner2"] = miner2, ["Dynamite"] = 1,
    };
}
