namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Instant-game multipliers in basis points (×10,000), so pays are exact integers.</summary>
public static class Multipliers
{
    public const long One = 10_000;

    public const long CapBp = 5_000 * One;

    public const int MaxWinStakes = (int)(CapBp / One);

    /// <summary>The 96.00% RTP every game was designed at; a game's configured rate scales its pays by rate ÷ Base.</summary>
    public const long Base = 9_600;

    public static long Rated(long bp, long rateBp) => (long)((Int128)bp * rateBp / Base);

    /// <summary>The win for a stake at a multiplier, capped and floored to the cent.</summary>
    public static long Pay(long stake, long bp) => (long)((Int128)stake * Math.Min(bp, CapBp) / One);
}
