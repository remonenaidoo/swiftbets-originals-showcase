namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Instant-game multipliers in basis points (×10,000), so pays are exact integers.</summary>
public static class Mult
{
    public const long One = 10_000;

    /// <summary>5,000× stake, the cap on any round.</summary>
    public const long CapBp = 5_000 * One;

    /// <summary>The win for a stake at a multiplier, rounded down to the cent.</summary>
    public static long Pay(long stake, long bp) => (long)((Int128)stake * Math.Min(bp, CapBp) / One);
}
