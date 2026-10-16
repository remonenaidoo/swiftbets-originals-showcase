namespace SwiftBets.Originals.Domain.Slot;

/// <summary>What players are told and offered: the name, the stated RTP (the PAR sheet's total, to two decimals) and the stakes in minor units.</summary>
/// <param name="Name">The game's display name.</param>
/// <param name="StatedRtp">The RTP shown in the rules, in percent.</param>
/// <param name="Stakes">The total stakes offered, each a multiple of the line count.</param>
public sealed record GameProfile(string Name, decimal StatedRtp, long[] Stakes);

/// <summary>A playable slot: its definition (the maths) and its profile.</summary>
public sealed record SlotGame
{
    public SlotGame(GameDefinition definition, GameProfile profile)
    {
        // Pays are line-bet units × (stake / lines): a stake that does not divide by the line count would underpay.
        if (profile.Stakes.Length == 0 || profile.Stakes.Any(s => s <= 0 || s % definition.LineCount != 0))
        {
            throw new ArgumentException($"Every stake of {definition.GameId} must be a positive multiple of its {definition.LineCount} lines.", nameof(profile));
        }

        Definition = definition;
        Profile = profile;
    }

    public GameDefinition Definition { get; init; }

    public GameProfile Profile { get; init; }

    public string Id => Definition.GameId;
}

/// <summary>The games this studio offers, by id.</summary>
public sealed class GameCatalog(IEnumerable<SlotGame> games)
{
    private readonly Dictionary<string, SlotGame> byId = games.ToDictionary(g => g.Id);

    public IReadOnlyCollection<SlotGame> All => byId.Values;

    public SlotGame? Find(string? gameId) => gameId is not null && byId.TryGetValue(gameId, out var game) ? game : null;
}
