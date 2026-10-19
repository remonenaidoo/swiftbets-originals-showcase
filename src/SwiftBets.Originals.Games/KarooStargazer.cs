using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Games;

/// <summary>
/// Karoo Stargazer: Gilded Savanna's maths under the Karoo night sky. The reels, lines, pays and feature are
/// Gilded Savanna's, symbol for symbol, so docs/par-sheet/gilded-savanna.md holds for it unchanged: 5 reels × 3 rows,
/// 20 fixed lines, Southern Cross Wild on reels 2–4, Observatory Scatter (3+ = 10 free spins at ×2, retriggerable),
/// max win 5,000× stake.
/// </summary>
public static class KarooStargazer
{
    public const string GameId = "karoo-stargazer";

    /// <summary>Gilded Savanna's symbols renamed in place, in the same order (the strips hold symbol positions).</summary>
    private static readonly Dictionary<string, string> Names = new()
    {
        ["Lion"] = "Leopard", ["Elephant"] = "Kudu", ["Rhino"] = "Meerkat", ["Buffalo"] = "Ostrich",
    };

    public static GameDefinition Definition { get; } = GildedSavanna.Definition with
    {
        GameId = GameId,
        Symbols = [.. GildedSavanna.Definition.Symbols.Select(s => s with { Name = Names.GetValueOrDefault(s.Name, s.Name) })],
    };

    public static SlotGame Game { get; } = new(Definition, GildedSavanna.Game.Profile with { Name = "Karoo Stargazer" });
}
