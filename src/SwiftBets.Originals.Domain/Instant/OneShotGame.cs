using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

public sealed record OneShotResult(long MultiplierBp, object Detail);

/// <summary>An instant game settled in one draw, with no state between rounds.</summary>
public abstract class OneShotGame
{
    public abstract string Id { get; }

    public abstract string Name { get; }

    public virtual long[] Stakes => InstantStakes.Offered;

    public abstract bool Valid(JsonElement c);

    public abstract int FloatsNeeded(JsonElement c);

    public abstract OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp);

    public OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c) => Resolve(floats, c, Multipliers.Base);

    /// <summary>What the rules sheet shows (tables, ranges), with every pay at the game's rate.</summary>
    public abstract object InfoAt(long rateBp);

    public object Info => InfoAt(Multipliers.Base);

    protected static long[] Rated(IEnumerable<long> table, long rateBp) => [.. table.Select(bp => Multipliers.Rated(bp, rateBp))];

    protected static int Int(JsonElement e, string name, int fallback = int.MinValue) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : fallback;

    protected static long Long(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt64(out var i) ? i : long.MinValue;
}
