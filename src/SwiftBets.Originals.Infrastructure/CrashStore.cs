using Dapper;
using Npgsql;
using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Infrastructure;

/// <summary>Crash's shared rounds (<c>originals.crash_rounds</c>) and its bets (round rows with <c>table_round</c>).</summary>
public sealed class CrashStore(NpgsqlDataSource db, IRoundStore rounds) : ICrashStore
{
    private const string Columns = "id, seq, seed, seed_hash, point_bp, phase, betting_until, crashed_at";

    public async Task<long> LastSeqAsync(CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        return await c.ExecuteScalarAsync<long>(new CommandDefinition("SELECT coalesce(max(seq), 0) FROM originals.crash_rounds", cancellationToken: cancellationToken));
    }

    public async Task InsertAsync(CrashRound round, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO originals.crash_rounds (id, seq, seed, seed_hash, point_bp, phase, betting_until)
            VALUES (@Id, @Seq, @Seed, @SeedHash, @PointBp, @Phase, @BettingUntil)
            """, round, cancellationToken: cancellationToken));
    }

    public async Task SetPhaseAsync(Guid id, string phase, DateTimeOffset? crashedAt, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE originals.crash_rounds SET phase = @phase, crashed_at = coalesce(@crashedAt, crashed_at) WHERE id = @id",
            new { id, phase, crashedAt }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CrashRound>> UnfinishedAsync(CancellationToken cancellationToken) =>
        await QueryAsync($"SELECT {Columns} FROM originals.crash_rounds WHERE phase IN ('betting', 'flying') ORDER BY seq", null, cancellationToken);

    public async Task<IReadOnlyList<CrashRound>> HistoryAsync(int count, CancellationToken cancellationToken) =>
        await QueryAsync($"SELECT {Columns} FROM originals.crash_rounds WHERE phase = 'crashed' ORDER BY seq DESC LIMIT @count", new { count }, cancellationToken);

    public async Task InsertBetAsync(RoundRecord round, SpinRecord spin, Guid table, long? autoBp, CancellationToken cancellationToken)
    {
        await rounds.InsertAsync(round, spin, cancellationToken);
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE originals.rounds SET table_round = @table, auto_bp = @autoBp WHERE round_id = @RoundId",
            new { round.RoundId, table, autoBp }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CrashBetRecord>> OpenBetsAsync(Guid table, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var rows = await c.QueryAsync<(Guid round_id, long? auto_bp)>(new CommandDefinition(
            "SELECT round_id, auto_bp FROM originals.rounds WHERE table_round = @table AND status = 'open'", new { table }, cancellationToken: cancellationToken));
        var bets = new List<CrashBetRecord>();
        foreach (var (id, auto) in rows)
        {
            if (await rounds.GetAsync(id, cancellationToken) is { } round)
            {
                bets.Add(new CrashBetRecord(round, table, auto));
            }
        }

        return bets;
    }

    public async Task<CrashBetRecord?> BetAsync(Guid roundId, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var row = await c.QuerySingleOrDefaultAsync<(Guid? table_round, long? auto_bp)>(new CommandDefinition(
            "SELECT table_round, auto_bp FROM originals.rounds WHERE round_id = @roundId", new { roundId }, cancellationToken: cancellationToken));
        return row.table_round is { } table && await rounds.GetAsync(roundId, cancellationToken) is { } round ? new CrashBetRecord(round, table, row.auto_bp) : null;
    }

    private async Task<IReadOnlyList<CrashRound>> QueryAsync(string sql, object? args, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        var rows = await c.QueryAsync<Row>(new CommandDefinition(sql, args, cancellationToken: cancellationToken));
        return [.. rows.Select(r => new CrashRound(r.id, r.seq, r.seed, r.seed_hash, r.point_bp, r.phase, Utc(r.betting_until), r.crashed_at is { } at ? Utc(at) : null))];
    }

    private static DateTimeOffset Utc(DateTime t) => new(DateTime.SpecifyKind(t, DateTimeKind.Utc));

#pragma warning disable IDE1006, SA1300, CA1812 // Dapper maps snake_case columns onto these.
    private sealed class Row
    {
        public Guid id { get; set; }
        public long seq { get; set; }
        public string seed { get; set; } = string.Empty;
        public string seed_hash { get; set; } = string.Empty;
        public long point_bp { get; set; }
        public string phase { get; set; } = string.Empty;
        public DateTime betting_until { get; set; }
        public DateTime? crashed_at { get; set; }
    }
#pragma warning restore IDE1006, SA1300, CA1812
}
