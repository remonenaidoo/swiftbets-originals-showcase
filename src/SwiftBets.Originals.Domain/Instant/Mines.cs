namespace SwiftBets.Originals.Domain.Instant;

/// <summary>
/// Mines: 25 tiles, the player chooses 1–24 mines and reveals tiles until they cash out or hit a mine. After k safe
/// picks the multiplier is r × C(25, k) / C(25 − m, k) at the game's rate r: the inverse of the chance of surviving k
/// picks, times the rate, so every way of playing returns r (less the flooring to a basis point and the 5,000× cap).
/// </summary>
public static class Mines
{
    public const int Tiles = 25;
    public const string GameId = "mines";
    public static readonly long[] Stakes = [100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000];

    /// <summary>The mine tiles: a Fisher–Yates shuffle of the 25 tiles by the round's floats, its first <paramref name="mines"/>.</summary>
    public static int[] Layout(IReadOnlyList<double> floats, int mines)
    {
        var tiles = Enumerable.Range(0, Tiles).ToArray();
        for (var i = Tiles - 1; i > 0; i--)
        {
            var j = (int)(floats[Tiles - 1 - i] * (i + 1));
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }

        return [.. tiles.Take(mines).Order()];
    }

    /// <summary>The multiplier after <paramref name="safePicks"/> safe picks, in basis points, floored. Reachable picks never pass 5,000× (see <see cref="Finished"/>).</summary>
    public static long MultiplierBp(int mines, int safePicks, long rateBp = Mult.Base) => (long)Int128.Min(Raw(mines, safePicks, rateBp), Mult.CapBp);

    /// <summary>
    /// True when the round must cash out: every safe tile is revealed, or the next pick would pass the 5,000× cap.
    /// Stopping there, instead of capping that pick's multiplier, keeps every way of playing at the rate: a capped pick
    /// would carry the full risk for a fraction of its fair pay.
    /// </summary>
    public static bool Finished(int mines, int safePicks, long rateBp = Mult.Base) => safePicks >= Tiles - mines || Raw(mines, safePicks + 1, rateBp) > Mult.CapBp;

    /// <summary>The fair multiplier after k safe picks; 0 past the last safe tile (there is no such pick).</summary>
    private static Int128 Raw(int mines, int safePicks, long rateBp) =>
        safePicks == 0 ? Mult.One : safePicks > Tiles - mines ? 0 : (Int128)rateBp * Choose(Tiles, safePicks) / Choose(Tiles - mines, safePicks);

    private static Int128 Choose(int n, int k)
    {
        Int128 r = 1;
        for (var i = 1; i <= k; i++)
        {
            r = r * (n - k + i) / i;
        }

        return r;
    }
}
