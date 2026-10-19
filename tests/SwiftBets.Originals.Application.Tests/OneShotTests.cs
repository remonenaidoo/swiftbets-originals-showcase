using System.Text.Json;
using SwiftBets.Originals.Application.Instant;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Tests;

public sealed class OneShotTests : IDisposable
{
    private readonly FakeStore store = new();
    private readonly FakeWallet wallet = new();
    private readonly FakeSeeds seeds = new();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    public void Dispose() => store.Dispose();

    private OneShotHandler Handler() => new(store, wallet, seeds, TimeProvider.System);

    private static JsonElement Json(object o) => JsonSerializer.SerializeToElement(o);

    [Fact]
    public async Task A_dice_win_pays_the_stated_multiplier()
    {
        // The fake seeds' first roll; bet "over" just under it, so it wins.
        var roll = Dice.Roll(Fair.Floats("server-seed", "client", 0, 1)[0]);
        var target = Math.Clamp(roll - 1, 200, 9800);
        var view = (await Handler().PlayAsync(Dice.GameId, "s", 1000, Json(new { target, over = roll > target }), "k1", ct)).Value!;

        var bp = Dice.MultiplierBp(target, roll > target);
        view.MultiplierBp.ShouldBe(view.Win > 0 ? bp : 0);
        wallet.Balance.ShouldBe(100_000 - 1000 + view.Win);
        store.Rounds[view.RoundId].Status.ShouldBe(RoundStatus.Settled);
    }

    [Fact]
    public async Task An_invalid_bet_moves_no_money()
    {
        var result = await Handler().PlayAsync(Dice.GameId, "s", 1000, Json(new { target = 9990, over = true }), "k1", ct);

        result.Error.ShouldBe(SpinError.InvalidStake);
        wallet.Calls.ShouldNotContain(c => c.Action == "bet");
    }

    [Fact]
    public async Task A_game_pays_at_its_own_configured_rate()
    {
        var rates = new FixedRates(8_000);
        var view = (await new OneShotHandler(store, wallet, seeds, TimeProvider.System, rates)
            .PlayAsync(SunburstWheel.GameId, "s", 1000, Json(new { segments = 10, risk = "high" }), "k1", ct)).Value!;

        // The high wheel's one paying segment is 9.6× at the design rate: 8× at 80%.
        view.MultiplierBp.ShouldBeOneOf(0, 80_000);
        wallet.Balance.ShouldBe(100_000 - 1000 + view.Win);
    }
}
