using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

public sealed record SlotEngine(SlotGame Game, RoundEngine Engine);

public sealed class SlotEngines(GameCatalog catalog, Func<int, int> draw)
{
    private readonly Dictionary<string, RoundEngine> engines = catalog.All.ToDictionary(g => g.Id, g => new RoundEngine(g.Definition, draw));

    public GameCatalog Catalog => catalog;

    public SlotEngine? Find(string? gameId) => catalog.Find(gameId) is { } game ? new SlotEngine(game, engines[game.Id]) : null;
}
