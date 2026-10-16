namespace SwiftBets.Originals.Domain.Slot;

public enum SymbolKind : byte
{
    Regular,
    Wild,
    Scatter,
}

/// <summary>
/// One symbol of a game's table; reels and windows hold its index (an id). A split symbol has
/// <paramref name="Weight"/> 2 or 3 (it counts as that many symbols on a line) and pays as <paramref name="PaysAs"/>,
/// its single version. A Wild stands in for every regular symbol and counts by its own weight.
/// </summary>
/// <param name="Name">Stable name: used in the PAR sheet, the definition hash, stored outcomes and the client.</param>
/// <param name="Kind">Regular, Wild or Scatter.</param>
/// <param name="Weight">How many symbols it counts as on a line (1 for most).</param>
/// <param name="PaysAs">The name of the symbol whose pays it uses; null for itself.</param>
public sealed record SymbolDef(string Name, SymbolKind Kind = SymbolKind.Regular, int Weight = 1, string? PaysAs = null);

/// <summary>A cell that keeps its symbol for the rest of a feature.</summary>
public sealed record LockedCell(int Reel, int Row, byte Symbol);

/// <summary>
/// What free spins do: <paramref name="Awarded"/> spins for 3+ Scatters, each win × <paramref name="Multiplier"/>;
/// <paramref name="Retrigger"/>: 3+ Scatters during the feature add the same again; <paramref name="LockSplits"/>:
/// every split symbol (weight above 1) that lands in a free spin stays in its cell for the rest of the feature.
/// </summary>
/// <param name="Awarded">Free spins for 3 or more Scatters.</param>
/// <param name="Multiplier">Applied to every free-spin win.</param>
/// <param name="Retrigger">Whether Scatters in the feature award more spins.</param>
/// <param name="LockSplits">Whether split symbols lock during the feature.</param>
public sealed record FeatureRules(int Awarded, int Multiplier, bool Retrigger = true, bool LockSplits = false);
