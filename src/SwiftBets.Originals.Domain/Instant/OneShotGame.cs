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

    public virtual long[] Stakes => InstantStakes.Offered;


    public abstract bool Valid(JsonElement c);

    public abstract int FloatsNeeded(JsonElement c);

    /// <summary>The round's result at the game's rate (basis points; <see cref="Multipliers.Base"/> is the 96% the maths was designed at).</summary>
    public abstract OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp);

    public OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c) => Resolve(floats, c, Multipliers.Base);

    /// <summary>What the rules sheet shows (tables, ranges), with every pay at the game's rate.</summary>
    public abstract object InfoAt(long rateBp);

    public object Info => InfoAt(Multipliers.Base);

    /// <summary>A table of pays designed at <see cref="Multipliers.Base"/>, at the game's rate.</summary>
    protected static long[] Rated(IEnumerable<long> table, long rateBp) => [.. table.Select(bp => Multipliers.Rated(bp, rateBp))];

    protected static int Int(JsonElement e, string name, int fallback = int.MinValue) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : fallback;

    protected static long Long(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt64(out var i) ? i : long.MinValue;
}
