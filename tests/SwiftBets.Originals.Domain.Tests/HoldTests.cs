using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Domain.Tests;

public sealed class HoldTests
{
    /// <summary>Floats for a round whose base spin lands a full stack of coins on every reel; respin floats as given.</summary>
    private static double[] Coins(Func<int, double> respin)
    {
        var f = new double[HoldSlot.Floats];
        for (var r = 0; r < 5; r++)
        {
            var strip = HoldTables.Reels[r];
            var stop = Enumerable.Range(0, strip.Length).First(s => Enumerable.Range(0, 3).All(row => strip[(s + row) % strip.Length] == "Coin"));
            f[r] = (stop + 0.5) / strip.Length;
        }

        for (var i = 5; i < 20; i++) f[i] = 0.1;
        for (var i = 20; i < f.Length; i++) f[i] = respin(i);
        return f;
    }

    [Fact]
    public void A_new_coin_resets_the_respins_to_three()
    {
        // Six coins (reels 1 and 2), nine empty cells.
        var board = new Dictionary<(int, int), int> { [(0, 0)] = 1, [(0, 1)] = 1, [(0, 2)] = 1, [(1, 0)] = 1, [(1, 1)] = 1, [(1, 2)] = 1 };
        var floats = new double[200];
        Array.Fill(floats, 0.99);
        floats[0] = 0.0; // the first empty cell lands a coin in the first respin
        floats[1] = 0.1;
        var at = 0;

        var (respins, final) = HoldSlot.Hold(board, floats, ref at);

        respins[0].Added.Count.ShouldBe(1);
        respins[0].Left.ShouldBe(3);
        respins.Skip(1).Select(r => r.Left).ShouldBe([2, 1, 0]);
        final.Count.ShouldBe(7);
    }

    [Fact]
    public void A_full_board_pays_the_grand()
    {
        var round = HoldSlot.PlayRound(Coins(_ => 0.0)); // every empty cell lands a coin (value float 0.0 = 1 stake)

        round.Grand.ShouldBeTrue();
        // Every coin is worth 1 stake here (the value floats are under the first weight).
        round.FeatureUnits.ShouldBe((15 + HoldTables.Grand) * (long)HoldSlot.Units);
    }
}
