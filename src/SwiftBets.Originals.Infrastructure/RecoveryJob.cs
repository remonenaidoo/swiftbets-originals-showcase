using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwiftBets.Originals.Application;

namespace SwiftBets.Originals.Infrastructure;

/// <summary>Runs <see cref="RoundRecovery"/> every 30 seconds: unconfirmed rollbacks, rounds stuck by a crash, owed wins.</summary>
public sealed partial class RecoveryJob(RoundRecovery recovery, ILogger<RecoveryJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await recovery.RunOnceAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Round recovery pass failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
