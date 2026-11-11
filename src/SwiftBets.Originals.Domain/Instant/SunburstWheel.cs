using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Sunburst Wheel's wheels, segment by segment clockwise from the top, in basis points at the design rate; each averages exactly 0.96×.</summary>
public static class WheelTables
{
    public static readonly IReadOnlyDictionary<(int Segments, string Risk), long[]> Wheels = new Dictionary<(int, string), long[]>
    {
        [(10, "low")] = [12000, 12000, 12000, 0, 12000, 12000, 12000, 0, 12000, 12000],
        [(20, "low")] = [21000, 12000, 0, 15000, 12000, 12000, 0, 12000, 12000, 12000, 0, 12000, 12000, 12000, 0, 12000, 12000, 12000, 0, 12000],
        [(30, "low")] = [18000, 12000, 0, 15000, 12000, 15000, 0, 12000, 12000, 12000, 12000, 0, 12000, 12000, 12000, 0, 12000, 12000, 12000, 0, 12000, 12000, 12000, 12000, 0, 12000, 12000, 12000, 0, 12000],
        [(10, "medium")] = [31000, 0, 15000, 0, 20000, 0, 15000, 0, 15000, 0],
        [(20, "medium")] = [32000, 0, 15000, 0, 30000, 0, 15000, 0, 20000, 0, 15000, 0, 20000, 0, 15000, 0, 15000, 0, 15000, 0],
        [(30, "medium")] = [63000, 0, 15000, 0, 30000, 0, 15000, 0, 0, 20000, 0, 15000, 0, 20000, 0, 15000, 0, 20000, 0, 15000, 0, 15000, 0, 0, 15000, 0, 15000, 0, 15000, 0],
        [(10, "high")] = [96000, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [(20, "high")] = [192000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [(30, "high")] = [288000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    };
}

public sealed class SunburstWheel : OneShotGame
{
    public const string GameId = "sunburst-wheel";
    public static readonly int[] Segments = [10, 20, 30];
    public static readonly string[] Risks = ["low", "medium", "high"];

    public override string Id => GameId;

    public override string Name => "Sunburst Wheel";

    public override bool Valid(JsonElement c) =>
        Array.IndexOf(Segments, Int(c, "segments")) >= 0 && c.TryGetProperty("risk", out var r) && r.ValueKind == JsonValueKind.String && Array.IndexOf(Risks, r.GetString()) >= 0;

    public override int FloatsNeeded(JsonElement c) => 1;

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var segments = Int(c, "segments");
        var risk = c.GetProperty("risk").GetString()!;
        var segment = (int)(floats[0] * segments);
        return new(Multipliers.Rated(WheelTables.Wheels[(segments, risk)][segment], rateBp), new { segments, risk, segment });
    }

    public override object InfoAt(long rateBp) => new { segments = Segments, risks = Risks, wheels = WheelTables.Wheels.ToDictionary(t => $"{t.Key.Segments}-{t.Key.Risk}", t => Rated(t.Value, rateBp)) };
}
