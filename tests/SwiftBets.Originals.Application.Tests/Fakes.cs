using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Application.Tests;

/// <summary>A casino that answers each action with a scripted status and records every call.</summary>
public sealed class FakeWallet : IWallet
{
    public long Balance { get; set; } = 100_000;
    public string? PlayerRef { get; set; } = "player-1";
    public Dictionary<string, string> Replies { get; } = new() { ["balance"] = WalletStatus.Ok, ["bet"] = WalletStatus.Ok, ["win"] = WalletStatus.Ok, ["rollback"] = WalletStatus.Ok };
    public List<(string Action, string TransactionId, long Amount, string? Reference)> Calls { get; } = [];

    public Task<WalletReply> BalanceAsync(string sessionToken, CancellationToken cancellationToken) => Reply("balance", "", 0, null);

    public Task<WalletReply> BetAsync(string sessionToken, string transactionId, string roundId, long amount, CancellationToken cancellationToken) => Reply("bet", transactionId, amount, null);

    public Task<WalletReply> WinAsync(string sessionToken, string transactionId, string roundId, long amount, CancellationToken cancellationToken) => Reply("win", transactionId, amount, null);

    public Task<WalletReply> RollbackAsync(string sessionToken, string transactionId, string roundId, string betTransactionId, CancellationToken cancellationToken) => Reply("rollback", transactionId, 0, betTransactionId);

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

public sealed class FakeStore : IRoundStore
{
    public Dictionary<Guid, RoundRecord> Rounds { get; } = [];
    public List<SpinRecord> Spins { get; } = [];

    public Task<RoundRecord?> FindByKeyAsync(string playerRef, string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(Rounds.Values.FirstOrDefault(r => r.PlayerRef == playerRef && r.IdempotencyKey == idempotencyKey));

    public Task<RoundRecord?> FindOpenFeatureAsync(string playerRef, CancellationToken cancellationToken) =>
        Task.FromResult(Rounds.Values.FirstOrDefault(r => r.PlayerRef == playerRef && r.Status == RoundStatus.Feature));

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

    public Task<IReadOnlyList<RoundRecord>> RecentAsync(string playerRef, int count, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RoundRecord>>([.. Rounds.Values.Where(r => r.PlayerRef == playerRef).Take(count)]);

    public Task<IReadOnlyList<(RoundRecord Round, SpinRecord Spin)>> UnpaidWinsAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<(RoundRecord, SpinRecord)>>([.. Spins.Where(s => s.Win > 0 && !s.WinPaid).Select(s => (Rounds[s.RoundId], s))]);
}
