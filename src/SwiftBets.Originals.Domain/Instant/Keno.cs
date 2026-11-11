using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

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
        return new(Multipliers.Rated(InstantTables.Keno[picks.Length][hits], rateBp), new { picks, drawn, hits });
    }

    public override object InfoAt(long rateBp) => new { numbers = Numbers, drawn = Drawn, tables = InstantTables.Keno.Select(t => Rated(t, rateBp)).ToArray() };
}
