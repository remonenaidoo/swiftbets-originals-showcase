using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Games;

/// <summary>Every slot this studio offers. A new game is a class in this project and a line here.</summary>
public static class OriginalsGames
{
    public static GameCatalog Catalog { get; } = new([GildedSavanna.Game, GoldReefRush.Game, KarooStargazer.Game]);

    /// <summary>Each game rebuilt from other reel counts (base, free), for tuning in the maths tool.</summary>
    public static IReadOnlyDictionary<string, Func<IReadOnlyList<IReadOnlyDictionary<string, int>>, IReadOnlyList<IReadOnlyDictionary<string, int>>, GameDefinition>> Builders { get; } =
        new Dictionary<string, Func<IReadOnlyList<IReadOnlyDictionary<string, int>>, IReadOnlyList<IReadOnlyDictionary<string, int>>, GameDefinition>>
        {
            [GildedSavanna.GameId] = GildedSavanna.Build,
            [GoldReefRush.GameId] = GoldReefRush.Build,
        };
}
