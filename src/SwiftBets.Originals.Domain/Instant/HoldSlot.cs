using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

public sealed record Coin(int[] Cell, int Stakes);

public sealed record HoldLine(int Line, string Symbol, int Count, long Units, int[][] Cells);

/// <summary>Respins reset to 3 whenever a coin lands.</summary>
public sealed record Respin(IReadOnlyList<Coin> Added, int Left);

public static partial class HoldTables
{
    public static readonly string[] Symbols = ["Thunderbird", "Windpomp", "Springbok", "Cosmos", "Ruby", "Sapphire", "Emerald", "Amber", "Wild", "Coin"];
    public static string[][] Reels => ReelNames;
    public const int Grand = 1_000;
    public const int Trigger = 6;

    public static readonly int[][] Lines =
    [
        [1, 1, 1, 1, 1], [0, 0, 0, 0, 0], [2, 2, 2, 2, 2], [0, 1, 2, 1, 0], [2, 1, 0, 1, 2],
        [0, 0, 1, 2, 2], [2, 2, 1, 0, 0], [1, 0, 0, 0, 1], [1, 2, 2, 2, 1], [0, 1, 1, 1, 0],
        [2, 1, 1, 1, 2], [1, 0, 1, 2, 1], [1, 2, 1, 0, 1], [0, 1, 0, 1, 0], [2, 1, 2, 1, 2],
        [1, 1, 0, 1, 1], [1, 1, 2, 1, 1], [0, 0, 2, 0, 0], [2, 2, 0, 2, 2], [0, 2, 0, 2, 0],
    ];

    public static int Value(double f)
    {
        var x = (int)(f * 1000);
        foreach (var (stakes, weight) in Values)
        {
            if (x < weight) return stakes;
            x -= weight;
        }

        return Values[^1].Stakes;
    }
}

/// <summary>Highveld Hold: a 20-line slot where six coins start a hold-and-respin feature; one engine, several themes.</summary>
public sealed class HoldSlot(string id = HoldSlot.GameId, string name = "Highveld Hold") : OneShotGame
{
    public const string GameId = "highveld-hold";
    public const int Units = 20;
    public const long CapUnits = 5_000L * Units;
    public const int Floats = 1024;

    public override string Id => id;

    public override string Name => name;

    public override long[] Stakes { get; } = [200, 400, 1000, 2000, 4000, 10000, 20000];

    public override bool Valid(JsonElement c) => true;

    public override int FloatsNeeded(JsonElement c) => Floats;

    public static string[,] Window(int[] stops)
    {
        var w = new string[5, 3];
        for (var r = 0; r < 5; r++) for (var row = 0; row < 3; row++) w[r, row] = HoldTables.Reels[r][(stops[r] + row) % HoldTables.Reels[r].Length];
        return w;
    }

    /// <summary>Line pays: a symbol from reel 1 over adjacent reels, Wilds standing in on reels 2–4; coins break a line.</summary>
    public static List<HoldLine> Lines(string[,] w)
    {
        var wins = new List<HoldLine>();
        for (var i = 0; i < HoldTables.Lines.Length; i++)
        {
            var rows = HoldTables.Lines[i];
            var symbol = w[0, rows[0]];
            if (!HoldTables.Pays.TryGetValue(symbol, out var pays)) continue;
            var count = 1;
            while (count < 5 && (w[count, rows[count]] == symbol || w[count, rows[count]] == "Wild")) count++;
            if (count >= 3) wins.Add(new HoldLine(i, symbol, count, pays[count - 3], [.. Enumerable.Range(0, count).Select(r => new[] { r, rows[r] })]));
        }

        return wins;
    }

    /// <summary>Each respin, each empty cell (reel by reel, top to bottom) lands a coin when its float is under the chance.</summary>
    public static (List<Respin> Respins, Dictionary<(int, int), int> Board) Hold(Dictionary<(int, int), int> board, IReadOnlyList<double> floats, ref int at)
    {
        var respins = new List<Respin>();
        var left = 3;
        while (left > 0 && board.Count < 15)
        {
            var added = new List<Coin>();
            for (var r = 0; r < 5; r++)
            {
                for (var row = 0; row < 3; row++)
                {
                    if (board.ContainsKey((r, row))) continue;
                    if (floats[at++] * 10_000 < HoldTables.RespinChanceBp)
                    {
                        var v = HoldTables.Value(floats[at++]);
                        board[(r, row)] = v;
                        added.Add(new Coin([r, row], v));
                    }
                }
            }

            left = added.Count > 0 ? 3 : left - 1;
            respins.Add(new Respin(added, left));
        }

        return (respins, board);
    }

    public static (int[] Stops, List<HoldLine> Lines, List<Coin> Coins, List<Respin> Respins, long FeatureUnits, bool Grand, long Units, bool Capped) PlayRound(IReadOnlyList<double> floats)
    {
        var stops = Enumerable.Range(0, 5).Select(r => (int)(floats[r] * HoldTables.Reels[r].Length)).ToArray();
        var w = Window(stops);
        var lines = Lines(w);
        var coins = new List<Coin>();
        var board = new Dictionary<(int, int), int>();
        for (var r = 0; r < 5; r++)
        {
            for (var row = 0; row < 3; row++)
            {
                if (w[r, row] != "Coin") continue;
                var v = HoldTables.Value(floats[5 + (r * 3) + row]);
                coins.Add(new Coin([r, row], v));
                board[(r, row)] = v;
            }
        }

        long units = lines.Sum(l => l.Units);
        List<Respin> respins = [];
        long feature = 0;
        var grand = false;
        if (coins.Count >= HoldTables.Trigger)
        {
            var at = 20;
            (respins, board) = Hold(board, floats, ref at);
            grand = board.Count == 15;
            feature = ((long)board.Values.Sum() + (grand ? HoldTables.Grand : 0)) * Units;
        }

        // Lines are paid first; the hold pays what is left under the cap (and the playback shows that).
        var lineUnits = Math.Min(units, CapUnits);
        var featurePaid = Math.Min(feature, CapUnits - lineUnits);
        return (stops, lines, coins, respins, featurePaid, grand, lineUnits + featurePaid, units + feature >= CapUnits);
    }

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var r = PlayRound(floats);
        return new(Multipliers.Rated(r.Units * (Multipliers.One / Units), rateBp), new { stops = r.Stops, lines = r.Lines, coins = r.Coins, respins = r.Respins, featureUnits = r.FeatureUnits, grand = r.Grand, units = r.Units, capped = r.Capped });
    }

    public override object InfoAt(long rateBp) => new
    {
        units = Units, symbols = HoldTables.Symbols, pays = HoldTables.Pays, values = HoldTables.Values.Select(v => new { v.Stakes, v.Weight }), grand = HoldTables.Grand,
        trigger = HoldTables.Trigger, respinChanceBp = HoldTables.RespinChanceBp, reels = HoldTables.Reels, lines = HoldTables.Lines, rateBp,
    };
}
