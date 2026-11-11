using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Limbo: pick a target from 1.01× to 5,000×; the round's result has P(result ≥ x) = r / x at the game's rate r (Crash's draw); a result at or above the target pays the target.</summary>
public sealed class Limbo : OneShotGame
{
    public const string GameId = "limbo";

    public override string Id => GameId;

    public override string Name => "Limbo";

    public override bool Valid(JsonElement c) => Long(c, "target") is var t && t is >= Crash.MinAutoBp and <= Multipliers.CapBp && t % 100 == 0;

    public override int FloatsNeeded(JsonElement c) => 1;

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var target = Long(c, "target");
        var result = Crash.PointBp(floats[0], rateBp);
        return new(result >= target ? target : 0, new { result, target, win = result >= target });
    }

    public override object InfoAt(long rateBp) => new { minTarget = Crash.MinAutoBp, maxTarget = Multipliers.CapBp, rateBp };
}
