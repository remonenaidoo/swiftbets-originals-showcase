using System.Numerics;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>
/// Higher or Lower: cards from an endless deck (each one of 52 with equal chance, Ace low, King high). The player
/// guesses whether the next card is higher or the same, or lower or the same, or skips it. A guess that wins with c
/// ranks of 13 multiplies by 13 ÷ c; the edge is taken once, on the first guess (× the game's rate r), so cashing out
/// after any number of guesses returns r. A guess that cannot lose, or whose win would pass the 5,000× cap, is refused; the round
/// cashes out by itself when no guess is left.
/// </summary>
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

    /// <summary>The multiplier after winning guesses with these winning counts.</summary>
    public static long MultiplierBp(IReadOnlyList<int> counts, long rateBp = Mult.Base) => counts.Count == 0 ? Mult.One : Math.Min(Raw(counts, rateBp), Mult.CapBp);

    public static bool Allowed(IReadOnlyList<int> counts, int rank, int guess, long rateBp = Mult.Base) =>
        guess is Higher or Lower && Count(rank, guess) is var c && c < 13 && Raw([.. counts, c], rateBp) <= Mult.CapBp;

    /// <summary>The multiplier before the cap, exact (13^k outgrows any fixed width long before a run ends); anything past the cap reads as cap + 1.</summary>
    private static long Raw(IReadOnlyList<int> counts, long rateBp)
    {
        BigInteger num = rateBp, den = 1;
        foreach (var c in counts)
        {
            num *= 13;
            den *= c;
        }

        return (long)BigInteger.Min(num / den, Mult.CapBp + 1);
    }
}
