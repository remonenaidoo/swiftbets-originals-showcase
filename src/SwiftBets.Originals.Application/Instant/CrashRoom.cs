using System.Text.Json;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

public sealed record CrashBet(string SessionToken, long Stake, long? AutoBp, string IdempotencyKey);

/// <summary>A player's bet as they see it. <paramref name="Status"/>: open, cashed, lost or refunded.</summary>
public sealed record CrashBetView(Guid RoundId, Guid Table, long Stake, long? AutoBp, string Status, long? CashedAtBp, long Win, long? Balance);

/// <summary>
/// The shared round as every viewer sees it. The flight starts at <paramref name="BettingUntil"/>; a client draws the
/// curve from it, <paramref name="ServerNow"/> and <paramref name="Rate"/>. The point and seed appear only once crashed.
/// </summary>
public sealed record CrashPublic(Guid Id, long Seq, string SeedHash, string Phase, DateTimeOffset BettingUntil, DateTimeOffset ServerNow, double Rate, long? PointBp, string? Seed);

public sealed record CashoutPublic(Guid Table, string Player, long Bp);

/// <summary>
/// The shared Crash round: betting (7 s), flying until the crash point, crashed, then a pause and the next round.
/// One loop drives it (<see cref="RunAsync"/>); bets and cash-outs arrive over HTTP and are judged by the server's
/// clock at receipt. A bet cannot be half-placed when the flight starts: placing holds the room's gate, and so does
/// starting the flight. Losing bets settle a grace period after the crash, so a cash-out received before the crash
/// but still being stored is never beaten by the settlement.
/// </summary>
public sealed class CrashRoom(IRoundStore store, ICrashStore crashes, IWallet wallet, ICrashBroadcast broadcast, TimeProvider time, Func<string> newSeed) : IDisposable
{
    public static readonly TimeSpan SettleGrace = TimeSpan.FromSeconds(1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile CrashRound? current;
    private bool settled;
    private DateTimeOffset lastSync;

    public CrashRound? Current => current;

    public void Dispose() => gate.Dispose();

    public CrashPublic? Public() => current is { } r ? ToPublic(r, time.GetUtcNow()) : null;

    public async Task OpenBettingAsync(CancellationToken cancellationToken)
    {
        var seq = await crashes.LastSeqAsync(cancellationToken) + 1;
        var seed = newSeed();
        var round = new CrashRound(Guid.NewGuid(), seq, seed, Fair.Hash(seed), Crash.PointBp(Fair.Floats(seed, Crash.ClientSeed, seq, 1)[0]), CrashPhase.Betting, time.GetUtcNow() + Crash.Betting, null);
        await crashes.InsertAsync(round, cancellationToken);
        current = round;
        settled = false;
        await broadcast.PublishAsync("round", ToPublic(round, time.GetUtcNow()), cancellationToken);
    }

    public async Task FlyAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var round = current! with { Phase = CrashPhase.Flying };
            await crashes.SetPhaseAsync(round.Id, CrashPhase.Flying, null, cancellationToken);
            current = round;
        }
        finally
        {
            gate.Release();
        }

        lastSync = time.GetUtcNow();
        await broadcast.PublishAsync("round", ToPublic(current!, time.GetUtcNow()), cancellationToken);
    }

    public async Task<Result<CrashBetView>> BetAsync(CrashBet request, CancellationToken cancellationToken)
    {
        if (Array.IndexOf(Crash.Stakes, request.Stake) < 0 || request.AutoBp is < Crash.MinAutoBp or > Mult.CapBp || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100)
        {
            return SpinError.InvalidStake;
        }

        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, request.SessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await store.FindByKeyAsync(playerRef!, request.IdempotencyKey, cancellationToken) is { } repeat)
        {
            return repeat.GameId == Crash.GameId && await crashes.BetAsync(repeat.RoundId, cancellationToken) is { } same ? await ViewAsync(same, balance, cancellationToken) : SpinError.KeyInUse;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (current is not { Phase: CrashPhase.Betting } table || time.GetUtcNow() >= table.BettingUntil)
            {
                return SpinError.BettingClosed;
            }

            if (await store.FindOpenAsync(playerRef!, Crash.GameId, cancellationToken) is not null)
            {
                return SpinError.RoundInProgress;
            }

            var roundId = Guid.NewGuid();
            var round = new RoundRecord(roundId, playerRef!, request.SessionToken, Crash.GameId, request.Stake, request.IdempotencyKey, RoundStatus.Pending, 0, 0, time.GetUtcNow());
            var spin = new SpinRecord(roundId, 0, [], JsonSerializer.Serialize(new { table = table.Id, auto = request.AutoBp }, Json), 0, Play.BetTx(roundId), null, false);
            await crashes.InsertBetAsync(round, spin, table.Id, request.AutoBp, cancellationToken);
            (balance, error) = await Stake.TakeAsync(store, wallet, round, cancellationToken);
            if (error is not null)
            {
                return error;
            }

            await store.UpdateAsync(roundId, RoundStatus.Open, 0, 0, cancellationToken);
            return new CrashBetView(roundId, table.Id, request.Stake, request.AutoBp, "open", null, 0, balance);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<CrashBetView>> CashoutAsync(string sessionToken, Guid roundId, CancellationToken cancellationToken)
    {
        var received = time.GetUtcNow();
        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await crashes.BetAsync(roundId, cancellationToken) is not { } bet || bet.Round.PlayerRef != playerRef)
        {
            return SpinError.RoundNotFound;
        }

        if (current is not { } table || table.Id != bet.Table)
        {
            return SpinError.RoundOver;
        }

        if (table.Phase == CrashPhase.Betting)
        {
            return SpinError.NotFlying;
        }

        var elapsed = (received - table.BettingUntil).TotalMilliseconds;
        if (elapsed >= Crash.ElapsedFor(table.PointBp))
        {
            return SpinError.RoundOver;
        }

        // An auto target already passed is what the bet was owed; the click came after it.
        var bp = Crash.MultiplierBpAt(elapsed);
        if (bet.AutoBp is { } auto && auto <= bp)
        {
            bp = auto;
        }

        var (paid, paidBalance) = await PayAtAsync(bet, bp, cancellationToken);
        if (!paid)
        {
            return SpinError.RoundOver;
        }

        return new CrashBetView(roundId, table.Id, bet.Round.Stake, bet.AutoBp, "cashed", bp, Mult.Pay(bet.Round.Stake, bp), paidBalance ?? balance);
    }

    /// <summary>
    /// One step of the flight: pays auto cash-outs the curve has passed, announces the crash, and a grace period
    /// later settles every bet still open (an auto target at or below the point is paid, the rest lose).
    /// </summary>
    public async Task TickAsync(CancellationToken cancellationToken)
    {
        if (current is not { } table || table.Phase == CrashPhase.Betting || settled)
        {
            return;
        }

        var now = time.GetUtcNow();
        var elapsed = (now - table.BettingUntil).TotalMilliseconds;
        var crashAt = Crash.ElapsedFor(table.PointBp);
        var reached = Math.Min(Crash.MultiplierBpAt(elapsed), table.PointBp);
        foreach (var bet in await crashes.OpenBetsAsync(table.Id, cancellationToken))
        {
            if (bet.AutoBp is { } auto && auto <= reached)
            {
                await PayAtAsync(bet, auto, cancellationToken);
            }
        }

        if (elapsed >= crashAt && table.Phase == CrashPhase.Flying)
        {
            table = table with { Phase = CrashPhase.Crashed, CrashedAt = table.BettingUntil.AddMilliseconds(crashAt) };
            await crashes.SetPhaseAsync(table.Id, CrashPhase.Crashed, table.CrashedAt, cancellationToken);
            current = table;
            await broadcast.PublishAsync("crashed", ToPublic(table, now), cancellationToken);
        }
        else if (table.Phase == CrashPhase.Flying && now - lastSync >= TimeSpan.FromSeconds(1))
        {
            lastSync = now;
            await broadcast.PublishAsync("tick", ToPublic(table, now), cancellationToken);
        }

        if (table.Phase == CrashPhase.Crashed && elapsed >= crashAt + SettleGrace.TotalMilliseconds)
        {
            foreach (var bet in await crashes.OpenBetsAsync(table.Id, cancellationToken))
            {
                await store.StepAsync(bet.Round.RoundId, bet.Round.PlayerRef, Crash.GameId, (_, spins) =>
                    (new SpinRecord(bet.Round.RoundId, spins.Count, [], "{}", 0, null, null, false), RoundStatus.Settled, 0L), cancellationToken);
            }

            settled = true;
        }
    }

    /// <summary>Refunds the open bets of rounds a stopped service left unfinished, and marks those rounds void.</summary>
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        foreach (var table in await crashes.UnfinishedAsync(cancellationToken))
        {
            foreach (var bet in await crashes.OpenBetsAsync(table.Id, cancellationToken))
            {
                var r = bet.Round;
                await store.SetRollbackPendingAsync(r.RoundId, true, cancellationToken);
                await store.UpdateAsync(r.RoundId, RoundStatus.Voided, 0, 0, cancellationToken);
                var reply = await wallet.RollbackAsync(r.SessionToken, Crash.GameId, Play.RollbackTx(r.RoundId), r.RoundId.ToString(), Play.BetTx(r.RoundId), cancellationToken);
                if (reply.Status == WalletStatus.Ok)
                {
                    await store.SetRollbackPendingAsync(r.RoundId, false, cancellationToken);
                }
            }

            await crashes.SetPhaseAsync(table.Id, CrashPhase.Void, null, cancellationToken);
        }
    }

    /// <summary>The loop: recover, then rounds forever. Only one instance runs it (the caller holds the lock).</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await RecoverAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await OpenBettingAsync(cancellationToken);
            await Task.Delay(Crash.Betting, time, cancellationToken);
            await FlyAsync(cancellationToken);
            while (!settled)
            {
                await TickAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(100), time, cancellationToken);
            }

            await broadcast.PublishAsync("history", await HistoryAsync(cancellationToken), cancellationToken);
            await Task.Delay(Crash.Pause, time, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<long>> HistoryAsync(CancellationToken cancellationToken) =>
        [.. (await crashes.HistoryAsync(20, cancellationToken)).Select(r => r.PointBp)];

    public async Task<CrashBetView?> OpenBetAsync(string playerRef, long? balance, CancellationToken cancellationToken) =>
        await store.FindOpenAsync(playerRef, Crash.GameId, cancellationToken) is { } round && await crashes.BetAsync(round.RoundId, cancellationToken) is { } bet
            ? await ViewAsync(bet, balance, cancellationToken) : null;

    /// <summary>Pays a bet at <paramref name="bp"/> in one locked step; not paid when it was no longer open. The balance is null when the casino did not answer.</summary>
    private async Task<(bool Paid, long? Balance)> PayAtAsync(CrashBetRecord bet, long bp, CancellationToken cancellationToken)
    {
        var r = bet.Round;
        var win = Mult.Pay(r.Stake, bp);
        if (await store.StepAsync(r.RoundId, r.PlayerRef, Crash.GameId, (_, spins) =>
            (new SpinRecord(r.RoundId, spins.Count, [], JsonSerializer.Serialize(new { bp }, Json), win, null, Play.WinTx(r.RoundId, spins.Count), false), RoundStatus.WinPending, win), cancellationToken) is not var (round, spin))
        {
            return (false, null);
        }

        var balance = await Stake.PayAsync(store, wallet, round, spin, null, cancellationToken);
        await broadcast.PublishAsync("cashout", new CashoutPublic(bet.Table, Mask(r.PlayerRef), bp), cancellationToken);
        return (true, balance);
    }

    private async Task<CrashBetView> ViewAsync(CrashBetRecord bet, long? balance, CancellationToken cancellationToken)
    {
        var r = await store.GetAsync(bet.Round.RoundId, cancellationToken) ?? bet.Round;
        var spins = await store.SpinsAsync(r.RoundId, cancellationToken);
        var win = spins.Sum(s => s.Win);
        long? at = win > 0 ? JsonSerializer.Deserialize<JsonElement>(spins[^1].OutcomeJson).GetProperty("bp").GetInt64() : null;
        var status = r.Status switch
        {
            RoundStatus.Open or RoundStatus.Pending => "open",
            RoundStatus.Voided or RoundStatus.Refused => "refunded",
            _ => win > 0 ? "cashed" : "lost",
        };
        return new CrashBetView(r.RoundId, bet.Table, r.Stake, bet.AutoBp, status, at, win, balance);
    }

    private static string Mask(string playerRef) => $"Player {playerRef[^4..].ToUpperInvariant()}";

    private static CrashPublic ToPublic(CrashRound r, DateTimeOffset now)
    {
        var over = r.Phase is CrashPhase.Crashed or CrashPhase.Void;
        return new CrashPublic(r.Id, r.Seq, r.SeedHash, r.Phase, r.BettingUntil, now, Crash.Rate, over ? r.PointBp : null, over ? r.Seed : null);
    }
}
