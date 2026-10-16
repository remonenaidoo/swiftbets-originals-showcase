namespace SwiftBets.Originals.Domain.Instant;

/// <summary>
/// Towers: climb 8 rows; each row has a few tiles and one or two hide a trap. After k safe rows the multiplier is
/// 0.96 × (tiles / safe tiles)^k, floored to a basis point: the inverse of the chance of getting there, times the RTP.
/// The round cashes out by itself at the top, or before a row would pass the 5,000× cap (as Mines does).
/// </summary>
public static class Towers
{
    public const string GameId = "towers";
    public const int Rows = 8;
    public const int FloatsNeeded = Rows * 3;

    public static readonly IReadOnlyDictionary<string, (int Tiles, int Traps)> Levels = new Dictionary<string, (int, int)>
    {
        ["easy"] = (4, 1),
        ["medium"] = (3, 1),
        ["hard"] = (2, 1),
        ["expert"] = (3, 2),
    };

    /// <summary>Each row's trap tiles: a shuffle of the row's tiles by three of the round's floats, its first traps.</summary>
    public static int[][] Layout(IReadOnlyList<double> floats, string level)
    {
        var (tiles, traps) = Levels[level];
        var rows = new int[Rows][];
        for (var r = 0; r < Rows; r++)
        {
            var row = Enumerable.Range(0, tiles).ToArray();
            for (var i = tiles - 1; i > 0; i--)
            {
                var j = (int)(floats[(r * 3) + (tiles - 1 - i)] * (i + 1));
                (row[i], row[j]) = (row[j], row[i]);
            }

            rows[r] = [.. row.Take(traps).Order()];
        }

        return rows;
    }

    public static long MultiplierBp(string level, int safeRows) => (long)Int128.Min(Raw(level, safeRows), Mult.CapBp);

    public static bool Finished(string level, int safeRows) => safeRows >= Rows || Raw(level, safeRows + 1) > Mult.CapBp;

    private static Int128 Raw(string level, int k)
    {
        if (k == 0)
        {
            return Mult.One;
        }

        var (tiles, traps) = Levels[level];
        Int128 num = 9600, den = 1;
        for (var i = 0; i < k; i++)
        {
            num *= tiles;
            den *= tiles - traps;
        }

        return num / den;
    }
}
