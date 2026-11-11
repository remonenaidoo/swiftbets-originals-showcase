using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>One way win: a symbol over the first <paramref name="Reels"/> reels, <paramref name="Ways"/> ways (multipliers included), the cells it used.</summary>
public sealed record WayWin(string Symbol, int Reels, long Ways, long Units, int[][] Cells);

public sealed record WaysSpin(bool Free, int[] Stops, IReadOnlyList<WayWin> Wins, int Scatters, long ScatterUnits, long Units)
{
    /// <summary>What this spin paid after the round's cap.</summary>
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

/// <summary>Cape Point Ways: 243 ways, ×2/×3 Wilds and Pearl-triggered free spins; one engine, several themes.</summary>
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

    /// <summary>Spins after the cap is reached are not played.</summary>
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

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var (spins, units, capped) = PlayRound(floats);
        // The playback is in design units; the page scales them by rateBp ÷ Multipliers.Base, as the pay is here.
        return new(Multipliers.Rated(units * (Multipliers.One / Units), rateBp), new { spins, units, capped, freeSpins = spins.Count - 1 });
    }

    public override object InfoAt(long rateBp) => new
    {
        units = Units, symbols = WaysTables.Symbols, wilds = WaysTables.WildMultiplier, pays = WaysTables.Pays, scatterStakes = WaysTables.ScatterStakes,
        freeSpins = WaysTables.FreeSpins, baseReels = WaysTables.Base, freeReels = WaysTables.Free, rateBp,
    };
}
