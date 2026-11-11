namespace SwiftBets.Originals.Domain.Slot;

/// <summary>Everything that decides outcomes and pays; its hash is recorded in the PAR sheet.</summary>
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
