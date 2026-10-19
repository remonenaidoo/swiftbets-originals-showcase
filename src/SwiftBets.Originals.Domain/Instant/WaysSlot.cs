using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>One way win: a symbol over the first <paramref name="Reels"/> reels, <paramref name="Ways"/> ways (multipliers included), the cells it used.</summary>
public sealed record WayWin(string Symbol, int Reels, long Ways, long Units, int[][] Cells);

/// <summary>One spin of a round, as the client plays it back.</summary>
public sealed record WaysSpin(bool Free, int[] Stops, IReadOnlyList<WayWin> Wins, int Scatters, long ScatterUnits, long Units)
{
    /// <summary>What this spin actually paid: its units, or what was left under the round's cap (the playback shows this).</summary>
    public long Paid { get; init; } = Units;
}

public static partial class WaysTables
{
    public static readonly string[] Symbols = ["Shark", "Whale", "Lighthouse", "Penguin", "Anchor", "Compass", "Shell", "Starfish", "Wild", "Wild2", "Wild3", "Pearl"];
    public static readonly IReadOnlyDictionary<string, int> WildMultiplier = new Dictionary<string, int> { ["Wild"] = 1, ["Wild2"] = 2, ["Wild3"] = 3 };
    public static readonly int[] ScatterStakes = [2, 10, 50];
    public static readonly int[] FreeSpins = [8, 12, 16];
    public static string[][] Base => BaseNames;
    public static string[][] Free => FreeNames;
}

/// <summary>
/// Cape Point Ways: 5 reels × 3 rows, 243 ways. A symbol pays when it shows on adjacent reels from reel 1 (Wilds,
/// on reels 2–4, stand in): the number of ways is the product, over those reels, of the cells it fills there. In free
/// spins Wilds can be ×2 or ×3, and a reel then counts the sum of its matching cells' multipliers, which multiplies
/// every way through them. 3, 4 or 5 Pearls pay 2, 10 or 50 stakes and award 8, 12 or 16 free spins (no retrigger).
/// A stake is 20 units; one request plays the whole round, capped at 5,000× stake.
/// </summary>
/// <remarks>One engine, several themes: a theme is a new id and name on the same maths (its page maps the symbols to its own art).</remarks>
public sealed class WaysSlot(string id = WaysSlot.GameId, string name = "Cape Point Ways") : OneShotGame
{
    public const string GameId = "cape-point-ways";
    public const int Units = 20;
    public const long CapUnits = 5_000L * Units;

    public override string Id => id;

    public override string Name => name;

    public override long[] Stakes { get; } = [200, 400, 1000, 2000, 4000, 10000, 20000];

    public override bool Valid(JsonElement c) => true;

    public override int FloatsNeeded(JsonElement c) => 5 + (16 * 5);

    /// <summary>The window of a spin: rows 0–2 of each reel from its stop.</summary>
    public static string[,] Window(string[][] strips, int[] stops)
    {
        var w = new string[5, 3];
        for (var r = 0; r < 5; r++)
        {
            for (var row = 0; row < 3; row++)
            {
                w[r, row] = strips[r][(stops[r] + row) % strips[r].Length];
            }
        }

        return w;
    }

    /// <summary>Pays one window: ways for every symbol, then Pearls anywhere.</summary>
    public static WaysSpin Evaluate(string[,] w, int[] stops, bool free)
    {
        var wins = new List<WayWin>();
        long units = 0;
        foreach (var (symbol, pays) in WaysTables.Pays)
        {
            long ways = 1;
            var reels = 0;
            var cells = new List<int[]>();
            for (var r = 0; r < 5; r++)
            {
                var m = 0;
                for (var row = 0; row < 3; row++)
                {
                    var s = w[r, row];
                    var add = s == symbol ? 1 : WaysTables.WildMultiplier.GetValueOrDefault(s);
                    if (add > 0)
                    {
                        m += add;
                        cells.Add([r, row]);
                    }
                }

                if (m == 0)
                {
                    cells.RemoveAll(c => c[0] == r);
                    break;
                }

                ways *= m;
                reels++;
            }

            if (reels >= 3)
            {
                var u = pays[reels - 3] * ways;
                units += u;
                wins.Add(new WayWin(symbol, reels, ways, u, [.. cells.Where(c => c[0] < reels)]));
            }
        }

        var scatters = 0;
        foreach (var s in w)
        {
            if (s == "Pearl") scatters++;
        }

        long scatterUnits = scatters >= 3 ? (long)WaysTables.ScatterStakes[Math.Min(scatters, 5) - 3] * Units : 0;
        return new WaysSpin(free, stops, wins, scatters, scatterUnits, units + scatterUnits);
    }

    public static int[] Stops(string[][] strips, IReadOnlyList<double> floats, int at) =>
        [.. Enumerable.Range(0, 5).Select(r => (int)(floats[at + r] * strips[r].Length))];

    /// <summary>The whole round: the base spin and its free spins, paid up to the cap (spins after the cap are not played).</summary>
    public static (IReadOnlyList<WaysSpin> Spins, long Units, bool Capped) PlayRound(IReadOnlyList<double> floats)
    {
        var first = Evaluate(Window(WaysTables.Base, Stops(WaysTables.Base, floats, 0)), Stops(WaysTables.Base, floats, 0), false);
        var paid = Math.Min(first.Units, CapUnits);
        var spins = new List<WaysSpin> { first with { Paid = paid } };
        var free = first.Scatters >= 3 ? WaysTables.FreeSpins[Math.Min(first.Scatters, 5) - 3] : 0;
        for (var i = 0; i < free && paid < CapUnits; i++)
        {
            var stops = Stops(WaysTables.Free, floats, 5 + (i * 5));
            var spin = Evaluate(Window(WaysTables.Free, stops), stops, true);
            var more = Math.Min(spin.Units, CapUnits - paid);
            spins.Add(spin with { Paid = more });
            paid += more;
        }

        return (spins, paid, paid >= CapUnits);
    }

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c)
    {
        var (spins, units, capped) = PlayRound(floats);
        return new(units * (Mult.One / Units), new { spins, units, capped, freeSpins = spins.Count - 1 });
    }

    public override object Info => new
    {
        units = Units, symbols = WaysTables.Symbols, wilds = WaysTables.WildMultiplier, pays = WaysTables.Pays, scatterStakes = WaysTables.ScatterStakes,
        freeSpins = WaysTables.FreeSpins, baseReels = WaysTables.Base, freeReels = WaysTables.Free, rtp = 96.00m,
    };
}
