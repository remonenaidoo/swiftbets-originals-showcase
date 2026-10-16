using SwiftBets.Originals.Domain.Slot;
using SwiftBets.Originals.Games;

namespace SwiftBets.Originals.Domain.Tests;

public sealed class EvaluatorTests
{
    private static readonly GameDefinition Savanna = GildedSavanna.Definition;

    [Fact]
    public void Wilds_complete_five_lions_on_the_middle_line()
    {
        var reels = Fixed(Savanna, "Lion", "Wild", "Wild", "Lion", "Lion");

        var outcome = Evaluator.Evaluate(Savanna, reels, [0, 0, 0, 0, 0], multiplier: 1);

        outcome.Lines.ShouldContain(w => w.Line == 0 && Savanna.Name(w.Symbol) == "Lion" && w.Count == 5 && w.Units == 500);
    }

    [Fact]
    public void The_cap_pays_up_to_the_limit_and_no_more()
    {
        var (paid, reached) = Cap.Apply(paidSoFarUnits: 99_000, winUnits: 5_000, capUnits: 5000 * 20);

        paid.ShouldBe(1_000);
        reached.ShouldBeTrue();
    }

    [Fact]
    public void Split_symbols_count_by_weight_and_pay_as_their_single()
    {
        var game = SplitGame();
        var reels = Fixed(game, "Gold3", "Gold", "Wild2", "Coal", "Coal");

        var outcome = Evaluator.Evaluate(game, reels, [0, 0, 0, 0, 0], multiplier: 1);

        // 3 + 1 + 2 = a count of 6 over three reels: Gold's pay for 6.
        outcome.Lines.ShouldHaveSingleItem().ShouldBe(new LineWin(0, game.Id("Gold"), 6, 3, 60));
    }

    [Fact]
    public void A_high_count_on_fewer_than_three_reels_pays_nothing()
    {
        var game = SplitGame();
        var reels = Fixed(game, "Gold3", "Gold3", "Coal", "Coal", "Gold");

        Evaluator.Evaluate(game, reels, [0, 0, 0, 0, 0], multiplier: 1).Lines.ShouldBeEmpty();
    }

    [Fact]
    public void Locked_cells_override_the_draw_and_new_splits_join_them()
    {
        var game = SplitGame();
        var reels = Fixed(game, "Gold", "Coal", "Gold3", "Coal", "Coal");
        LockedCell[] locks = [new(1, 1, game.Id("Gold3"))];

        var outcome = Evaluator.Evaluate(game, reels, [0, 0, 0, 0, 0], multiplier: 1, locks, locking: true);

        outcome.Window[1, 1].ShouldBe(game.Id("Gold3"));
        outcome.Lines.ShouldContain(w => w.Count == 7 && w.Reels == 3);
        outcome.Locked.ShouldBe([new LockedCell(1, 1, game.Id("Gold3")), new LockedCell(2, 1, game.Id("Gold3"))]);
    }

    /// <summary>Five strips of three whose middle row, at stop 0, is the given symbols; the other rows cannot line up.</summary>
    private static IReadOnlyList<byte>[] Fixed(GameDefinition game, params string[] middle)
    {
        string[] top = game == Savanna ? ["Ten", "Jack", "Queen", "King", "Ace"] : ["Rock", "Coal2", "Rock", "Coal2", "Rock"];
        string[] bottom = game == Savanna ? ["Ace", "King", "Queen", "Jack", "Ten"] : ["Coal2", "Rock", "Coal2", "Rock", "Coal2"];
        return [.. middle.Select((s, reel) => (IReadOnlyList<byte>)[game.Id(top[reel]), game.Id(s), game.Id(bottom[reel])])];
    }

    private static GameDefinition SplitGame()
    {
        SymbolDef[] symbols =
        [
            new("Gold"), new("Gold3", Weight: 3, PaysAs: "Gold"), new("Coal"), new("Coal2", Weight: 1, PaysAs: "Coal"), new("Rock"),
            new("Wild2", SymbolKind.Wild, Weight: 2), new("Bomb", SymbolKind.Scatter),
        ];
        var anyStrip = (IReadOnlyList<byte>)[0];
        return new GameDefinition("split-test", symbols, [anyStrip, anyStrip, anyStrip, anyStrip, anyStrip], [anyStrip, anyStrip, anyStrip, anyStrip, anyStrip],
            [[1, 1, 1, 1, 1]], new Dictionary<byte, int[]> { [0] = [10, 20, 30, 60, 70, 80] }, [1, 5, 25], new FeatureRules(8, 1, Retrigger: false, LockSplits: true), 5000);
    }
}
