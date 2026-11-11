using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

public sealed record CrashBet(string SessionToken, long Stake, long? AutoBp, string IdempotencyKey);

/// <summary>A player's bet as they see it. <paramref name="Status"/>: open, cashed, lost or refunded.</summary>
public sealed record CrashBetView(Guid RoundId, Guid Table, long Stake, long? AutoBp, string Status, long? CashedAtBp, long Win, long? Balance);

/// <summary>The shared round as every viewer sees it; the point and seed appear only once it has crashed.</summary>
public sealed record CrashPublic(Guid Id, long Seq, string SeedHash, string Phase, DateTimeOffset BettingUntil, DateTimeOffset ServerNow, double Rate, long? PointBp, string? Seed);

/// <summary>The shared Crash round; bets and cash-outs are judged by the server's clock at receipt, and placing a bet holds the same gate as take-off.</summary>
public sealed partial class CrashRoom(
    IRoundStore store,
    ICrashStore crashes,
    IWallet wallet,
    ICrashBroadcast broadcast,
    TimeProvider time,
    Func<string> newSeed,
    IGameRates rates,
    ILogger<CrashRoom> logger) : IDisposable
{
    /// <summary>Losing bets settle this long after the crash, so a cash-out received in time but still being stored wins the race.</summary>
    public static readonly TimeSpan SettleGrace = TimeSpan.FromSeconds(1);

    public const int MaxBetsPerPlayer = 2;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CrashBoard board = new();
    private readonly Cashier cashier = new(store, wallet);
    private volatile CrashRound? current;
    private bool settled;
    private DateTimeOffset lastSync;

    public CrashRound? Current => current;

    public void Dispose() => gate.Dispose();

    public CrashPublic? Public() => current is { } r ? ToPublic(r, time.GetUtcNow()) : null;

    public IReadOnlyList<BetPublic> Board() => board.NewestFirst();

    public async Task OpenBettingAsync(CancellationToken cancellationToken)
    {
        var seq = await crashes.LastSeqAsync(cancellationToken) + 1;
        var seed = newSeed();
        var point = Crash.PointBp(Fair.Floats(seed, Crash.ClientSeed, seq, 1)[0], rates.For(Crash.GameId));
        var round = new CrashRound(Guid.NewGuid(), seq, seed, Fair.Hash(seed), point, CrashPhase.Betting, time.GetUtcNow() + Crash.Betting, null);
        await crashes.InsertAsync(round, cancellationToken);
        current = round;
        settled = false;
        board.Reset(round.Id);
        await broadcast.PublishAsync("round", ToPublic(round, time.GetUtcNow()), cancellationToken);
    }

    public async Task FlyAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            // A late take-off starts the flight clock at take-off; the point was fixed before betting, so that stays fair.
            var now = time.GetUtcNow();
            var round = current! with { Phase = CrashPhase.Flying, BettingUntil = now > current!.BettingUntil ? now : current!.BettingUntil };
            await crashes.SetPhaseAsync(round.Id, CrashPhase.Flying, null, round.BettingUntil, cancellationToken);
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
        if (Array.IndexOf(InstantStakes.Offered, request.Stake) < 0 || request.AutoBp is < Crash.MinAutoBp or > Multipliers.CapBp || !IdempotencyKey.IsValid(request.IdempotencyKey))
        {
            return GameError.InvalidStake;
        }

        var identity = await wallet.IdentifyAsync(request.SessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        var player = identity.Value;
        if (await store.FindByKeyAsync(player.Ref, request.IdempotencyKey, cancellationToken) is { } repeat)
        {
            return repeat.GameId == Crash.GameId && await crashes.BetAsync(repeat.RoundId, cancellationToken) is { } same ? await ViewAsync(same, player.Balance, cancellationToken) : GameError.KeyInUse;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            return await PlaceAsync(request, player, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<CrashBetView>> CashoutAsync(string sessionToken, Guid roundId, CancellationToken cancellationToken)
    {
        // Judged at receipt, before any wallet call: the session that placed the bet is the credential.
        var received = time.GetUtcNow();
        if (await crashes.BetAsync(roundId, cancellationToken) is not { } bet || bet.Round.SessionToken != sessionToken)
        {
            return GameError.RoundNotFound;
        }

        if (current is not { } table || table.Id != bet.Table)
        {
            return GameError.RoundOver;
        }

        if (table.Phase == CrashPhase.Betting)
        {
            return GameError.NotFlying;
        }

        var elapsed = (received - table.BettingUntil).TotalMilliseconds;
        if (elapsed >= Crash.ElapsedFor(table.PointBp))
        {
            return GameError.RoundOver;
        }

        // An auto target the curve already passed is what the bet was owed.
        var bp = Crash.MultiplierBpAt(elapsed);
        if (bet.AutoBp is { } auto && auto <= bp)
        {
            bp = auto;
        }

        if (await CashOutAsync(bet, bp, cancellationToken) is not { } paid)
        {
            return GameError.RoundOver;
        }

        var balance = await cashier.PayWinAsync(paid.Round, paid.Spin, null, cancellationToken);
        await ShowCashOutAsync(bet, bp, paid.Spin.Win, cancellationToken);
        return new CrashBetView(roundId, table.Id, bet.Round.Stake, bet.AutoBp, "cashed", bp, Multipliers.Pay(bet.Round.Stake, bp), balance);
    }

    /// <summary>One step of the flight: pays passed auto cash-outs, announces the crash, and settles the rest a grace period later.</summary>
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
        if (elapsed >= crashAt && table.Phase == CrashPhase.Flying)
        {
            table = table with { Phase = CrashPhase.Crashed, CrashedAt = table.BettingUntil.AddMilliseconds(crashAt) };
            await crashes.SetPhaseAsync(table.Id, CrashPhase.Crashed, table.CrashedAt, null, cancellationToken);
            current = table;
            await broadcast.PublishAsync("crashed", ToPublic(table, now), cancellationToken);
        }
        else if (table.Phase == CrashPhase.Flying && now - lastSync >= TimeSpan.FromSeconds(1))
        {
            lastSync = now;
            await broadcast.PublishAsync("tick", ToPublic(table, now), cancellationToken);
        }

        foreach (var bet in await crashes.OpenBetsAsync(table.Id, cancellationToken))
        {
            if (bet.AutoBp is { } auto && auto <= reached)
            {
                await PayInBackgroundAsync(bet, auto, cancellationToken);
            }
        }

        if (table.Phase == CrashPhase.Crashed && elapsed >= crashAt + SettleGrace.TotalMilliseconds)
        {
            await SettleAsync(table, cancellationToken);
            settled = true;
        }
    }

    /// <summary>After a stop: a round stopped before its crash refunds its still-open bets and is voided; one stopped after it settles.</summary>
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        foreach (var table in await crashes.UnfinishedAsync(cancellationToken))
        {
            if (table.Phase == CrashPhase.Crashed)
            {
                await SettleAsync(table, cancellationToken);
                continue;
            }

            foreach (var bet in await crashes.OpenBetsAsync(table.Id, cancellationToken))
            {
                await RefundAsync(bet.Round, cancellationToken);
            }

            await crashes.SetPhaseAsync(table.Id, CrashPhase.Void, null, null, cancellationToken);
        }
    }

    /// <summary>Recovers, then runs rounds until stopped. The caller guarantees a single instance runs it.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // No round is live while recovering, so cash-outs get round_over instead of racing the refunds.
        Detach();
        try
        {
            await RecoverAsync(cancellationToken);
            await LoopAsync(cancellationToken);
        }
        finally
        {
            Detach();
        }
    }

    public async Task<IReadOnlyList<long>> HistoryAsync(CancellationToken cancellationToken) =>
        [.. (await crashes.HistoryAsync(20, cancellationToken)).Select(r => r.PointBp)];

    public async Task<IReadOnlyList<CrashBetView>> PlayerBetsAsync(string playerRef, long? balance, CancellationToken cancellationToken)
    {
        if (current is not { } table)
        {
            return [];
        }

        var views = new List<CrashBetView>();
        foreach (var bet in await crashes.PlayerBetsAsync(table.Id, playerRef, cancellationToken))
        {
            views.Add(await ViewAsync(bet, balance, cancellationToken));
        }

        return views;
    }

    private async Task<Result<CrashBetView>> PlaceAsync(CrashBet request, Player player, CancellationToken cancellationToken)
    {
        if (current is not { Phase: CrashPhase.Betting } table || time.GetUtcNow() >= table.BettingUntil)
        {
            return GameError.BettingClosed;
        }

        if ((await crashes.PlayerBetsAsync(table.Id, player.Ref, cancellationToken)).Count >= MaxBetsPerPlayer)
        {
            return GameError.RoundInProgress;
        }

        var roundId = Guid.NewGuid();
        var round = new RoundRecord(roundId, player.Ref, request.SessionToken, Crash.GameId, request.Stake, request.IdempotencyKey, RoundStatus.Pending, 0, 0, time.GetUtcNow());
        var spin = new SpinRecord(roundId, 0, [], JsonSerializer.Serialize(new { table = table.Id, auto = request.AutoBp }, Json), 0, TransactionIds.Bet(roundId), null, false);
        await crashes.InsertBetAsync(round, spin, table.Id, request.AutoBp, cancellationToken);
        var staked = await cashier.TakeStakeAsync(round, cancellationToken);
        if (staked.Failed)
        {
            return staked.Error;
        }

        await store.UpdateAsync(roundId, RoundStatus.Open, 0, 0, cancellationToken);
        var shown = new BetPublic(table.Id, Tag(roundId), Mask(player.Ref), request.Stake, null, null);
        board.Show(roundId, shown);
        await broadcast.PublishAsync("bet", shown, cancellationToken);
        return new CrashBetView(roundId, table.Id, request.Stake, request.AutoBp, "open", null, 0, staked.Value);
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
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

    private void Detach()
    {
        current = null;
        board.Detach();
    }

    private async Task SettleAsync(CrashRound table, CancellationToken cancellationToken)
    {
        foreach (var bet in await crashes.OpenBetsAsync(table.Id, cancellationToken))
        {
            if (bet.AutoBp is { } auto && auto <= table.PointBp)
            {
                await PayInBackgroundAsync(bet, auto, cancellationToken);
                continue;
            }

            await store.StepAsync(bet.Round.RoundId, bet.Round.PlayerRef, Crash.GameId, (_, spins) =>
                new RoundStep(new SpinRecord(bet.Round.RoundId, spins.Count, [], "{}", 0, null, null, false), RoundStatus.Settled, 0), cancellationToken);
        }
    }

    /// <summary>Voided only while still open, so a bet that cashed out meanwhile keeps its win.</summary>
    private async Task RefundAsync(RoundRecord round, CancellationToken cancellationToken)
    {
        if (!await store.VoidIfAsync(round.RoundId, RoundStatus.Open, cancellationToken))
        {
            return;
        }

        var reply = await wallet.RollbackAsync(round.SessionToken, Crash.GameId, TransactionIds.Rollback(round.RoundId), round.RoundId.ToString(), TransactionIds.Bet(round.RoundId), cancellationToken);
        if (reply.Status == WalletStatus.Ok)
        {
            await store.SetRollbackPendingAsync(round.RoundId, false, cancellationToken);
        }
    }

    /// <summary>Records the cash-out under the bet's row lock; null when the bet was no longer open.</summary>
    private Task<PlayedSpin?> CashOutAsync(CrashBetRecord bet, long bp, CancellationToken cancellationToken)
    {
        var r = bet.Round;
        var win = Multipliers.Pay(r.Stake, bp);
        return store.StepAsync(r.RoundId, r.PlayerRef, Crash.GameId, (_, spins) =>
            new RoundStep(new SpinRecord(r.RoundId, spins.Count, [], JsonSerializer.Serialize(new { bp }, Json), win, null, TransactionIds.Win(r.RoundId, spins.Count), false), RoundStatus.WinPending, win), cancellationToken);
    }

    private async Task ShowCashOutAsync(CrashBetRecord bet, long bp, long win, CancellationToken cancellationToken)
    {
        var shown = new BetPublic(bet.Table, Tag(bet.Round.RoundId), Mask(bet.Round.PlayerRef), bet.Round.Stake, bp, win);
        board.Show(bet.Round.RoundId, shown);
        await broadcast.PublishAsync("cashout", shown, cancellationToken);
    }

    /// <summary>Auto cash-outs pay off the loop so many payouts never delay the crash or the next tick.</summary>
    private async Task PayInBackgroundAsync(CrashBetRecord bet, long bp, CancellationToken cancellationToken)
    {
        if (await CashOutAsync(bet, bp, cancellationToken) is not { } paid)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await cashier.PayWinAsync(paid.Round, paid.Spin, null, CancellationToken.None);
            }
            catch (Exception e)
            {
                // Still owed: RoundRecovery pays it under the same transaction id.
                PayoutFailed(logger, paid.Round.RoundId, e);
            }
        }, CancellationToken.None);
        await ShowCashOutAsync(bet, bp, paid.Spin.Win, cancellationToken);
    }

    private async Task<CrashBetView> ViewAsync(CrashBetRecord bet, long? balance, CancellationToken cancellationToken)
    {
        var r = await store.GetAsync(bet.Round.RoundId, cancellationToken) ?? bet.Round;
        var spins = await store.SpinsAsync(r.RoundId, cancellationToken);
        var win = spins.Sum(s => s.Win);
        long? cashedAt = win > 0 ? JsonSerializer.Deserialize<JsonElement>(spins[^1].OutcomeJson).GetProperty("bp").GetInt64() : null;
        var status = r.Status switch
        {
            RoundStatus.Open or RoundStatus.Pending => "open",
            RoundStatus.Voided or RoundStatus.Refused => "refunded",
            _ => win > 0 ? "cashed" : "lost",
        };
        return new CrashBetView(r.RoundId, bet.Table, r.Stake, bet.AutoBp, status, cashedAt, win, balance);
    }

    private static string Mask(string playerRef) => $"Player {playerRef[^4..].ToUpperInvariant()}";

    /// <summary>Enough to match a bet's cash-out on the board, nothing to act on it with.</summary>
    private static string Tag(Guid roundId) => Convert.ToHexStringLower(SHA256.HashData(roundId.ToByteArray()))[..10];

    private static CrashPublic ToPublic(CrashRound r, DateTimeOffset now)
    {
        var over = r.Phase is CrashPhase.Crashed or CrashPhase.Void;
        return new CrashPublic(r.Id, r.Seq, r.SeedHash, r.Phase, r.BettingUntil, now, Crash.Rate, over ? r.PointBp : null, over ? r.Seed : null);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Crash payout for round {RoundId} failed; recovery will retry it")]
    private static partial void PayoutFailed(ILogger logger, Guid roundId, Exception exception);
}
