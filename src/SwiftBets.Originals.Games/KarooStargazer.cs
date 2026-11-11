using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Games;

/// <summary>Gilded Savanna's maths symbol for symbol under new names, so its PAR sheet holds unchanged.</summary>
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
