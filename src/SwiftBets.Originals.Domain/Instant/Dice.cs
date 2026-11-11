using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Dice: c winning rolls of 10,000 pay r × 10,000 / c.</summary>
public sealed class Dice : OneShotGame
{
    public const string GameId = "dice";

    public override string Id => GameId;

    public override string Name => "Dice";

    public static int Roll(double f) => (int)(f * 10_000);

    /// <summary>Winning outcomes: over wins on a roll above the target, under on a roll below it.</summary>
    public static int Count(int target, bool over) => over ? 9_999 - target : target;

    public static long MultiplierBp(int target, bool over, long rateBp = Multipliers.Base) => rateBp * Multipliers.One / Count(target, over);

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
