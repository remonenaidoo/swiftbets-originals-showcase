using SwiftBets.Originals.Application.Instant;

namespace SwiftBets.Originals.Api.Endpoints;

/// <summary>The instant games' API under <c>/instant</c>, and the player's provably-fair seeds. The session token is the credential, as for the slots.</summary>
public static class InstantEndpoints
{
    public sealed record StartBody(string? Session, long Stake, int Mines, string? Key);

    public sealed record PickBody(string? Session, Guid RoundId, int Tile);

    public sealed record RoundBody(string? Session, Guid RoundId);

    public sealed record SeedBody(string? Session, string? ClientSeed);

    public sealed record PlayBody(string? Session, long Stake, System.Text.Json.JsonElement Choices, string? Key);

    public sealed record TowersStartBody(string? Session, long Stake, string? Level, string? Key);

    public sealed record CrashBetBody(string? Session, long Stake, long? AutoBp, string? Key);

    public static IEndpointRouteBuilder MapInstantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var instant = endpoints.MapGroup("/instant").AllowAnonymous();
        instant.MapPost("/seed", async (SeedBody body, InstantQueries queries, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await queries.RotateAsync(body.Session ?? string.Empty, body.ClientSeed ?? string.Empty, cancellationToken), context));

        var mines = instant.MapGroup("/mines");
        mines.MapGet("/state", async (string? session, InstantQueries queries, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await queries.MinesAsync(session ?? string.Empty, cancellationToken), context));
        mines.MapPost("/start", async (StartBody body, MinesHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await handler.StartAsync(new MinesStart(body.Session ?? string.Empty, body.Stake, body.Mines, body.Key ?? string.Empty), cancellationToken), context));
        mines.MapPost("/pick", async (PickBody body, MinesHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await handler.PickAsync(body.Session ?? string.Empty, body.RoundId, body.Tile, cancellationToken), context));
        mines.MapPost("/cashout", async (RoundBody body, MinesHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await handler.CashoutAsync(body.Session ?? string.Empty, body.RoundId, cancellationToken), context));

        var towers = instant.MapGroup("/towers");
        towers.MapPost("/start", async (TowersStartBody body, TowersHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await handler.StartAsync(new TowersStart(body.Session ?? string.Empty, body.Stake, body.Level ?? string.Empty, body.Key ?? string.Empty), cancellationToken), context));
        towers.MapPost("/pick", async (PickBody body, TowersHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await handler.PickAsync(body.Session ?? string.Empty, body.RoundId, body.Tile, cancellationToken), context));
        towers.MapPost("/cashout", async (RoundBody body, TowersHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await handler.CashoutAsync(body.Session ?? string.Empty, body.RoundId, cancellationToken), context));

        // Dice, Limbo, Peg Drop, Keno (one draw each) and Towers' page data.
        instant.MapPost("/{gameId}/play", async (string gameId, PlayBody body, OneShotHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await handler.PlayAsync(gameId, body.Session ?? string.Empty, body.Stake, body.Choices, body.Key ?? string.Empty, cancellationToken), context));
        instant.MapGet("/{gameId}/state", async (string gameId, string? session, InstantQueries queries, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await queries.StateAsync(gameId, session ?? string.Empty, cancellationToken), context));

        var crash = instant.MapGroup("/crash");
        crash.MapGet("/state", async (string? session, InstantQueries queries, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await queries.CrashAsync(session ?? string.Empty, cancellationToken), context));
        crash.MapPost("/bet", async (CrashBetBody body, CrashRoom room, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await room.BetAsync(new CrashBet(body.Session ?? string.Empty, body.Stake, body.AutoBp, body.Key ?? string.Empty), cancellationToken), context));
        crash.MapPost("/cashout", async (RoundBody body, CrashRoom room, HttpContext context, CancellationToken cancellationToken) =>
            SlotEndpoints.Respond(await room.CashoutAsync(body.Session ?? string.Empty, body.RoundId, cancellationToken), context));
        return endpoints;
    }
}
