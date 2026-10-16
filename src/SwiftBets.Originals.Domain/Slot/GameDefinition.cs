namespace SwiftBets.Originals.Domain.Slot;

/// <summary>
/// Everything that decides a slot's outcomes and pays. Nothing else may: the PAR sheet is generated from this,
/// and its hash is recorded in the sheet so a change without a regenerated sheet fails the build.
/// </summary>
/// <param name="GameId">The catalogue id.</param>
/// <param name="Symbols">The symbol table; reels and pays refer to symbols by their index in it.</param>
/// <param name="BaseReels">Five strips for the base game; a spin draws one stop per strip.</param>
/// <param name="FreeReels">Five strips used during free spins.</param>
/// <param name="Lines">Paylines, each five row indexes (0 top, 2 bottom), read left to right.</param>
/// <param name="LinePays">Pays by symbol for a total count of 3, 4, 5, … in line-bet units; a count above the last entry pays the last.</param>
/// <param name="ScatterPays">Pays for 3, 4 and 5 Scatters anywhere, in total-stake units.</param>
/// <param name="Feature">The free-spin rules.</param>
/// <param name="MaxWinStakes">The most one round (a base spin and its feature) can pay, in total stakes.</param>
public sealed record GameDefinition(
    string GameId,
    IReadOnlyList<SymbolDef> Symbols,
    IReadOnlyList<IReadOnlyList<byte>> BaseReels,
    IReadOnlyList<IReadOnlyList<byte>> FreeReels,
    IReadOnlyList<int[]> Lines,
    IReadOnlyDictionary<byte, int[]> LinePays,
    int[] ScatterPays,
    FeatureRules Feature,
    int MaxWinStakes)
{
    public const int Rows = 3;

    /// <summary>No symbol: a line with only Wilds so far.</summary>
    public const byte None = byte.MaxValue;

    public int LineCount => Lines.Count;

    public int FreeSpinsAwarded => Feature.Awarded;

    public int FreeSpinMultiplier => Feature.Multiplier;

    /// <summary>The cap in line-bet units: <see cref="MaxWinStakes"/> total stakes.</summary>
    public long CapUnits => (long)MaxWinStakes * LineCount;

    // Lookups by symbol id for the evaluator's hot path (the enumeration calls it billions of times).
    internal SymbolKind[] Kinds { get; } = [.. Symbols.Select(s => s.Kind)];

    internal int[] Weights { get; } = [.. Symbols.Select(s => s.Weight)];

    internal byte[] PaysAsIds { get; } = [.. Symbols.Select((s, i) => s.PaysAs is null ? (byte)i : IdIn(Symbols, s.PaysAs))];

    internal int[]?[] PayTable { get; } = [.. Symbols.Select((_, i) => LinePays.TryGetValue((byte)i, out var p) ? p : null)];

    public byte Id(string name) => IdIn(Symbols, name);

    public string Name(byte id) => Symbols[id].Name;

    public SymbolKind KindOf(byte id) => Kinds[id];

    public int WeightOf(byte id) => Weights[id];

    private static byte IdIn(IReadOnlyList<SymbolDef> symbols, string name)
    {
        for (var i = 0; i < symbols.Count; i++)
        {
            if (symbols[i].Name == name)
            {
                return (byte)i;
            }
        }

        throw new ArgumentException($"No symbol named {name}.", nameof(name));
    }
}
