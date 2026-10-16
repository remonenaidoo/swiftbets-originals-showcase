using SwiftBets.Originals.Application.Instant;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Tests;

public sealed class MinesTests : IDisposable
{
    private readonly FakeStore store = new();
    private readonly FakeWallet wallet = new();
    private readonly FakeSeeds seeds = new();

    public void Dispose() => store.Dispose();

    private MinesHandler Handler() => new(store, wallet, seeds, TimeProvider.System);

    /// <summary>The fake seeds' first round (nonce 0) with 3 mines: where the mines and the safe tiles are.</summary>
    private static int[] MineTiles() => Mines.Layout(Fair.Floats("server-seed", "client", 0, Mines.Tiles), 3);

    private static int SafeTile(int skip = 0) => Enumerable.Range(0, Mines.Tiles).Except(MineTiles()).ElementAt(skip);

    [Fact]
    public async Task A_safe_pick_raises_the_multiplier_and_cashout_pays_it()
    {
        var start = (await Handler().StartAsync(new MinesStart("s", 1000, 3, "k1"), TestContext.Current.CancellationToken)).Value!;
        var pick = (await Handler().PickAsync("s", start.RoundId, SafeTile(), TestContext.Current.CancellationToken)).Value!;
        var cash = (await Handler().CashoutAsync("s", start.RoundId, TestContext.Current.CancellationToken)).Value!;

        pick.MultiplierBp.ShouldBe(Mines.MultiplierBp(3, 1));
        pick.MultiplierBp.ShouldBeGreaterThan(Mult.One);
        cash.Win.ShouldBe(Mult.Pay(1000, Mines.MultiplierBp(3, 1)));
        cash.Status.ShouldBe("cashed");
        cash.MineTiles.ShouldBe(MineTiles());
        wallet.Balance.ShouldBe(100_000 - 1000 + cash.Win);
        store.Rounds[start.RoundId].Status.ShouldBe(RoundStatus.Settled);
    }

    [Fact]
    public async Task A_pick_after_a_mine_is_refused()
    {
        var start = (await Handler().StartAsync(new MinesStart("s", 1000, 3, "k1"), TestContext.Current.CancellationToken)).Value!;
        var boom = (await Handler().PickAsync("s", start.RoundId, MineTiles()[0], TestContext.Current.CancellationToken)).Value!;
        var after = await Handler().PickAsync("s", start.RoundId, SafeTile(), TestContext.Current.CancellationToken);
        var cash = await Handler().CashoutAsync("s", start.RoundId, TestContext.Current.CancellationToken);

        boom.Status.ShouldBe("busted");
        after.Error.ShouldBe(SpinError.RoundOver);
        cash.Error.ShouldBe(SpinError.RoundOver);
        wallet.Balance.ShouldBe(100_000 - 1000);
    }

    [Fact]
    public async Task The_same_tile_twice_is_refused()
    {
        var start = (await Handler().StartAsync(new MinesStart("s", 1000, 3, "k1"), TestContext.Current.CancellationToken)).Value!;
        await Handler().PickAsync("s", start.RoundId, SafeTile(), TestContext.Current.CancellationToken);
        var again = await Handler().PickAsync("s", start.RoundId, SafeTile(), TestContext.Current.CancellationToken);
        var view = (await Handler().PickAsync("s", start.RoundId, SafeTile(1), TestContext.Current.CancellationToken)).Value!;

        again.Error.ShouldBe(SpinError.TileTaken);
        view.MultiplierBp.ShouldBe(Mines.MultiplierBp(3, 2));
    }

    [Fact]
    public async Task A_replayed_start_never_reveals_the_mines_of_a_round_not_played_out()
    {
        wallet.Replies["bet"] = WalletStatus.Unreachable;
        await Handler().StartAsync(new MinesStart("s", 1000, 3, "k1"), TestContext.Current.CancellationToken);
        wallet.Replies["bet"] = WalletStatus.Ok;

        var replay = await Handler().StartAsync(new MinesStart("s", 1000, 3, "k1"), TestContext.Current.CancellationToken);

        replay.Value?.MineTiles.ShouldBeNull();
    }
}
