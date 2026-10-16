using Microsoft.Extensions.Time.Testing;
using SwiftBets.Originals.Application.Instant;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Tests;

public sealed class CrashTests : IDisposable
{
    private readonly FakeStore store = new();
    private readonly FakeCrashStore crashes;
    private readonly FakeWallet wallet = new();
    private readonly FakeTimeProvider time = new(DateTimeOffset.Parse("2026-10-16T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    public CrashTests() => crashes = new FakeCrashStore(store);

    public void Dispose() => store.Dispose();

    /// <summary>A room whose first round (seq 1) crashes at or above 2.00×.</summary>
    private CrashRoom Room()
    {
        var seed = Enumerable.Range(0, 1000).Select(i => $"seed-{i}").First(s => Crash.PointBp(Fair.Floats(s, Crash.ClientSeed, 1, 1)[0]) >= 20_000);
        return new CrashRoom(store, crashes, wallet, new FakeBroadcast(), time, () => seed);
    }

    private async Task<(CrashRoom Room, Guid Bet)> Flying()
    {
        var room = Room();
        await room.OpenBettingAsync(ct);
        var bet = (await room.BetAsync(new CrashBet("s", 1000, null, "k1"), ct)).Value!;
        time.Advance(Crash.Betting);
        await room.FlyAsync(ct);
        return (room, bet.RoundId);
    }

    [Fact]
    public async Task A_cashout_before_the_crash_pays_stake_times_the_multiplier()
    {
        var (room, bet) = await Flying();
        time.Advance(TimeSpan.FromMilliseconds(Crash.ElapsedFor(15_000)));

        var cash = (await room.CashoutAsync("s", bet, ct)).Value!;

        cash.CashedAtBp.ShouldBe(15_000);
        cash.Win.ShouldBe(1500);
        wallet.Balance.ShouldBe(100_000 - 1000 + 1500);
    }

    [Fact]
    public async Task A_cashout_at_or_after_the_crash_loses()
    {
        var (room, bet) = await Flying();
        time.Advance(TimeSpan.FromMilliseconds(Crash.ElapsedFor(room.Current!.PointBp)));

        var late = await room.CashoutAsync("s", bet, ct);
        time.Advance(CrashRoom.SettleGrace);
        await room.TickAsync(ct);

        late.Error.ShouldBe(SpinError.RoundOver);
        store.Rounds[bet].Status.ShouldBe(RoundStatus.Settled);
        wallet.Balance.ShouldBe(100_000 - 1000);
    }

    [Fact]
    public async Task An_interrupted_round_refunds_its_open_bets()
    {
        var (_, bet) = await Flying();

        await Room().RecoverAsync(ct);

        store.Rounds[bet].Status.ShouldBe(RoundStatus.Voided);
        wallet.Calls.ShouldContain(c => c.Action == "rollback" && c.Reference == $"{bet}:bet");
        wallet.Balance.ShouldBe(100_000);
    }

    [Fact]
    public async Task A_player_holds_two_bets_in_a_round_but_not_three()
    {
        var room = Room();
        await room.OpenBettingAsync(ct);

        var first = await room.BetAsync(new CrashBet("s", 1000, null, "k1"), ct);
        var second = await room.BetAsync(new CrashBet("s", 500, 20_000, "k2"), ct);
        var third = await room.BetAsync(new CrashBet("s", 100, null, "k3"), ct);

        first.Error.ShouldBeNull();
        second.Error.ShouldBeNull();
        third.Error.ShouldBe(SpinError.RoundInProgress);
        wallet.Balance.ShouldBe(100_000 - 1500);
    }

    [Fact]
    public async Task A_bet_cashed_out_while_recovery_runs_is_not_refunded()
    {
        var (room, bet) = await Flying();
        time.Advance(TimeSpan.FromMilliseconds(Crash.ElapsedFor(12_000)));
        await room.CashoutAsync("s", bet, ct);
        crashes.StaleOpenBets = true; // recovery read the bet as open just before the cash-out landed

        await Room().RecoverAsync(ct);

        store.Rounds[bet].Status.ShouldBe(RoundStatus.Settled);
        wallet.Calls.ShouldNotContain(c => c.Action == "rollback");
    }

    [Fact]
    public async Task A_round_stopped_after_its_crash_settles_its_bets_on_restart()
    {
        var (room, bet) = await Flying();
        time.Advance(TimeSpan.FromMilliseconds(Crash.ElapsedFor(room.Current!.PointBp)));
        await room.TickAsync(ct); // crashed, not yet settled

        await Room().RecoverAsync(ct);

        store.Rounds[bet].Status.ShouldBe(RoundStatus.Settled);
        wallet.Balance.ShouldBe(100_000 - 1000);
    }

    [Fact]
    public async Task A_late_take_off_starts_the_flight_clock_at_take_off()
    {
        var room = Room();
        await room.OpenBettingAsync(ct);
        var bet = (await room.BetAsync(new CrashBet("s", 1000, null, "k1"), ct)).Value!;
        time.Advance(Crash.Betting + TimeSpan.FromSeconds(3)); // the flight starts 3 s late
        await room.FlyAsync(ct);
        time.Advance(TimeSpan.FromMilliseconds(Crash.ElapsedFor(15_000)));

        var cash = (await room.CashoutAsync("s", bet.RoundId, ct)).Value!;

        cash.CashedAtBp.ShouldBe(15_000);
    }

    [Fact]
    public async Task A_cashout_does_not_wait_on_the_wallet_balance()
    {
        var (room, bet) = await Flying();
        time.Advance(TimeSpan.FromMilliseconds(Crash.ElapsedFor(12_000)));
        wallet.Replies["balance"] = WalletStatus.Unreachable;

        var cash = await room.CashoutAsync("s", bet, ct);

        cash.Value!.CashedAtBp.ShouldBe(12_000);
    }

    [Fact]
    public async Task Paying_an_auto_cash_out_does_not_hold_up_the_crash()
    {
        var room = Room();
        await room.OpenBettingAsync(ct);
        await room.BetAsync(new CrashBet("s", 1000, 10_100, "k1"), ct);
        time.Advance(Crash.Betting);
        await room.FlyAsync(ct);
        time.Advance(TimeSpan.FromMilliseconds(Crash.ElapsedFor(room.Current!.PointBp)));
        wallet.WinDelay = TimeSpan.FromSeconds(30);

        var tick = room.TickAsync(ct);

        (await Task.WhenAny(tick, Task.Delay(2000, ct))).ShouldBe(tick);
        room.Current!.Phase.ShouldBe(CrashPhase.Crashed);
    }
}
