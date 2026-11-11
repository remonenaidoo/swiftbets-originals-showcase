namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Crash: the multiplier climbs as e^(0.00006 t); P(point ≥ x) = r / x, so every cash-out target returns r.</summary>
public static class Crash
{
    public const string GameId = "crash";

    /// <summary>The fixed "client seed" of the shared round: the round's own seed and its sequence number make the draw.</summary>
    public const string ClientSeed = "crash";

    public const double Rate = 0.00006;
    public static readonly TimeSpan Betting = TimeSpan.FromSeconds(7);
    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(3);
    public const long MinAutoBp = 10_100;

    /// <summary>The crash point for a uniform float at rate r (basis points): floor(100r / (1 − u)) hundredths, at least 1.00×, at most 5,000×.</summary>
    public static long PointBp(double u, long rateBp = Multipliers.Base)
    {
        var hundredths = Math.Floor(rateBp / 100.0 / (1 - u));
        return Math.Clamp((long)Math.Min(hundredths, 1e9), 100, Multipliers.CapBp / 100) * 100;
    }

    public static long MultiplierBpAt(double elapsedMs) =>
        elapsedMs <= 0 ? Multipliers.One : Math.Min((long)Math.Floor(Math.Exp(Rate * elapsedMs) * 100) * 100, Multipliers.CapBp);

    /// <summary>The first whole millisecond at which the multiplier shows <paramref name="bp"/>.</summary>
    public static long ElapsedFor(long bp)
    {
        var t = (long)Math.Ceiling(Math.Log(bp / (double)Multipliers.One) / Rate);
        while (t > 0 && MultiplierBpAt(t - 1) >= bp) t--;
        while (MultiplierBpAt(t) < bp) t++;
        return t;
    }
}
