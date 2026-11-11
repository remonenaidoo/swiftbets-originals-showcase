using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Coin Flip: pays r ÷ P(at least <c>need</c> of <c>coins</c> land on the chosen side).</summary>
public sealed class CoinFlip : OneShotGame
{
    public const string GameId = "coin-flip";

    public override string Id => GameId;

    public override string Name => "Coin Flip";

    /// <summary>The number of the 2^coins outcomes with at least <paramref name="need"/> coins on the chosen side.</summary>
    public static long Ways(int coins, int need)
    {
        long ways = 0, choose = 1;
        for (var k = 0; k <= coins; k++)
        {
            if (k >= need)
            {
                ways += choose;
            }

            choose = choose * (coins - k) / (k + 1);
        }

        return ways;
    }

    public static long MultiplierBp(int coins, int need, long rateBp = Multipliers.Base) => (rateBp << coins) / Ways(coins, need);

    public override bool Valid(JsonElement c) =>
        Int(c, "coins") is var n && n is >= 1 and <= 10 && Int(c, "need") is var k && k >= 1 && k <= n
        && c.TryGetProperty("side", out var s) && s.ValueKind == JsonValueKind.String && s.GetString() is "heads" or "tails";

    public override int FloatsNeeded(JsonElement c) => Int(c, "coins");

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var coins = Int(c, "coins");
        var need = Int(c, "need");
        var side = c.GetProperty("side").GetString()!;
        var flips = floats.Take(coins).Select(f => f < 0.5 ? "heads" : "tails").ToArray();
        var landed = flips.Count(f => f == side);
        var win = landed >= need;
        return new(win ? MultiplierBp(coins, need, rateBp) : 0, new { coins, need, side, flips, landed, win });
    }

    public override object InfoAt(long rateBp) => new { maxCoins = 10, rateBp };
}
