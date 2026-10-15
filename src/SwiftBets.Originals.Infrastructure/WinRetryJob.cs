using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Infrastructure;

/// <summary>
/// Pays wins the casino did not confirm at the time, every 30 seconds, under the same transaction id (the casino
/// applies each once). A round waiting only on such a win is settled once nothing more is owed.
/// </summary>
public sealed partial class WinRetryJob(IRoundStore store, IWallet wallet, ILogger<WinRetryJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PayOwedAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
    }

    public async Task PayOwedAsync(CancellationToken cancellationToken)
    {
        foreach (var (round, spin) in await store.UnpaidWinsAsync(100, cancellationToken))
        {
            var reply = await wallet.WinAsync(round.SessionToken, spin.WinTx!, round.RoundId.ToString(), spin.Win, cancellationToken);
            if (reply.Status != WalletStatus.Ok)
            {
                LogStillOwed(logger, round.RoundId, spin.Seq, reply.Status);
                continue;
            }

            await store.MarkWinPaidAsync(round.RoundId, spin.Seq, cancellationToken);
            var owed = (await store.SpinsAsync(round.RoundId, cancellationToken)).Any(s => s.Win > 0 && !s.WinPaid);
            if (round.Status == RoundStatus.WinPending && !owed)
            {
                await store.UpdateAsync(round.RoundId, RoundStatus.Settled, 0, round.PaidUnits, cancellationToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Win for round {RoundId} spin {Seq} still owed: {Status}")]
    private static partial void LogStillOwed(ILogger logger, Guid roundId, int seq, string status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Win retry pass failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
