using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application.Tests;

public sealed class SpinHandlerTests
{
    private readonly FakeWallet wallet = new();
    private readonly FakeStore store = new();

    private SpinHandler Handler() => new(store, wallet, new RoundEngine(GildedSavanna.Definition, n => 0), TimeProvider.System);

    [Fact]
    public async Task Invalid_stake_is_refused_before_the_wallet()
    {
        var result = await Handler().SpinAsync(new SpinRequest("s", 150, "k1"), CancellationToken.None);

        result.Error!.Code.ShouldBe("invalid_stake");
        result.Error.Http.ShouldBe(400);
        wallet.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_first_round()
    {
        var first = await Handler().SpinAsync(new SpinRequest("s", 100, "k1"), CancellationToken.None);
        var again = await Handler().SpinAsync(new SpinRequest("s", 100, "k1"), CancellationToken.None);

        again.Value!.RoundId.ShouldBe(first.Value!.RoundId);
        wallet.Calls.Count(c => c.Action == "bet").ShouldBe(1);
    }

    [Fact]
    public async Task A_refused_bet_moves_nothing_and_says_why()
    {
        wallet.Replies["bet"] = WalletStatus.InsufficientFunds;

        var result = await Handler().SpinAsync(new SpinRequest("s", 100, "k1"), CancellationToken.None);

        result.Error!.Http.ShouldBe(402);
        store.Rounds.Values.Single().Status.ShouldBe(RoundStatus.Refused);
        wallet.Calls.ShouldNotContain(c => c.Action == "win");
    }

    [Fact]
    public async Task No_answer_to_the_bet_rolls_it_back()
    {
        wallet.Replies["bet"] = WalletStatus.Unreachable;

        var result = await Handler().SpinAsync(new SpinRequest("s", 100, "k1"), CancellationToken.None);

        var round = store.Rounds.Values.Single();
        result.Error!.Http.ShouldBe(503);
        round.Status.ShouldBe(RoundStatus.Voided);
        wallet.Calls.ShouldContain(c => c.Action == "rollback" && c.Reference == $"{round.RoundId}:bet");
    }

    [Fact]
    public async Task Spin_during_a_feature_is_refused_and_state_resumes_it()
    {
        var open = new RoundRecord(Guid.NewGuid(), "player-1", "s", GildedSavanna.GameId, 100, "k0", RoundStatus.Feature, 7, 0, DateTimeOffset.UtcNow);
        await store.InsertAsync(open, new SpinRecord(open.RoundId, 0, [0, 0, 0, 0, 0], "{}", 0, null, null, true), CancellationToken.None);

        var result = await Handler().SpinAsync(new SpinRequest("s", 100, "k1"), CancellationToken.None);
        var state = await new StateQuery(store, wallet).GetAsync("s", CancellationToken.None);

        result.Error!.Code.ShouldBe("feature_in_progress");
        state.Value!.OpenFeature!.RoundId.ShouldBe(open.RoundId);
        state.Value.OpenFeature.FreeSpinsLeft.ShouldBe(7);
    }

    [Fact]
    public async Task Expired_session_is_401()
    {
        wallet.Replies["balance"] = WalletStatus.SessionInvalid;

        var result = await Handler().SpinAsync(new SpinRequest("s", 100, "k1"), CancellationToken.None);

        result.Error!.Http.ShouldBe(401);
    }
}

public sealed class FeatureAndRecoveryTests
{
    private readonly FakeWallet wallet = new();
    private readonly FakeStore store = new();
    private readonly RoundEngine engine = new(GildedSavanna.Definition, n => 0);

    private async Task<RoundRecord> OpenFeature(int left)
    {
        var round = new RoundRecord(Guid.NewGuid(), "player-1", "s", GildedSavanna.GameId, 100, "k0", RoundStatus.Feature, left, 0, DateTimeOffset.UtcNow);
        await store.InsertAsync(round, new SpinRecord(round.RoundId, 0, [0, 0, 0, 0, 0], "{}", 0, null, null, true), CancellationToken.None);
        return round;
    }

    [Fact]
    public async Task Two_concurrent_free_spins_each_consume_a_spin()
    {
        var round = await OpenFeature(5);
        wallet.WinDelay = TimeSpan.FromMilliseconds(200);
        var handler = new FeatureHandler(store, wallet, engine);

        await Task.WhenAll(handler.NextAsync("s", round.RoundId, CancellationToken.None), handler.NextAsync("s", round.RoundId, CancellationToken.None));

        store.Rounds[round.RoundId].FreeSpinsLeft.ShouldBe(3);
        store.Spins.Count(s => s.RoundId == round.RoundId).ShouldBe(3);
    }

    [Fact]
    public async Task An_ambiguous_bet_reply_is_rolled_back()
    {
        wallet.Replies["bet"] = "wallet_unavailable";

        await new SpinHandler(store, wallet, engine, TimeProvider.System).SpinAsync(new SpinRequest("s", 100, "k1"), CancellationToken.None);

        var r = store.Rounds.Values.Single();
        r.Status.ShouldBe(RoundStatus.Voided);
        wallet.Calls.ShouldContain(c => c.Action == "rollback");
    }

    [Fact]
    public async Task Recovery_rolls_back_a_round_left_pending_by_a_crash()
    {
        var stuck = new RoundRecord(Guid.NewGuid(), "player-1", "s", GildedSavanna.GameId, 100, "k9", RoundStatus.Pending, 0, 0, DateTimeOffset.UtcNow.AddMinutes(-10));
        await store.InsertAsync(stuck, new SpinRecord(stuck.RoundId, 0, [0, 0, 0, 0, 0], "{}", 0, $"{stuck.RoundId}:bet", null, false), CancellationToken.None);

        await new RoundRecovery(store, wallet, TimeProvider.System).RunOnceAsync(CancellationToken.None);

        store.Rounds[stuck.RoundId].Status.ShouldBe(RoundStatus.Voided);
        store.RollbackPending.ShouldNotContain(stuck.RoundId);
        wallet.Calls.ShouldContain(c => c.Action == "rollback" && c.Reference == $"{stuck.RoundId}:bet");
    }
}
