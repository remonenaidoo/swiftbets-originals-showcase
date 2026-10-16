using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Domain.Tests;

public sealed class CascadeTests
{
    private static IEnumerable<GemSpin> Spins(int rounds)
    {
        var rng = new Random(11);
        for (var n = 0; n < rounds; n++)
        {
            var floats = Enumerable.Range(0, CascadeSlot.Floats).Select(_ => rng.NextDouble()).ToArray();
            foreach (var s in CascadeSlot.PlayRound(CascadeSlot.Production, floats).Spins) yield return s;
        }
    }

    [Fact]
    public void A_tumble_refills_each_reel_from_its_strip_above_the_board()
    {
        var checkedSteps = 0;
        foreach (var spin in Spins(3000).Where(s => s.Steps.Count > 0))
        {
            var strips = spin.Free ? CascadeSlot.Production.FreeStrips : CascadeSlot.Production.BaseStrips;
            var pos = (int[])spin.Stops.Clone();
            var before = spin.Board;
            foreach (var step in spin.Steps)
            {
                var removed = step.Wins.SelectMany(w => w.Cells).Select(c => (c[0], c[1])).ToHashSet();
                for (var r = 0; r < CascadeSlot.Reels; r++)
                {
                    var strip = strips[r];
                    var survivors = before[r].Where((_, row) => !removed.Contains((r, row))).Select(c => c.Symbol).ToList();
                    var k = CascadeSlot.Rows - survivors.Count;
                    pos[r] = ((pos[r] - k) % strip.Length + strip.Length) % strip.Length;
                    var expected = Enumerable.Range(0, k).Select(i => strip[(pos[r] + i) % strip.Length]).Concat(survivors);
                    step.Board[r].Select(c => c.Symbol).ShouldBe(expected);
                }

                before = step.Board;
                checkedSteps++;
            }
        }

        checkedSteps.ShouldBeGreaterThan(100);
    }

    [Fact]
    public void A_tumble_sequence_ends_only_when_nothing_pays()
    {
        foreach (var spin in Spins(3000))
        {
            var last = spin.Steps.Count > 0 ? spin.Steps[^1].Board : spin.Board;
            CascadeSlot.Wins(CascadeSlot.Production, last).ShouldBeEmpty();
        }
    }
}
