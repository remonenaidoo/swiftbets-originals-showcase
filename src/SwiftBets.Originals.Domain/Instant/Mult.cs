namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Instant-game multipliers in basis points (×10,000), so pays are exact integers.</summary>
public static class Mult
{
    public const long One = 10_000;

    /// <summary>5,000× stake, the cap on any round.</summary>
    public const long CapBp = 5_000 * One;

    /// <summary>
    /// The RTP every game's maths was designed at (96.00%), in basis points. A game runs at its own configured rate:
    /// formula games put the rate where 0.96 stood, table games and slots scale every pay by rate ÷ <see cref="Base"/>.
    /// </summary>
    public const long Base = 9_600;

    /// <summary>A multiplier designed at <see cref="Base"/>, paid at <paramref name="rateBp"/>, floored to a basis point.</summary>
    public static long Rated(long bp, long rateBp) => (long)((Int128)bp * rateBp / Base);

    /// <summary>The win for a stake at a multiplier, rounded down to the cent.</summary>
    public static long Pay(long stake, long bp) => (long)((Int128)stake * Math.Min(bp, CapBp) / One);
}
