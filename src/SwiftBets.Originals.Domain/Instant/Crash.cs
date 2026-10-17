namespace SwiftBets.Originals.Domain.Instant;

/// <summary>
/// Crash: one shared round. The multiplier climbs as e^(0.00006 t) (t in ms, floored to 0.01×) until the round's
/// crash point, fixed by its seed before betting opens, with P(point ≥ x) = 0.96 / x up to 5,000×. A bet that cashes
/// out at x before the crash, or whose auto cash-out x is at most the point, is paid x; so every target returns 96%.
/// </summary>
public static class Crash
{
    public const string GameId = "crash";

    /// <summary>The fixed "client seed" of the shared round: the round's own seed and its sequence number make the draw.</summary>
    public const string ClientSeed = "crash";

    public const double Rate = 0.00006;
    public static readonly TimeSpan Betting = TimeSpan.FromSeconds(7);
    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(3);
    public static readonly long[] Stakes = Mines.Stakes;
    public const long MinAutoBp = 10_100;

    /// <summary>The crash point for a uniform float: floor(96 / (1 − u)) hundredths, at least 1.00×, at most 5,000×.</summary>
    public static long PointBp(double u)
    {
        var hundredths = Math.Floor(96 / (1 - u));
        return Math.Clamp((long)Math.Min(hundredths, 1e9), 100, Mult.CapBp / 100) * 100;
    }

    /// <summary>The multiplier <paramref name="elapsedMs"/> into the flight, floored to 0.01×.</summary>
    public static long MultiplierBpAt(double elapsedMs) =>
        elapsedMs <= 0 ? Mult.One : Math.Min((long)Math.Floor(Math.Exp(Rate * elapsedMs) * 100) * 100, Mult.CapBp);

    /// <summary>The first whole millisecond at which the multiplier shows <paramref name="bp"/>.</summary>
    public static long ElapsedFor(long bp)
    {
        var t = (long)Math.Ceiling(Math.Log(bp / (double)Mult.One) / Rate);
        while (t > 0 && MultiplierBpAt(t - 1) >= bp) t--;
        while (MultiplierBpAt(t) < bp) t++;
        return t;
    }
}
