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

    public Task<WalletReply> RollbackAsync(string sessionToken, string gameId, string transactionId, string roundId, string betTransactionId, CancellationToken cancellationToken) => Reply("rollback", transactionId, 0, betTransactionId);

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

    public Task SettleIfPaidAsync(Guid roundId, CancellationToken cancellationToken)
    {
        if (Rounds[roundId].Status == RoundStatus.WinPending && !Spins.Any(s => s.RoundId == roundId && s.Win > 0 && !s.WinPaid))
        {
            Rounds[roundId] = Rounds[roundId] with { Status = RoundStatus.Settled };
        }

        return Task.CompletedTask;
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
