using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>A cell of the cascade board: its symbol and, for a meteor, its multiplier.</summary>
public sealed record GemCell(string Symbol, int Multiplier = 0);

/// <summary>One pay-anywhere win in a tumble step: a symbol, how many, its pay in units, the cells (reel, row) removed.</summary>
public sealed record GemWin(string Symbol, int Count, long Units, int[][] Cells);

/// <summary>One tumble step: what paid and the board after the winners fell away and the reels dropped.</summary>
public sealed record GemStep(IReadOnlyList<GemWin> Wins, GemCell[][] Board);

/// <summary>One spin: the board it landed, its tumble steps, the Suns at the end, the meteors' total and its pay.</summary>
public sealed record GemSpin(bool Free, int[] Stops, GemCell[][] Board, IReadOnlyList<GemStep> Steps, int Suns, int Multiplier, long Units)
{
    /// <summary>What this spin actually paid: its units, or what was left under the round's cap (the playback shows this).</summary>
    public long Paid { get; init; } = Units;
}

/// <summary>Everything the cascade's maths depends on, so the maths tool can try other values without a rebuild.</summary>
public sealed record CascadeConfig(
    IReadOnlyDictionary<string, int> BaseCounts,
    IReadOnlyDictionary<string, int> FreeCounts,
    IReadOnlyDictionary<string, int[]> Pays,
    int[] SunStakes,
    (int Value, int Weight)[] Meteors)
{
    /// <summary>Each reel's base strip: the counts in a fixed shuffle per reel (pay-anywhere needs symbols to clump).</summary>
    public string[][] BaseStrips { get; } = [.. Enumerable.Range(0, CascadeSlot.Reels).Select(r => Shuffle(BaseCounts, 1 + r))];

    public string[][] FreeStrips { get; } = [.. Enumerable.Range(0, CascadeSlot.Reels).Select(r => Shuffle(FreeCounts, 101 + r))];

    /// <summary>
    /// A strip from symbol counts in a fixed pseudo-random order (a Fisher–Yates shuffle driven by a 64-bit LCG from
    /// <paramref name="seed"/>), so it is the same on every machine and in the maths document.
    /// </summary>
    public static string[] Shuffle(IReadOnlyDictionary<string, int> counts, int seed)
    {
        var strip = counts.OrderBy(kv => kv.Key, StringComparer.Ordinal).SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value)).ToArray();
        var x = (ulong)seed * 0x9E3779B97F4A7C15UL + 1;
        for (var i = strip.Length - 1; i > 0; i--)
        {
            x = (x * 6364136223846793005UL) + 1442695040888963407UL;
            var j = (int)((x >> 33) % (ulong)(i + 1));
            (strip[i], strip[j]) = (strip[j], strip[i]);
        }

        return strip;
    }
}

/// <summary>
/// Kalahari Gem Fall: 6 reels × 5 rows; a symbol pays anywhere when 8 or more show (more pays more). Winners vanish
/// and each reel drops: survivors fall and new symbols come from that reel's strip above the board, so the drop is
/// fixed by the stop. Tumbles go on while anything pays. 4+ Suns pay and award 10 free spins (3+ in free spins add 5,
/// up to 60 in all). In free spins meteors land carrying ×2 to ×50; they stay through the tumbles, and when nothing
/// more pays the spin's win is multiplied by their sum. A stake is 100 units; one request plays the whole round,
/// capped at 5,000× stake.
/// </summary>
/// <remarks>One engine, several themes: a theme is a new id and name on the same maths (its page maps the symbols to its own art).</remarks>
public sealed class CascadeSlot(string id = CascadeSlot.GameId, string name = "Kalahari Gem Fall") : OneShotGame
{
    public const string GameId = "kalahari-gems";
    public const int Units = 100;
    public const long CapUnits = 5_000L * Units;
    public const int Reels = 6;
    public const int Rows = 5;
    public const int MaxFreeSpins = 60;
    public const int Floats = 4096;

    public static CascadeConfig Production { get; } = CascadeTables.Config;

    public override string Id => id;

    public override string Name => name;

    public override long[] Stakes { get; } = [100, 200, 500, 1000, 2000, 5000, 10000, 20000];

    public override bool Valid(JsonElement c) => true;

    public override int FloatsNeeded(JsonElement c) => Floats;

    /// <summary>A whole round under a config (production: <see cref="Production"/>).</summary>
    public static (IReadOnlyList<GemSpin> Spins, long Units, bool Capped) PlayRound(CascadeConfig cfg, IReadOnlyList<double> floats)
    {
        var at = 0;
        var spins = new List<GemSpin>();
        var first = Spin(cfg, false, floats, ref at);
        var paid = Math.Min(first.Units, CapUnits);
        spins.Add(first with { Paid = paid });
        var left = first.Suns >= 4 ? 10 : 0;
        var played = 0;
        while (left > 0 && paid < CapUnits && played < MaxFreeSpins)
        {
            var s = Spin(cfg, true, floats, ref at);
            var more = Math.Min(s.Units, CapUnits - paid);
            spins.Add(s with { Paid = more });
            paid += more;
            played++;
            left += (s.Suns >= 3 ? 5 : 0) - 1;
        }

        return (spins, paid, paid >= CapUnits);
    }

    public static GemSpin Spin(CascadeConfig cfg, bool free, IReadOnlyList<double> floats, ref int at)
    {
        var strips = free ? cfg.FreeStrips : cfg.BaseStrips;
        var stops = new int[Reels];
        var pos = new int[Reels];
        var board = new GemCell[Reels][];
        for (var r = 0; r < Reels; r++)
        {
            var strip = strips[r];
            stops[r] = (int)(floats[at++] * strip.Length);
            pos[r] = stops[r];
            board[r] = new GemCell[Rows];
            for (var row = 0; row < Rows; row++) board[r][row] = Cell(cfg, strip[(pos[r] + row) % strip.Length], floats, ref at);
        }

        var landed = Copy(board);
        var steps = new List<GemStep>();
        long units = 0;
        while (true)
        {
            var wins = Wins(cfg, board);
            if (wins.Count == 0) break;
            units += wins.Sum(w => w.Units);
            var removed = wins.SelectMany(w => w.Cells).Select(c => (c[0], c[1])).ToHashSet();
            for (var r = 0; r < Reels; r++)
            {
                var strip = strips[r];
                var survivors = board[r].Where((_, row) => !removed.Contains((r, row))).ToList();
                var k = Rows - survivors.Count;
                if (k == 0) continue;
                pos[r] = ((pos[r] - k) % strip.Length + strip.Length) % strip.Length;
                var fresh = new List<GemCell>(k);
                for (var i = 0; i < k; i++) fresh.Add(Cell(cfg, strip[(pos[r] + i) % strip.Length], floats, ref at));
                board[r] = [.. fresh.Concat(survivors)];
            }

            steps.Add(new GemStep(wins, Copy(board)));
        }

        var suns = board.Sum(col => col.Count(c => c.Symbol == "Sun"));
        long sunUnits = suns >= 4 ? (long)cfg.SunStakes[Math.Min(suns, 6) - 4] * Units : 0;
        var multiplier = free ? board.Sum(col => col.Sum(c => c.Multiplier)) : 0;
        var total = units * (units > 0 && multiplier > 0 ? multiplier : 1) + sunUnits;
        return new GemSpin(free, stops, landed, steps, suns, multiplier, total);
    }

    private static GemCell Cell(CascadeConfig cfg, string symbol, IReadOnlyList<double> floats, ref int at)
    {
        if (symbol != "Meteor") return new(symbol);
        var x = (int)(floats[at++] * cfg.Meteors.Sum(m => m.Weight));
        foreach (var (value, weight) in cfg.Meteors)
        {
            if (x < weight) return new(symbol, value);
            x -= weight;
        }

        return new(symbol, cfg.Meteors[^1].Value);
    }

    /// <summary>Every symbol with 8 or more on the board pays its tier (8–9, 10–11, 12+).</summary>
    public static List<GemWin> Wins(CascadeConfig cfg, GemCell[][] board)
    {
        var wins = new List<GemWin>();
        foreach (var (symbol, pays) in cfg.Pays)
        {
            var cells = new List<int[]>();
            for (var r = 0; r < Reels; r++) for (var row = 0; row < Rows; row++) if (board[r][row].Symbol == symbol) cells.Add([r, row]);
            if (cells.Count < 8) continue;
            var tier = cells.Count >= 12 ? 2 : cells.Count >= 10 ? 1 : 0;
            wins.Add(new GemWin(symbol, cells.Count, pays[tier], [.. cells]));
        }

        return wins;
    }

    private static GemCell[][] Copy(GemCell[][] b) => [.. b.Select(col => col.ToArray())];

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var (spins, units, capped) = PlayRound(Production, floats);
        return new(Mult.Rated(units * (Mult.One / Units), rateBp), new { spins, units, capped, freeSpins = spins.Count - 1 });
    }

    public override object InfoAt(long rateBp) => new
    {
        units = Units, reels = Reels, rows = Rows, pays = Production.Pays, sunStakes = Production.SunStakes,
        meteors = Production.Meteors.Select(m => new { m.Value, m.Weight }), baseStrips = Production.BaseStrips, freeStrips = Production.FreeStrips, rateBp,
    };
}
