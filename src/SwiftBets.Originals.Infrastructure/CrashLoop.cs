using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using SwiftBets.Originals.Application.Instant;

namespace SwiftBets.Originals.Infrastructure;

/// <summary>
/// Runs the shared Crash round. Only the instance holding a Postgres advisory lock runs it, so a second instance
/// never runs a second round; the lock is held by one connection for the loop's life and released if it dies.
/// </summary>
public sealed partial class CrashLoop(NpgsqlDataSource db, CrashRoom room, ILogger<CrashLoop> logger) : BackgroundService
{
    private const long LockKey = 0x5B_C2A5_0001;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var c = await db.OpenConnectionAsync(stoppingToken);
                await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", c);
                cmd.Parameters.AddWithValue("key", LockKey);
                if (await cmd.ExecuteScalarAsync(stoppingToken) is true)
                {
                    await room.RunAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                LoopFailed(logger, e);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The Crash loop stopped; it restarts, refunding the interrupted round.")]
    private static partial void LoopFailed(ILogger logger, Exception e);
}
