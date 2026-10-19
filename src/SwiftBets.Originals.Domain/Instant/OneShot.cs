using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>A one-shot round's result: the multiplier paid (0 for a loss) and what the client shows.</summary>
public sealed record OneShotResult(long MultiplierBp, object Detail);

/// <summary>
/// An instant game settled in one draw (Dice, Limbo, Peg Drop, Keno): it validates the player's choices, says how
/// many floats it needs, and turns the round's floats and the choices into a result. No state between rounds.
/// </summary>
public abstract class OneShotGame
{
    public abstract string Id { get; }

    public abstract string Name { get; }

    public virtual long[] Stakes => Mines.Stakes;


    public abstract bool Valid(JsonElement c);

    public abstract int FloatsNeeded(JsonElement c);

    /// <summary>The round's result at the game's rate (basis points; <see cref="Mult.Base"/> is the 96% the maths was designed at).</summary>
    public abstract OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp);

    public OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c) => Resolve(floats, c, Mult.Base);

    /// <summary>What the rules sheet shows (tables, ranges), with every pay at the game's rate.</summary>
    public abstract object InfoAt(long rateBp);

    public object Info => InfoAt(Mult.Base);

    /// <summary>A table of pays designed at <see cref="Mult.Base"/>, at the game's rate.</summary>
    protected static long[] Rated(IEnumerable<long> table, long rateBp) => [.. table.Select(bp => Mult.Rated(bp, rateBp))];

    protected static int Int(JsonElement e, string name, int fallback = int.MinValue) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : fallback;

    protected static long Long(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt64(out var i) ? i : long.MinValue;
}

public static class OneShotGames
{
    public static IReadOnlyDictionary<string, OneShotGame> All { get; } =
        new OneShotGame[]
        {
            new Dice(), new Limbo(), new PegDrop(), new Keno(), new WaysSlot(), new HoldSlot(), new CascadeSlot(),
            new SunburstWheel(), new CoinFlip(), new LuckyScratch(), new GemHand(), new Baccarat(),
            new WaysSlot("sardine-run", "Sardine Run"), new HoldSlot("kimberley-hold", "Kimberley Hold"), new CascadeSlot("fynbos-bloom", "Fynbos Bloom"),
        }.ToDictionary(g => g.Id);
}

/// <summary>
/// Dice: a roll of 0.00–99.99 (10,000 equally likely outcomes); the player bets over or under a target. With c
/// winning outcomes out of 10,000 the pay is r × 10,000 / c at the game's rate r, floored to a basis point. Win chance 2%–98%.
/// </summary>
public sealed class Dice : OneShotGame
{
    public const string GameId = "dice";

    public override string Id => GameId;

    public override string Name => "Dice";

    public static int Roll(double f) => (int)(f * 10_000);

    /// <summary>Winning outcomes: over wins on a roll above the target, under on a roll below it.</summary>
    public static int Count(int target, bool over) => over ? 9_999 - target : target;

    public static long MultiplierBp(int target, bool over, long rateBp = Mult.Base) => rateBp * Mult.One / Count(target, over);

    public override bool Valid(JsonElement c) =>
        Int(c, "target") is var t && t is >= 0 and <= 9_999 && c.TryGetProperty("over", out var o) && o.ValueKind is JsonValueKind.True or JsonValueKind.False
        && Count(t, o.GetBoolean()) is >= 200 and <= 9_800;

    public override int FloatsNeeded(JsonElement c) => 1;

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var target = Int(c, "target");
        var over = c.GetProperty("over").GetBoolean();
        var roll = Roll(floats[0]);
        var win = over ? roll > target : roll < target;
        return new(win ? MultiplierBp(target, over, rateBp) : 0, new { roll, target, over, win });
    }

    public override object InfoAt(long rateBp) => new { minChance = 200, maxChance = 9_800, rateBp };
}

/// <summary>Limbo: pick a target from 1.01× to 5,000×; the round's result has P(result ≥ x) = r / x at the game's rate r (Crash's draw); a result at or above the target pays the target.</summary>
public sealed class Limbo : OneShotGame
{
    public const string GameId = "limbo";

    public override string Id => GameId;

    public override string Name => "Limbo";

    public override bool Valid(JsonElement c) => Long(c, "target") is var t && t is >= Crash.MinAutoBp and <= Mult.CapBp && t % 100 == 0;

    public override int FloatsNeeded(JsonElement c) => 1;

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var target = Long(c, "target");
        var result = Crash.PointBp(floats[0], rateBp);
        return new(result >= target ? target : 0, new { result, target, win = result >= target });
    }

    public override object InfoAt(long rateBp) => new { minTarget = Crash.MinAutoBp, maxTarget = Mult.CapBp, rateBp };
}

/// <summary>Peg Drop: a ball falls through 8, 12 or 16 rows of pegs, bouncing left or right with equal chance; it lands in bucket k (the number of right bounces) and pays that bucket of the chosen risk's table.</summary>
public sealed class PegDrop : OneShotGame
{
    public const string GameId = "peg-drop";
    public static readonly int[] Rows = [8, 12, 16];
    public static readonly string[] Risks = ["low", "medium", "high"];

    public override string Id => GameId;

    public override string Name => "Peg Drop";

    public override bool Valid(JsonElement c) =>
        Array.IndexOf(Rows, Int(c, "rows")) >= 0 && c.TryGetProperty("risk", out var r) && r.ValueKind == JsonValueKind.String && Array.IndexOf(Risks, r.GetString()) >= 0;

    public override int FloatsNeeded(JsonElement c) => Int(c, "rows");

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var rows = Int(c, "rows");
        var risk = c.GetProperty("risk").GetString()!;
        var path = floats.Take(rows).Select(f => f < 0.5 ? 0 : 1).ToArray();
        var bucket = path.Sum();
        return new(Mult.Rated(InstantTables.PegDrop[(rows, risk)][bucket], rateBp), new { rows, risk, path, bucket });
    }

    public override object InfoAt(long rateBp) => new { rows = Rows, risks = Risks, tables = InstantTables.PegDrop.ToDictionary(t => $"{t.Key.Rows}-{t.Key.Risk}", t => Rated(t.Value, rateBp)) };
}

/// <summary>Keno: 40 numbers; the player picks 1–10; 10 are drawn (a Fisher–Yates shuffle by the round's floats); pays by picks and hits.</summary>
public sealed class Keno : OneShotGame
{
    public const string GameId = "keno";
    public const int Numbers = 40;
    public const int Drawn = 10;

    public override string Id => GameId;

    public override string Name => "Keno";

    public static int[] Picks(JsonElement c) =>
        c.ValueKind == JsonValueKind.Object && c.TryGetProperty("picks", out var p) && p.ValueKind == JsonValueKind.Array
            ? [.. p.EnumerateArray().Select(x => x.TryGetInt32(out var i) ? i : -1)] : [];

    public override bool Valid(JsonElement c)
    {
        var picks = Picks(c);
        return picks.Length is >= 1 and <= 10 && picks.All(n => n is >= 1 and <= Numbers) && picks.Distinct().Count() == picks.Length;
    }

    public override int FloatsNeeded(JsonElement c) => Numbers - 1;

    public static int[] Draw(IReadOnlyList<double> floats)
    {
        var balls = Enumerable.Range(1, Numbers).ToArray();
        for (var i = Numbers - 1; i > 0; i--)
        {
            var j = (int)(floats[Numbers - 1 - i] * (i + 1));
            (balls[i], balls[j]) = (balls[j], balls[i]);
        }

        return [.. balls.Take(Drawn)];
    }

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var picks = Picks(c);
        var drawn = Draw(floats);
        var hits = picks.Count(drawn.Contains);
        return new(Mult.Rated(InstantTables.Keno[picks.Length][hits], rateBp), new { picks, drawn, hits });
    }

    public override object InfoAt(long rateBp) => new { numbers = Numbers, drawn = Drawn, tables = InstantTables.Keno.Select(t => Rated(t, rateBp)).ToArray() };
}
