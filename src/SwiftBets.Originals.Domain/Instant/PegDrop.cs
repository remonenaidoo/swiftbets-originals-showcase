using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

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
        return new(Multipliers.Rated(InstantTables.PegDrop[(rows, risk)][bucket], rateBp), new { rows, risk, path, bucket });
    }

    public override object InfoAt(long rateBp) => new { rows = Rows, risks = Risks, tables = InstantTables.PegDrop.ToDictionary(t => $"{t.Key.Rows}-{t.Key.Risk}", t => Rated(t.Value, rateBp)) };
}
