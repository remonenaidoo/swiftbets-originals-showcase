using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Gem Hand: five gems, each one of 7 colours with equal chance, paid like a poker hand.</summary>
public sealed class GemHand : OneShotGame
{
    public const string GameId = "gem-hand";
    public const int Colours = 7;

    /// <summary>Pays by hand in basis points at the design rate (exact RTP 95.96% over the 16,807 equally likely hands).</summary>
    public static readonly IReadOnlyDictionary<string, long> Pays = new Dictionary<string, long>
    {
        ["five"] = 240_000, ["four"] = 50_000, ["full-house"] = 40_000, ["three"] = 25_000, ["two-pair"] = 20_000, ["pair"] = 2_000, ["none"] = 0,
    };

    public override string Id => GameId;

    public override string Name => "Gem Hand";

    public override bool Valid(JsonElement c) => c.ValueKind == JsonValueKind.Object;

    public override int FloatsNeeded(JsonElement c) => 5;

    public static string Classify(IReadOnlyList<int> gems)
    {
        var counts = gems.GroupBy(g => g).Select(g => g.Count()).OrderDescending().ToArray();
        return counts switch
        {
            [5] => "five",
            [4, 1] => "four",
            [3, 2] => "full-house",
            [3, 1, 1] => "three",
            [2, 2, 1] => "two-pair",
            [2, 1, 1, 1] => "pair",
            _ => "none",
        };
    }

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var gems = floats.Take(5).Select(f => (int)(f * Colours)).ToArray();
        var hand = Classify(gems);
        return new(Multipliers.Rated(Pays[hand], rateBp), new { gems, hand });
    }

    public override object InfoAt(long rateBp) => new { colours = Colours, pays = Pays.ToDictionary(p => p.Key, p => Multipliers.Rated(p.Value, rateBp)) };
}
