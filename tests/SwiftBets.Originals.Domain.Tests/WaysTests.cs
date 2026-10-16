using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Domain.Tests;

public sealed class WaysTests
{
    private static string[,] Window(params string[][] reels)
    {
        var w = new string[5, 3];
        for (var r = 0; r < 5; r++) for (var row = 0; row < 3; row++) w[r, row] = reels[r][row];
        return w;
    }

    private static readonly string[] Lows = ["Shell", "Starfish", "Compass"];

    [Fact]
    public void A_way_needs_the_symbol_on_every_reel_from_reel_1()
    {
        var gap = WaysSlot.Evaluate(Window(["Shark", "Shell", "Shell"], Lows, ["Shark", "Anchor", "Anchor"], ["Shark", "Anchor", "Anchor"], ["Shark", "Anchor", "Anchor"]), new int[5], false);
        var run = WaysSlot.Evaluate(Window(["Shark", "Shark", "Shell"], ["Shark", "Starfish", "Compass"], ["Wild", "Anchor", "Anchor"], Lows, Lows), new int[5], false);

        gap.Wins.ShouldNotContain(w => w.Symbol == "Shark");
        run.Wins.ShouldContain(w => w.Symbol == "Shark" && w.Reels == 3 && w.Ways == 2 && w.Units == 2 * WaysTables.Pays["Shark"][0]);
    }

    [Fact]
    public void A_rounds_spins_add_up_to_what_it_pays()
    {
        var rng = new Random(7);
        for (var n = 0; n < 2000; n++)
        {
            var floats = Enumerable.Range(0, 85).Select(_ => rng.NextDouble()).ToArray();
            var (spins, units, capped) = WaysSlot.PlayRound(floats);
            if (!capped) units.ShouldBe(spins.Sum(s => s.Units));
        }
    }
}
