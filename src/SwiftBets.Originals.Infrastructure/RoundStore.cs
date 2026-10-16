using Dapper;
using Npgsql;
using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Infrastructure;

/// <summary>Rounds and spins in Postgres (<c>sb_originals</c>, schema <c>originals</c>).</summary>
public sealed class RoundStore(NpgsqlDataSource db) : IRoundStore
{
    private const string RoundColumns = "round_id, player_ref, session_token, game_id, stake, idempotency_key, status, free_spins_left, paid_units, created_at";
    private const string SpinColumns = "round_id, seq, stops, outcome::text AS outcome, win, wallet_bet_tx, wallet_win_tx, win_paid";

    public async Task<RoundRecord?> FindByKeyAsync(string playerRef, string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var row = await c.QuerySingleOrDefaultAsync<RoundRow>(new CommandDefinition(
            $"SELECT {RoundColumns} FROM originals.rounds WHERE player_ref = @playerRef AND idempotency_key = @idempotencyKey",
            new { playerRef, idempotencyKey }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    public async Task<RoundRecord?> FindOpenFeatureAsync(string playerRef, string gameId, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var row = await c.QueryFirstOrDefaultAsync<RoundRow>(new CommandDefinition(
            $"SELECT {RoundColumns} FROM originals.rounds WHERE player_ref = @playerRef AND game_id = @gameId AND status = 'feature' ORDER BY created_at LIMIT 1",
            new { playerRef, gameId }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    public async Task<RoundRecord?> GetAsync(Guid roundId, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var row = await c.QuerySingleOrDefaultAsync<RoundRow>(new CommandDefinition(
            $"SELECT {RoundColumns} FROM originals.rounds WHERE round_id = @roundId", new { roundId }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    public async Task InsertAsync(RoundRecord round, SpinRecord spin, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO originals.rounds (round_id, player_ref, session_token, game_id, stake, idempotency_key, status, free_spins_left, paid_units, created_at)
            VALUES (@RoundId, @PlayerRef, @SessionToken, @GameId, @Stake, @IdempotencyKey, @Status, @FreeSpinsLeft, @PaidUnits, @CreatedAt)
            """, round, tx, cancellationToken: cancellationToken));
        await InsertSpinAsync(c, tx, spin, cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task InsertSpinAsync(SpinRecord spin, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await InsertSpinAsync(c, null, spin, cancellationToken);
    }

    public async Task UpdateAsync(Guid roundId, string status, int freeSpinsLeft, long paidUnits, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition(
            """
            UPDATE originals.rounds SET status = @status, free_spins_left = @freeSpinsLeft, paid_units = @paidUnits,
                settled_at = CASE WHEN @status IN ('settled', 'refused', 'voided') THEN now() ELSE settled_at END
            WHERE round_id = @roundId
            """, new { roundId, status, freeSpinsLeft, paidUnits }, cancellationToken: cancellationToken));
    }

    public async Task MarkWinPaidAsync(Guid roundId, int seq, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE originals.spins SET win_paid = true WHERE round_id = @roundId AND seq = @seq", new { roundId, seq }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SpinRecord>> SpinsAsync(Guid roundId, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var rows = await c.QueryAsync<SpinRow>(new CommandDefinition(
            $"SELECT {SpinColumns} FROM originals.spins WHERE round_id = @roundId ORDER BY seq", new { roundId }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToRecord())];
    }

    public async Task<IReadOnlyList<RoundRecord>> RecentAsync(string playerRef, string gameId, int count, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var rows = await c.QueryAsync<RoundRow>(new CommandDefinition(
            $"SELECT {RoundColumns} FROM originals.rounds WHERE player_ref = @playerRef AND game_id = @gameId AND status NOT IN ('pending', 'refused') ORDER BY created_at DESC LIMIT @count",
            new { playerRef, gameId, count }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToRecord())];
    }

    public async Task<IReadOnlyList<(RoundRecord Round, SpinRecord Spin)>> UnpaidWinsAsync(int limit, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var spins = (await c.QueryAsync<SpinRow>(new CommandDefinition(
            $"""
            SELECT {SpinColumns.Replace("round_id", "s.round_id", StringComparison.Ordinal)} FROM originals.spins s
            JOIN originals.rounds r ON r.round_id = s.round_id
            WHERE s.win > 0 AND NOT s.win_paid AND r.status NOT IN ('pending', 'refused', 'voided')
            ORDER BY r.created_at LIMIT @limit
            """, new { limit }, cancellationToken: cancellationToken))).ToList();
        var result = new List<(RoundRecord, SpinRecord)>();
        foreach (var spin in spins)
        {
            if (await GetAsync(spin.round_id, cancellationToken) is { } round)
            {
                result.Add((round, spin.ToRecord()));
            }
        }

        return result;
    }

    public async Task<(RoundRecord Round, SpinRecord Spin)?> ClaimFreeSpinAsync(Guid roundId, string playerRef, string gameId, Func<RoundRecord, SpinRecord, (SpinRecord Spin, int Left, long PaidUnits)> play, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        var row = await c.QuerySingleOrDefaultAsync<RoundRow>(new CommandDefinition(
            $"SELECT {RoundColumns} FROM originals.rounds WHERE round_id = @roundId FOR UPDATE", new { roundId }, tx, cancellationToken: cancellationToken));
        if (row is null || row.player_ref != playerRef || row.game_id != gameId || row.status != RoundStatus.Feature || row.free_spins_left <= 0)
        {
            return null;
        }

        var round = row.ToRecord();
        var last = await c.QuerySingleAsync<SpinRow>(new CommandDefinition(
            $"SELECT {SpinColumns} FROM originals.spins WHERE round_id = @roundId ORDER BY seq DESC LIMIT 1", new { roundId }, tx, cancellationToken: cancellationToken));
        var (spin, left, paidUnits) = play(round, last.ToRecord());
        await InsertSpinAsync(c, tx, spin, cancellationToken);
        var status = left > 0 ? RoundStatus.Feature : RoundStatus.WinPending;
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE originals.rounds SET free_spins_left = @left, paid_units = @paidUnits, status = @status WHERE round_id = @roundId",
            new { roundId, left, paidUnits, status }, tx, cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return (round with { FreeSpinsLeft = left, PaidUnits = paidUnits, Status = status }, spin);
    }

    public async Task<RoundRecord?> FindOpenAsync(string playerRef, string gameId, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var row = await c.QueryFirstOrDefaultAsync<RoundRow>(new CommandDefinition(
            $"SELECT {RoundColumns} FROM originals.rounds WHERE player_ref = @playerRef AND game_id = @gameId AND status IN ('open', 'pending') ORDER BY created_at LIMIT 1",
            new { playerRef, gameId }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    public async Task<(RoundRecord Round, SpinRecord Spin)?> StepAsync(Guid roundId, string playerRef, string gameId, Func<RoundRecord, IReadOnlyList<SpinRecord>, (SpinRecord Spin, string Status, long Paid)?> step, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        var row = await c.QuerySingleOrDefaultAsync<RoundRow>(new CommandDefinition(
            $"SELECT {RoundColumns} FROM originals.rounds WHERE round_id = @roundId FOR UPDATE", new { roundId }, tx, cancellationToken: cancellationToken));
        if (row is null || row.player_ref != playerRef || row.game_id != gameId || row.status != RoundStatus.Open)
        {
            return null;
        }

        var round = row.ToRecord();
        var spins = (await c.QueryAsync<SpinRow>(new CommandDefinition(
            $"SELECT {SpinColumns} FROM originals.spins WHERE round_id = @roundId ORDER BY seq", new { roundId }, tx, cancellationToken: cancellationToken))).Select(r => r.ToRecord()).ToList();
        if (step(round, spins) is not var (spin, status, paid))
        {
            return null;
        }

        await InsertSpinAsync(c, tx, spin, cancellationToken);
        await c.ExecuteAsync(new CommandDefinition(
            """
            UPDATE originals.rounds SET status = @status, paid_units = @paid,
                settled_at = CASE WHEN @status = 'settled' THEN now() ELSE settled_at END
            WHERE round_id = @roundId
            """, new { roundId, status, paid }, tx, cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return (round with { Status = status, PaidUnits = paid }, spin);
    }

    public async Task SettleIfPaidAsync(Guid roundId, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition(
            """
            UPDATE originals.rounds SET status = 'settled', settled_at = now()
            WHERE round_id = @roundId AND status = 'win_pending'
              AND NOT EXISTS (SELECT 1 FROM originals.spins WHERE round_id = @roundId AND win > 0 AND NOT win_paid)
            """, new { roundId }, cancellationToken: cancellationToken));
    }

    public async Task SetRollbackPendingAsync(Guid roundId, bool pending, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE originals.rounds SET rollback_pending = @pending WHERE round_id = @roundId", new { roundId, pending }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<RoundRecord>> NeedingRollbackAsync(DateTimeOffset pendingBefore, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var rows = await c.QueryAsync<RoundRow>(new CommandDefinition(
            $"SELECT {RoundColumns} FROM originals.rounds WHERE (status = 'pending' AND created_at < @pendingBefore) OR rollback_pending ORDER BY created_at LIMIT 100",
            new { pendingBefore }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToRecord())];
    }

    private static Task<int> InsertSpinAsync(NpgsqlConnection c, NpgsqlTransaction? tx, SpinRecord spin, CancellationToken cancellationToken) =>
        c.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO originals.spins (round_id, seq, stops, outcome, win, wallet_bet_tx, wallet_win_tx, win_paid)
            VALUES (@RoundId, @Seq, @Stops, @OutcomeJson::jsonb, @Win, @BetTx, @WinTx, @WinPaid)
            """, spin, tx, cancellationToken: cancellationToken));

#pragma warning disable IDE1006, SA1300, CA1812 // Dapper maps snake_case columns onto these.
    private sealed class RoundRow
    {
        public Guid round_id { get; set; }
        public string player_ref { get; set; } = string.Empty;
        public string session_token { get; set; } = string.Empty;
        public string game_id { get; set; } = string.Empty;
        public long stake { get; set; }
        public string idempotency_key { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public int free_spins_left { get; set; }
        public long paid_units { get; set; }
        public DateTime created_at { get; set; }

        public RoundRecord ToRecord() => new(round_id, player_ref, session_token, game_id, stake, idempotency_key, status, free_spins_left, paid_units, new DateTimeOffset(DateTime.SpecifyKind(created_at, DateTimeKind.Utc)));
    }

    private sealed class SpinRow
    {
        public Guid round_id { get; set; }
        public int seq { get; set; }
        public int[] stops { get; set; } = [];
        public string outcome { get; set; } = "{}";
        public long win { get; set; }
        public string? wallet_bet_tx { get; set; }
        public string? wallet_win_tx { get; set; }
        public bool win_paid { get; set; }

        public SpinRecord ToRecord() => new(round_id, seq, stops, outcome, win, wallet_bet_tx, wallet_win_tx, win_paid);
    }
#pragma warning restore IDE1006, SA1300, CA1812
}
