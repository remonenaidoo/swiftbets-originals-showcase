using Microsoft.AspNetCore.SignalR;
using SwiftBets.Originals.Application.Instant;
using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Api.Hubs;

/// <summary>
/// The shared Crash round, pushed to every viewer: anonymous and receive-only (bets and cash-outs go over HTTP with
/// the session). Messages: <c>round</c>, <c>tick</c>, <c>bet</c>, <c>cashout</c>, <c>crashed</c>, <c>history</c>, <c>board</c>. A new connection
/// gets the current round and the history at once.
/// </summary>
public sealed class CrashHub(CrashRoom room) : Hub
{
    public const string Path = "/hubs/crash";

    public override async Task OnConnectedAsync()
    {
        if (room.Public() is { } round)
        {
            await Clients.Caller.SendAsync("round", round);
        }

        await Clients.Caller.SendAsync("history", await room.HistoryAsync(Context.ConnectionAborted));
        await Clients.Caller.SendAsync("board", room.Board());
        await base.OnConnectedAsync();
    }
}

public sealed class SignalRCrashBroadcast(IHubContext<CrashHub> hub) : ICrashBroadcast
{
    public Task PublishAsync(string kind, object payload, CancellationToken cancellationToken) => hub.Clients.All.SendAsync(kind, payload, cancellationToken);
}
