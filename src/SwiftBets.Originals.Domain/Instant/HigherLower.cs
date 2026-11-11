using System.Numerics;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Higher or Lower: a win on c ranks of 13 pays ×13/c and the edge is taken once, so cashing out at any point returns r.</summary>
public static class HigherLower
{
    public const string GameId = "higher-lower";
    public const int Cards = 52;
    public const int Skip = 0;
    public const int Higher = 1;
    public const int Lower = 2;

    public static int Rank(int card) => (card % 13) + 1;

    /// <summary>The ranks of 13 that win the guess on a card of <paramref name="rank"/>.</summary>
    public static int Count(int rank, int guess) => guess == Higher ? 14 - rank : rank;

    public static bool Wins(int rank, int next, int guess) => guess == Higher ? next >= rank : next <= rank;

    public static int[] Deal(IReadOnlyList<double> floats) => [.. floats.Take(Cards).Select(f => (int)(f * Cards))];

    public static long MultiplierBp(IReadOnlyList<int> counts, long rateBp = Multipliers.Base) => counts.Count == 0 ? Multipliers.One : Math.Min(Raw(counts, rateBp), Multipliers.CapBp);

    public static bool Allowed(IReadOnlyList<int> counts, int rank, int guess, long rateBp = Multipliers.Base) =>
        guess is Higher or Lower && Count(rank, guess) is var c && c < 13 && Raw([.. counts, c], rateBp) <= Multipliers.CapBp;

    /// <summary>Exact (13^k outgrows any fixed width); anything past the cap reads as cap + 1.</summary>
    private static long Raw(IReadOnlyList<int> counts, long rateBp)
    {
        BigInteger num = rateBp, den = 1;
        foreach (var c in counts)
        {
            num *= 13;
            den *= c;
        }

        return (long)BigInteger.Min(num / den, Multipliers.CapBp + 1);
    }
}
