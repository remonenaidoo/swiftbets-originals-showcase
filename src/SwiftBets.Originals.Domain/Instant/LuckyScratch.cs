using System.Text.Json;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Lucky Scratch: the first float picks the prize; the card shows it exactly three times and nothing else more than twice.</summary>
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

    public override OneShotResult Resolve(IReadOnlyList<double> floats, JsonElement c, long rateBp)
    {
        var prize = Prize(floats[0]);
        var cells = Card(prize, floats);
        return new(prize == 0 ? 0 : Multipliers.Rated(Prizes[prize - 1].PayBp, rateBp), new { prize, cells });
    }

    public override object InfoAt(long rateBp) => new { prizes = Prizes.Select(p => new { payBp = Multipliers.Rated(p.PayBp, rateBp), chance = p.Chance }), oddsScale = OddsScale };
}
