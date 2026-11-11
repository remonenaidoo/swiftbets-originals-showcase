using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Punto banco from an endless shoe; each bet's win pays what makes it return rate r, never more than the standard pay.</summary>
public sealed class Baccarat : OneShotGame
{
    public const string GameId = "baccarat";
    /// <summary>The standard pays, the most a win ever returns.</summary>
    public static readonly IReadOnlyDictionary<string, long> StandardBp = new Dictionary<string, long> { ["player"] = 20_000, ["banker"] = 19_500, ["tie"] = 90_000 };

    /// <summary>Of the 13^6 equally likely rank deals, how many each side wins (counted through <see cref="Deal"/>; the maths tool re-counts them).</summary>
    public const long Deals = 4_826_809;
    public static readonly IReadOnlyDictionary<string, long> Wins = new Dictionary<string, long> { ["player"] = 2_153_464, ["banker"] = 2_212_744, ["tie"] = 460_601 };

    /// <summary>A winning bet's pay at rate r: (r × deals − ties returned) ÷ wins, floored, at most the standard pay.</summary>
    public static long WinBp(string bet, long rateBp)
    {
        var pushes = bet == "tie" ? 0 : Wins["tie"];
        return Math.Min(StandardBp[bet], ((rateBp * Deals) - (pushes * Multipliers.One)) / Wins[bet]);
    }

    public override string Id => GameId;

    public override string Name => "Baccarat";

    public override bool Valid(JsonElement c) => c.ValueKind == JsonValueKind.Object && c.TryGetProperty("bet", out var b) && b.ValueKind == JsonValueKind.String && StandardBp.ContainsKey(b.GetString()!);

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

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var bet = c.GetProperty("bet").GetString()!;
        var (player, banker, winner) = Deal(floats);
        var bp = winner == bet ? WinBp(bet, rateBp) : winner == "tie" ? Multipliers.One : 0;
        return new(bp, new { bet, player, banker, winner, playerTotal = player.Sum(Value) % 10, bankerTotal = banker.Sum(Value) % 10 });
    }

    public override object InfoAt(long rateBp) => new { pays = StandardBp.ToDictionary(b => b.Key, b => WinBp(b.Key, rateBp)) };
}
