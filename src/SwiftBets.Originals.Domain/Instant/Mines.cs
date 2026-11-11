namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Mines: after k safe picks the multiplier is r × C(25, k) / C(25 − m, k), so every way of playing returns r.</summary>
public static class Mines
{
    public const int Tiles = 25;
    public const string GameId = "mines";

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

    /// <summary>Floored; reachable picks never pass the cap (see <see cref="Finished"/>).</summary>
    public static long MultiplierBp(int mines, int safePicks, long rateBp = Multipliers.Base) => (long)Int128.Min(Raw(mines, safePicks, rateBp), Multipliers.CapBp);

    /// <summary>Ends the round before a pick would pass the cap rather than capping it, so no pick carries full risk for part of its pay.</summary>
    public static bool Finished(int mines, int safePicks, long rateBp = Multipliers.Base) => safePicks >= Tiles - mines || Raw(mines, safePicks + 1, rateBp) > Multipliers.CapBp;

    /// <summary>The fair multiplier after k safe picks; 0 past the last safe tile (there is no such pick).</summary>
    private static Int128 Raw(int mines, int safePicks, long rateBp) =>
        safePicks == 0 ? Multipliers.One : safePicks > Tiles - mines ? 0 : (Int128)rateBp * Choose(Tiles, safePicks) / Choose(Tiles - mines, safePicks);

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
