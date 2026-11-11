namespace SwiftBets.Originals.Domain.Slot;

public enum SymbolKind : byte
{
    Regular,
    Wild,
    Scatter,
}

/// <summary>A split symbol has weight 2 or 3 and pays as <paramref name="PaysAs"/>; the name is stable across hash, sheet and client.</summary>
public sealed record SymbolDef(string Name, SymbolKind Kind = SymbolKind.Regular, int Weight = 1, string? PaysAs = null);

/// <summary>A cell that keeps its symbol for the rest of a feature.</summary>
public sealed record LockedCell(int Reel, int Row, byte Symbol);

/// <summary>With <paramref name="LockSplits"/>, every split symbol landing in a free spin stays for the rest of the feature.</summary>
public sealed record FeatureRules(int Awarded, int Multiplier, bool Retrigger = true, bool LockSplits = false);
