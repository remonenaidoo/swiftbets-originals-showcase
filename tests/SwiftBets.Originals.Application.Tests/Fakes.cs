using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;
using SwiftBets.Originals.Games;

namespace SwiftBets.Originals.Application.Tests;

/// <summary>A casino that answers each action with a scripted status and records every call.</summary>
public sealed class FakeWallet : IWallet
{
    public long Balance { get; set; } = 100_000;
    public string? PlayerRef { get; set; } = "player-1";
    public Dictionary<string, string> Replies { get; } = new() { ["balance"] = WalletStatus.Ok, ["bet"] = WalletStatus.Ok, ["win"] = WalletStatus.Ok, ["rollback"] = WalletStatus.Ok };
    public List<(string Action, string TransactionId, long Amount, string? Reference)> Calls { get; } = [];
    public TimeSpan WinDelay { get; set; }

    public Task<WalletReply> BalanceAsync(string sessionToken, CancellationToken cancellationToken) => Reply("balance", "", 0, null);

    public Task<WalletReply> BetAsync(string sessionToken, string gameId, string transactionId, string roundId, long amount, CancellationToken cancellationToken) => Reply("bet", transactionId, amount, null);

    public async Task<WalletReply> WinAsync(string sessionToken, string gameId, string transactionId, string roundId, long amount, CancellationToken cancellationToken)
    {
        await Task.Delay(WinDelay, cancellationToken);
        return await Reply("win", transactionId, amount, null);
    }

    public Task<WalletReply> RollbackAsync(string sessionToken, string gameId, string transactionId, string roundId, string betTransactionId, CancellationToken cancellationToken)
    {
        // A confirmed rollback returns the stake of the bet it names, once.
        var refund = Replies["rollback"] == WalletStatus.Ok && !Calls.Any(c => c.Action == "rollback" && c.Reference == betTransactionId && Replies["rollback"] == WalletStatus.Ok)
            ? Calls.Where(c => c.Action == "bet" && c.TransactionId == betTransactionId && Replies["bet"] == WalletStatus.Ok).Sum(c => c.Amount) : 0;
        Balance += refund;
        return Reply("rollback", transactionId, 0, betTransactionId);
    }

    private Task<WalletReply> Reply(string action, string tx, long amount, string? reference)
    {
        Calls.Add((action, tx, amount, reference));
        var status = Replies[action];
        if (status == WalletStatus.Ok)
        {
            Balance += action == "bet" ? -amount : action == "win" ? amount : 0;
        }

        return Task.FromResult(new WalletReply(status, status == WalletStatus.Ok ? Balance : null, action == "balance" && status == WalletStatus.Ok ? PlayerRef : null));
    }
}

public sealed class FakeStore : IRoundStore, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public void Dispose() => gate.Dispose();
    public Dictionary<Guid, RoundRecord> Rounds { get; } = [];
    public List<SpinRecord> Spins { get; } = [];
    public HashSet<Guid> RollbackPending { get; } = [];

    public async Task<(RoundRecord Round, SpinRecord Spin)?> ClaimFreeSpinAsync(Guid roundId, string playerRef, string gameId, Func<RoundRecord, SpinRecord, (SpinRecord Spin, int Left, long PaidUnits)> play, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!Rounds.TryGetValue(roundId, out var round) || round.PlayerRef != playerRef || round.GameId != gameId || round.Status != RoundStatus.Feature || round.FreeSpinsLeft <= 0)
            {
                return null;
            }

            var (spin, left, paid) = play(round, Spins.Where(s => s.RoundId == roundId).MaxBy(s => s.Seq)!);
            Spins.Add(spin);
            round = round with { FreeSpinsLeft = left, PaidUnits = paid, Status = left > 0 ? RoundStatus.Feature : RoundStatus.WinPending };
            Rounds[roundId] = round;
            return (round, spin);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<RoundRecord?> FindOpenAsync(string playerRef, string gameId, CancellationToken cancellationToken) =>
        Task.FromResult(Rounds.Values.FirstOrDefault(r => r.PlayerRef == playerRef && r.GameId == gameId && r.Status is RoundStatus.Open or RoundStatus.Pending));

    public async Task<(RoundRecord Round, SpinRecord Spin)?> StepAsync(Guid roundId, string playerRef, string gameId, Func<RoundRecord, IReadOnlyList<SpinRecord>, (SpinRecord Spin, string Status, long Paid)?> step, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!Rounds.TryGetValue(roundId, out var round) || round.PlayerRef != playerRef || round.GameId != gameId || round.Status != RoundStatus.Open)
            {
                return null;
            }

            if (step(round, [.. Spins.Where(s => s.RoundId == roundId).OrderBy(s => s.Seq)]) is not var (spin, status, paid))
            {
                return null;
            }

            Spins.Add(spin);
            round = round with { Status = status, PaidUnits = paid };
            Rounds[roundId] = round;
            return (round, spin);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task SettleIfPaidAsync(Guid roundId, CancellationToken cancellationToken)
    {
        if (Rounds[roundId].Status == RoundStatus.WinPending && !Spins.Any(s => s.RoundId == roundId && s.Win > 0 && !s.WinPaid))
        {
            Rounds[roundId] = Rounds[roundId] with { Status = RoundStatus.Settled };
        }

        return Task.CompletedTask;
    }

    public Task<bool> VoidIfAsync(Guid roundId, string fromStatus, CancellationToken cancellationToken)
    {
        if (Rounds[roundId].Status != fromStatus)
        {
            return Task.FromResult(false);
        }

        Rounds[roundId] = Rounds[roundId] with { Status = RoundStatus.Voided };
        RollbackPending.Add(roundId);
        return Task.FromResult(true);
    }

    public Task SetRollbackPendingAsync(Guid roundId, bool pending, CancellationToken cancellationToken)
    {
        if (pending) RollbackPending.Add(roundId); else RollbackPending.Remove(roundId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RoundRecord>> NeedingRollbackAsync(DateTimeOffset pendingBefore, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RoundRecord>>([.. Rounds.Values.Where(r => (r.Status == RoundStatus.Pending && r.CreatedAt < pendingBefore) || RollbackPending.Contains(r.RoundId))]);

    public Task<RoundRecord?> FindByKeyAsync(string playerRef, string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(Rounds.Values.FirstOrDefault(r => r.PlayerRef == playerRef && r.IdempotencyKey == idempotencyKey));

    public Task<RoundRecord?> FindOpenFeatureAsync(string playerRef, string gameId, CancellationToken cancellationToken) =>
        Task.FromResult(Rounds.Values.FirstOrDefault(r => r.PlayerRef == playerRef && r.GameId == gameId && r.Status == RoundStatus.Feature));

    public Task<RoundRecord?> GetAsync(Guid roundId, CancellationToken cancellationToken) => Task.FromResult(Rounds.GetValueOrDefault(roundId));

    public Task InsertAsync(RoundRecord round, SpinRecord spin, CancellationToken cancellationToken)
    {
        Rounds[round.RoundId] = round;
        Spins.Add(spin);
        return Task.CompletedTask;
    }

    public Task InsertSpinAsync(SpinRecord spin, CancellationToken cancellationToken)
    {
        if (Spins.Any(s => s.RoundId == spin.RoundId && s.Seq == spin.Seq))
        {
            throw new InvalidOperationException("duplicate spin");
        }

        Spins.Add(spin);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Guid roundId, string status, int freeSpinsLeft, long paidUnits, CancellationToken cancellationToken)
    {
        Rounds[roundId] = Rounds[roundId] with { Status = status, FreeSpinsLeft = freeSpinsLeft, PaidUnits = paidUnits };
        return Task.CompletedTask;
    }

    public Task MarkWinPaidAsync(Guid roundId, int seq, CancellationToken cancellationToken)
    {
        var i = Spins.FindIndex(s => s.RoundId == roundId && s.Seq == seq);
        Spins[i] = Spins[i] with { WinPaid = true };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SpinRecord>> SpinsAsync(Guid roundId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpinRecord>>([.. Spins.Where(s => s.RoundId == roundId).OrderBy(s => s.Seq)]);

    public Task<IReadOnlyList<RoundRecord>> RecentAsync(string playerRef, string gameId, int count, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RoundRecord>>([.. Rounds.Values.Where(r => r.PlayerRef == playerRef && r.GameId == gameId).Take(count)]);

    public Task<IReadOnlyList<(RoundRecord Round, SpinRecord Spin)>> UnpaidWinsAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<(RoundRecord, SpinRecord)>>([.. Spins.Where(s => s.Win > 0 && !s.WinPaid).Select(s => (Rounds[s.RoundId], s))]);
}

/// <summary>Shared Crash rounds and their bets in memory, over a FakeStore for the bets' own rounds.</summary>
public sealed class FakeCrashStore(FakeStore? rounds = null) : ICrashStore
{
    public FakeStore Store { get; set; } = rounds ?? new FakeStore();
    public Dictionary<Guid, CrashRound> Tables { get; } = [];
    public Dictionary<Guid, (Guid Table, long? Auto)> Bets { get; } = [];

    /// <summary>Returns every bet of a table as open, as a read taken just before a cash-out committed would.</summary>
    public bool StaleOpenBets { get; set; }

    public Task<long> LastSeqAsync(CancellationToken cancellationToken) => Task.FromResult(Tables.Count == 0 ? 0L : Tables.Values.Max(t => t.Seq));

    public Task InsertAsync(CrashRound round, CancellationToken cancellationToken)
    {
        Tables[round.Id] = round;
        return Task.CompletedTask;
    }

    public Task SetPhaseAsync(Guid id, string phase, DateTimeOffset? crashedAt, DateTimeOffset? bettingUntil, CancellationToken cancellationToken)
    {
        Tables[id] = Tables[id] with { Phase = phase, CrashedAt = crashedAt, BettingUntil = bettingUntil ?? Tables[id].BettingUntil };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CrashRound>> UnfinishedAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CrashRound>>([.. Tables.Values.Where(t => t.Phase is CrashPhase.Betting or CrashPhase.Flying
            || (t.Phase == CrashPhase.Crashed && Bets.Any(b => b.Value.Table == t.Id && Store.Rounds[b.Key].Status == RoundStatus.Open)))]);

    public Task<IReadOnlyList<CrashRound>> HistoryAsync(int count, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CrashRound>>([.. Tables.Values.Where(t => t.Phase == CrashPhase.Crashed).OrderByDescending(t => t.Seq).Take(count)]);

    public async Task InsertBetAsync(RoundRecord round, SpinRecord spin, Guid table, long? autoBp, CancellationToken cancellationToken)
    {
        await Store.InsertAsync(round, spin, cancellationToken);
        Bets[round.RoundId] = (table, autoBp);
    }

    public Task<IReadOnlyList<CrashBetRecord>> OpenBetsAsync(Guid table, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CrashBetRecord>>([.. Bets.Where(b => b.Value.Table == table && (StaleOpenBets || Store.Rounds[b.Key].Status == RoundStatus.Open)).Select(b => new CrashBetRecord(Store.Rounds[b.Key], b.Value.Table, b.Value.Auto))]);

    public Task<IReadOnlyList<CrashBetRecord>> PlayerBetsAsync(Guid table, string playerRef, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CrashBetRecord>>([.. Bets.Where(b => b.Value.Table == table && Store.Rounds[b.Key].PlayerRef == playerRef && Store.Rounds[b.Key].Status != RoundStatus.Refused)
            .Select(b => new CrashBetRecord(Store.Rounds[b.Key], b.Value.Table, b.Value.Auto))]);

    public Task<CrashBetRecord?> BetAsync(Guid roundId, CancellationToken cancellationToken) =>
        Task.FromResult(Bets.TryGetValue(roundId, out var b) ? new CrashBetRecord(Store.Rounds[roundId], b.Table, b.Auto) : null);
}

public sealed class FakeBroadcast : ICrashBroadcast
{
    public List<(string Kind, object Payload)> Sent { get; } = [];

    public Task PublishAsync(string kind, object payload, CancellationToken cancellationToken)
    {
        Sent.Add((kind, payload));
        return Task.CompletedTask;
    }
}

/// <summary>Seeds in memory; nonces count up from 0.</summary>
public sealed class FakeSeeds : ISeedStore
{
    public PlayerSeeds Seeds { get; set; } = new("server-seed", SwiftBets.Originals.Domain.Instant.Fair.Hash("server-seed"), "client", 0, null, null, null);

    public Task<PlayerSeeds> GetAsync(string playerRef, CancellationToken cancellationToken) => Task.FromResult(Seeds);

    public Task<PlayerSeeds> TakeNonceAsync(string playerRef, CancellationToken cancellationToken)
    {
        var taken = Seeds;
        Seeds = Seeds with { Nonce = Seeds.Nonce + 1 };
        return Task.FromResult(taken);
    }

    public Task<PlayerSeeds?> RotateAsync(string playerRef, string clientSeed, CancellationToken cancellationToken)
    {
        Seeds = new("server-seed-2", SwiftBets.Originals.Domain.Instant.Fair.Hash("server-seed-2"), clientSeed, 0, Seeds.ServerSeed, Seeds.ClientSeed, Seeds.Nonce);
        return Task.FromResult<PlayerSeeds?>(Seeds);
    }
}

/// <summary>The real catalogue plus a small locking game, drawn by a scripted sequence (0 when it runs out).</summary>
public static class TestGames
{
    public const string Locking = "lock-test";

    public static SlotEngines Engines(params int[] draws)
    {
        var next = 0;
        return new SlotEngines(new GameCatalog([GildedSavanna.Game, LockingGame]), _ => next < draws.Length ? draws[next++] : 0);
    }

    /// <summary>One line (top row). Free strips Gold2, Coal, Coal, Rock: stop 0 shows Gold2 on top, stop 1 does not.</summary>
    private static readonly SlotGame LockingGame = BuildLockingGame();

    private static SlotGame BuildLockingGame()
    {
        SymbolDef[] symbols = [new("Gold"), new("Gold2", Weight: 2, PaysAs: "Gold"), new("Coal"), new("Rock"), new("Bomb", SymbolKind.Scatter)];
        IReadOnlyList<byte> free = [1, 2, 2, 3];
        IReadOnlyList<byte> baseStrip = [2, 3, 2];
        var definition = new GameDefinition(Locking, symbols, [baseStrip, baseStrip, baseStrip, baseStrip, baseStrip], [free, free, free, free, free],
            [[0, 0, 0, 0, 0]], new Dictionary<byte, int[]> { [0] = [1, 2, 3, 4, 5, 6, 7, 8] }, [1, 5, 25], new FeatureRules(8, 1, Retrigger: false, LockSplits: true), 5000);
        return new SlotGame(definition, new GameProfile("Lock test", 96m, [100]));
    }
}
