using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Sunburst Wheel's wheels, segment by segment clockwise from the top, in basis points; each averages exactly 0.96×.</summary>
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

/// <summary>Sunburst Wheel: a wheel of 10, 20 or 30 equal segments at low, medium or high risk; one float picks the segment, which pays its multiplier.</summary>
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

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c)
    {
        var segments = Int(c, "segments");
        var risk = c.GetProperty("risk").GetString()!;
        var segment = (int)(floats[0] * segments);
        return new(WheelTables.Wheels[(segments, risk)][segment], new { segments, risk, segment });
    }

    public override object Info => new { segments = Segments, risks = Risks, wheels = WheelTables.Wheels.ToDictionary(t => $"{t.Key.Segments}-{t.Key.Risk}", t => t.Value), rtp = 96.00m };
}

/// <summary>
/// Coin Flip: toss 1–10 coins; the player picks a side and the least number of coins that must land on it. With
/// P the chance of at least that many, the pay is 0.96 ÷ P, floored to a basis point. One float per coin.
/// </summary>
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

    public static long MultiplierBp(int coins, int need) => (9_600L << coins) / Ways(coins, need);

    public override bool Valid(JsonElement c) =>
        Int(c, "coins") is var n && n is >= 1 and <= 10 && Int(c, "need") is var k && k >= 1 && k <= n
        && c.TryGetProperty("side", out var s) && s.ValueKind == JsonValueKind.String && s.GetString() is "heads" or "tails";

    public override int FloatsNeeded(JsonElement c) => Int(c, "coins");

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c)
    {
        var coins = Int(c, "coins");
        var need = Int(c, "need");
        var side = c.GetProperty("side").GetString()!;
        var flips = floats.Take(coins).Select(f => f < 0.5 ? "heads" : "tails").ToArray();
        var landed = flips.Count(f => f == side);
        var win = landed >= need;
        return new(win ? MultiplierBp(coins, need) : 0, new { coins, need, side, flips, landed, win });
    }

    public override object Info => new { maxCoins = 10, rtp = 96.00m };
}

/// <summary>
/// Lucky Scratch: a card of 9 cells holding six prize symbols. The first float picks the prize (or none) from fixed
/// odds; the card is then laid out from the others so that the prize, if any, shows exactly three times and no other
/// symbol shows more than twice. Returns exactly 96%.
/// </summary>
public sealed class LuckyScratch : OneShotGame
{
    public const string GameId = "lucky-scratch";
    public const int Floats = 1 + 11 + 8;
    public const int OddsScale = 100_000;

    /// <summary>Each prize's pay and its chance in 100,000, from prize 1 (lowest) to prize 6.</summary>
    public static readonly (long PayBp, int Chance)[] Prizes = [(10_000, 18_000), (20_000, 10_000), (50_000, 4_000), (100_000, 2_000), (500_000, 200), (10_000_000, 8)];

    public override string Id => GameId;

    public override string Name => "Lucky Scratch";

    public override bool Valid(JsonElement c) => c.ValueKind == JsonValueKind.Object;

    public override int FloatsNeeded(JsonElement c) => Floats;

    /// <summary>The prize the first float lands on, 1–6, or 0 for none.</summary>
    public static int Prize(double f)
    {
        var u = (int)(f * OddsScale);
        for (var i = 0; i < Prizes.Length; i++)
        {
            if (u < Prizes[i].Chance)
            {
                return i + 1;
            }

            u -= Prizes[i].Chance;
        }

        return 0;
    }

    /// <summary>The 9 cells for prize <paramref name="prize"/> (0 = none), from floats 1–19.</summary>
    public static int[] Card(int prize, IReadOnlyList<double> floats)
    {
        // Every other symbol twice; shuffle them and take what the card still needs, so none reaches three.
        var others = Enumerable.Range(1, 6).Where(s => s != prize).SelectMany(s => new[] { s, s }).ToArray();
        Shuffle(others, floats, 1);
        var cells = prize == 0 ? others.Take(9).ToArray() : [prize, prize, prize, .. others.Take(6)];
        Shuffle(cells, floats, 12);
        return cells;
    }

    private static void Shuffle(int[] a, IReadOnlyList<double> floats, int from)
    {
        for (var i = a.Length - 1; i > 0; i--)
        {
            var j = (int)(floats[from + (a.Length - 1 - i)] * (i + 1));
            (a[i], a[j]) = (a[j], a[i]);
        }
    }

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c)
    {
        var prize = Prize(floats[0]);
        var cells = Card(prize, floats);
        return new(prize == 0 ? 0 : Prizes[prize - 1].PayBp, new { prize, cells });
    }

    public override object Info => new { prizes = Prizes.Select(p => new { payBp = p.PayBp, chance = p.Chance }), oddsScale = OddsScale, rtp = 96.00m };
}

/// <summary>Gem Hand: five gems, each one of 7 colours with equal chance, paid like a poker hand.</summary>
public sealed class GemHand : OneShotGame
{
    public const string GameId = "gem-hand";
    public const int Colours = 7;

    /// <summary>Pays by hand in basis points (exact RTP 95.96% over the 16,807 equally likely hands).</summary>
    public static readonly IReadOnlyDictionary<string, long> Pays = new Dictionary<string, long>
    {
        ["five"] = 240_000, ["four"] = 50_000, ["full-house"] = 40_000, ["three"] = 25_000, ["two-pair"] = 20_000, ["pair"] = 2_000, ["none"] = 0,
    };

    public override string Id => GameId;

    public override string Name => "Gem Hand";

    public override decimal Rtp => 95.96m;

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

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c)
    {
        var gems = floats.Take(5).Select(f => (int)(f * Colours)).ToArray();
        var hand = Classify(gems);
        return new(Pays[hand], new { gems, hand });
    }

    public override object Info => new { colours = Colours, pays = Pays, rtp = 95.96m };
}

/// <summary>
/// Baccarat (punto banco) with the standard drawing rules, from an endless shoe: each card is one of 52 with equal
/// chance (six floats, dealt Player, Banker, Player, Banker, then each side's third card). Player pays 1:1, Banker
/// 0.95:1, Tie 8:1; a tie returns Player and Banker bets.
/// </summary>
public sealed class Baccarat : OneShotGame
{
    public const string GameId = "baccarat";
    public static readonly IReadOnlyDictionary<string, long> WinBp = new Dictionary<string, long> { ["player"] = 20_000, ["banker"] = 19_500, ["tie"] = 90_000 };

    public override string Id => GameId;

    public override string Name => "Baccarat";

    /// <summary>The best bet's (Banker); each bet's own is in the rules.</summary>
    public override decimal Rtp => 98.94m;

    public override bool Valid(JsonElement c) => c.ValueKind == JsonValueKind.Object && c.TryGetProperty("bet", out var b) && b.ValueKind == JsonValueKind.String && WinBp.ContainsKey(b.GetString()!);

    public override int FloatsNeeded(JsonElement c) => 6;

    public static int Value(int card) => card % 13 + 1 is var rank && rank >= 10 ? 0 : rank;

    private static bool BankerDraws(int banker, int? third) => third switch
    {
        null => banker <= 5,
        _ when banker <= 2 => true,
        8 when banker == 3 => false,
        _ when banker == 3 => true,
        >= 2 and <= 7 when banker == 4 => true,
        >= 4 and <= 7 when banker == 5 => true,
        >= 6 and <= 7 when banker == 6 => true,
        _ => false,
    };

    /// <summary>The cards each side ends with (0–51) and the winner.</summary>
    public static (int[] Player, int[] Banker, string Winner) Deal(IReadOnlyList<double> floats)
    {
        var cards = floats.Take(6).Select(f => (int)(f * 52)).ToArray();
        List<int> player = [cards[0], cards[2]], banker = [cards[1], cards[3]];
        int Total(List<int> hand) => hand.Sum(Value) % 10;
        if (Total(player) < 8 && Total(banker) < 8)
        {
            int? third = null;
            if (Total(player) <= 5)
            {
                player.Add(cards[4]);
                third = Value(cards[4]);
            }

            if (BankerDraws(Total(banker), third))
            {
                banker.Add(cards[5]);
            }
        }

        var (p, b) = (Total(player), Total(banker));
        return ([.. player], [.. banker], p > b ? "player" : b > p ? "banker" : "tie");
    }

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c)
    {
        var bet = c.GetProperty("bet").GetString()!;
        var (player, banker, winner) = Deal(floats);
        var bp = winner == bet ? WinBp[bet] : winner == "tie" ? Mult.One : 0;
        return new(bp, new { bet, player, banker, winner, playerTotal = player.Sum(Value) % 10, bankerTotal = banker.Sum(Value) % 10 });
    }

    public override object Info => new { pays = WinBp, rtp = new { player = 98.77m, banker = 98.94m, tie = 85.88m } };
}
