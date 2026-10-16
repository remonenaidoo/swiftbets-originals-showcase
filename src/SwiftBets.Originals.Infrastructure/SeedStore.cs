using Dapper;
using Npgsql;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Infrastructure;

/// <summary>Provably-fair seeds per player in <c>originals.player_seeds</c>.</summary>
public sealed class SeedStore(NpgsqlDataSource db) : ISeedStore
{
    private const string Columns = "server_seed, server_seed_hash, client_seed, nonce, previous_server_seed, previous_client_seed, previous_nonce";

    public async Task<PlayerSeeds> GetAsync(string playerRef, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await EnsureAsync(c, playerRef, cancellationToken);
        return (await c.QuerySingleAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM originals.player_seeds WHERE player_ref = @playerRef", new { playerRef }, cancellationToken: cancellationToken))).ToRecord();
    }

    public async Task<PlayerSeeds> TakeNonceAsync(string playerRef, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await EnsureAsync(c, playerRef, cancellationToken);
        var row = await c.QuerySingleAsync<Row>(new CommandDefinition(
            $"UPDATE originals.player_seeds SET nonce = nonce + 1 WHERE player_ref = @playerRef RETURNING {Columns}", new { playerRef }, cancellationToken: cancellationToken));
        return row.ToRecord() with { Nonce = row.nonce - 1 };
    }

    public async Task<PlayerSeeds> RotateAsync(string playerRef, string clientSeed, CancellationToken cancellationToken)
    {
        await using var c = await db.OpenConnectionAsync(cancellationToken);
        await EnsureAsync(c, playerRef, cancellationToken);
        var seed = Fair.NewSeed();
        return (await c.QuerySingleAsync<Row>(new CommandDefinition(
            $"""
            UPDATE originals.player_seeds SET previous_server_seed = server_seed, previous_client_seed = client_seed, previous_nonce = nonce,
                server_seed = @seed, server_seed_hash = @hash, client_seed = @clientSeed, nonce = 0, rotated_at = now()
            WHERE player_ref = @playerRef RETURNING {Columns}
            """, new { playerRef, seed, hash = Fair.Hash(seed), clientSeed }, cancellationToken: cancellationToken))).ToRecord();
    }

    private static Task<int> EnsureAsync(NpgsqlConnection c, string playerRef, CancellationToken cancellationToken)
    {
        var seed = Fair.NewSeed();
        return c.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO originals.player_seeds (player_ref, server_seed, server_seed_hash, client_seed)
            VALUES (@playerRef, @seed, @hash, @client) ON CONFLICT (player_ref) DO NOTHING
            """, new { playerRef, seed, hash = Fair.Hash(seed), client = Fair.NewSeed()[..16] }, cancellationToken: cancellationToken));
    }

#pragma warning disable IDE1006, SA1300, CA1812 // Dapper maps snake_case columns onto these.
    private sealed class Row
    {
        public string server_seed { get; set; } = string.Empty;
        public string server_seed_hash { get; set; } = string.Empty;
        public string client_seed { get; set; } = string.Empty;
        public long nonce { get; set; }
        public string? previous_server_seed { get; set; }
        public string? previous_client_seed { get; set; }
        public long? previous_nonce { get; set; }

        public PlayerSeeds ToRecord() => new(server_seed, server_seed_hash, client_seed, nonce, previous_server_seed, previous_client_seed, previous_nonce);
    }
#pragma warning restore IDE1006, SA1300, CA1812
}
