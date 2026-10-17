using SwiftBets.Originals.Application.Instant;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Tests;

public sealed class TowersTests : IDisposable
{
    private readonly FakeStore store = new();
    private readonly FakeWallet wallet = new();
    private readonly FakeSeeds seeds = new();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    public void Dispose() => store.Dispose();

    private TowersHandler Handler() => new(store, wallet, seeds, TimeProvider.System);

    /// <summary>The fake seeds' first round (nonce 0) on easy: the bad tile of each row.</summary>
    private static int[][] Bad() => Towers.Layout(Fair.Floats("server-seed", "client", 0, Towers.FloatsNeeded), "easy");

    private static int Safe(int row) => Enumerable.Range(0, Towers.Levels["easy"].Tiles).Except(Bad()[row]).First();

    [Fact]
    public async Task A_safe_climb_then_cashout_pays_the_row_reached()
    {
        var start = (await Handler().StartAsync(new TowersStart("s", 1000, "easy", "k1"), ct)).Value!;
        await Handler().PickAsync("s", start.RoundId, Safe(0), ct);
        await Handler().PickAsync("s", start.RoundId, Safe(1), ct);
        var cash = (await Handler().CashoutAsync("s", start.RoundId, ct)).Value!;

        cash.Win.ShouldBe(Mult.Pay(1000, Towers.MultiplierBp("easy", 2)));
        wallet.Balance.ShouldBe(100_000 - 1000 + cash.Win);
    }

    [Fact]
    public async Task A_pick_after_a_fall_is_refused()
    {
        var start = (await Handler().StartAsync(new TowersStart("s", 1000, "easy", "k1"), ct)).Value!;
        var fall = (await Handler().PickAsync("s", start.RoundId, Bad()[0][0], ct)).Value!;
        var again = await Handler().PickAsync("s", start.RoundId, Safe(1), ct);

        fall.Status.ShouldBe("busted");
        again.Error.ShouldBe(SpinError.RoundOver);
        wallet.Balance.ShouldBe(100_000 - 1000);
    }
}
