using System.Text.Json;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Domain.Tests;

public sealed class MoreGamesTests
{
    private static JsonElement Json(object o) => JsonSerializer.SerializeToElement(o);

    /// <summary>The float that deals card <paramref name="card"/> (0–51: rank = card % 13 + 1, Ace low).</summary>
    private static double Card(int card) => (card + 0.5) / 52;

    [Fact]
    public void Every_wheel_returns_96_percent()
    {
        foreach (var ((segments, _), table) in WheelTables.Wheels)
        {
            table.Length.ShouldBe(segments);
            (table.Sum() / segments).ShouldBe(9_600L);
        }
    }

    [Fact]
    public void A_wheel_of_an_unknown_size_is_refused() =>
        new SunburstWheel().Valid(Json(new { segments = 12, risk = "low" })).ShouldBeFalse();

    [Fact]
    public void One_coin_pays_1_92_and_ten_of_ten_pays_983()
    {
        CoinFlip.MultiplierBp(1, 1).ShouldBe(19_200);
        CoinFlip.MultiplierBp(10, 10).ShouldBe(9_830_400);
    }

    [Fact]
    public void Needing_more_heads_than_coins_is_refused() =>
        new CoinFlip().Valid(Json(new { coins = 3, need = 4, side = "heads" })).ShouldBeFalse();

    [Fact]
    public void A_winning_scratch_card_shows_its_prize_three_times_and_no_other_triple()
    {
        var floats = Enumerable.Range(0, LuckyScratch.Floats).Select(i => (i * 0.137) % 1).ToArray();
        floats[0] = 0.0001; // the first band: prize 1
        var cells = LuckyScratch.Card(1, floats);

        cells.Count(c => c == 1).ShouldBe(3);
        cells.GroupBy(c => c).Where(g => g.Key != 1).ShouldAllBe(g => g.Count() <= 2);
    }

    [Fact]
    public void A_losing_scratch_card_has_no_triple()
    {
        var floats = Enumerable.Range(0, LuckyScratch.Floats).Select(i => (i * 0.291) % 1).ToArray();
        floats[0] = 0.9999;
        var result = new LuckyScratch().Resolve(floats, Json(new { }));

        result.MultiplierBp.ShouldBe(0);
        LuckyScratch.Card(0, floats).GroupBy(c => c).ShouldAllBe(g => g.Count() <= 2);
    }

    [Fact]
    public void A_full_house_of_gems_pays_four_times()
    {
        GemHand.Classify([1, 1, 1, 2, 2]).ShouldBe("full-house");
        GemHand.Pays["full-house"].ShouldBe(40_000);
    }

    [Fact]
    public void Five_different_gems_pay_nothing() => GemHand.Classify([0, 1, 2, 3, 4]).ShouldBe("none");

    [Fact]
    public void A_natural_nine_wins_the_player_bet_at_even_money()
    {
        // Player 4 + 5 = 9 (a natural), Banker King + 7 = 7: no draws, Player wins.
        double[] floats = [Card(3), Card(12), Card(4), Card(6), Card(0), Card(0)];
        var result = new Baccarat().Resolve(floats, Json(new { bet = "player" }));

        result.MultiplierBp.ShouldBe(20_000);
    }

    [Fact]
    public void A_tie_returns_a_banker_bet()
    {
        // Player 8 (Ace + 7), Banker 8 (3 + 5): both natural, a tie.
        double[] floats = [Card(0), Card(2), Card(6), Card(4), Card(0), Card(0)];
        var result = new Baccarat().Resolve(floats, Json(new { bet = "banker" }));

        result.MultiplierBp.ShouldBe(Mult.One);
    }

    [Fact]
    public void The_first_guess_takes_the_edge_and_later_ones_pay_the_odds()
    {
        // A 7: "higher or same" wins on 7–King, 7 ranks of 13.
        HigherLower.Count(7, HigherLower.Higher).ShouldBe(7);
        HigherLower.MultiplierBp([7]).ShouldBe(9_600L * 13 / 7);
        HigherLower.MultiplierBp([7, 13]).ShouldBe(9_600L * 13 / 7);
    }

    [Fact]
    public void A_guess_that_cannot_lose_is_refused()
    {
        HigherLower.Allowed([], 1, HigherLower.Higher).ShouldBeFalse(); // anything is "higher or same" than an Ace
        HigherLower.Allowed([], 1, HigherLower.Lower).ShouldBeTrue();
    }
}
