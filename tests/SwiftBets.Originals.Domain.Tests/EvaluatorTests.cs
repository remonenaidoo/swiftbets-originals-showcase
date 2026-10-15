using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Domain.Tests;

public sealed class EvaluatorTests
{
    [Fact]
    public void Wilds_complete_five_lions_on_the_middle_line()
    {
        var reels = Fixed(Symbol.Lion, Symbol.Wild, Symbol.Wild, Symbol.Lion, Symbol.Lion);

        var outcome = Evaluator.Evaluate(GildedSavanna.Definition, reels, [0, 0, 0, 0, 0], multiplier: 1);

        outcome.Lines.ShouldContain(w => w.Line == 0 && w.Symbol == Symbol.Lion && w.Count == 5 && w.Units == 500);
    }

    [Fact]
    public void The_cap_pays_up_to_the_limit_and_no_more()
    {
        var (paid, reached) = Cap.Apply(paidSoFarUnits: 99_000, winUnits: 5_000, capUnits: 5000 * 20);

        paid.ShouldBe(1_000);
        reached.ShouldBeTrue();
    }

    /// <summary>Five strips of three whose middle row, at stop 0, is the given symbols; the other rows cannot line up.</summary>
    private static Symbol[][] Fixed(params Symbol[] middle)
    {
        Symbol[] top = [Symbol.Ten, Symbol.Jack, Symbol.Queen, Symbol.King, Symbol.Ace];
        Symbol[] bottom = [Symbol.Ace, Symbol.King, Symbol.Queen, Symbol.Jack, Symbol.Ten];
        return [.. middle.Select((s, reel) => new[] { top[reel], s, bottom[reel] })];
    }
}
