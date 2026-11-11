using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

/// <summary>The playable games, each with its engine on the shared draw (production: the OS CSPRNG).</summary>
public sealed class SlotEngines(GameCatalog catalog, Func<int, int> draw)
{
    private readonly Dictionary<string, RoundEngine> engines = catalog.All.ToDictionary(g => g.Id, g => new RoundEngine(g.Definition, draw));

    public GameCatalog Catalog => catalog;

    public (SlotGame Game, RoundEngine Engine)? Find(string? gameId) =>
        catalog.Find(gameId) is { } game ? (game, engines[game.Id]) : null;
}
