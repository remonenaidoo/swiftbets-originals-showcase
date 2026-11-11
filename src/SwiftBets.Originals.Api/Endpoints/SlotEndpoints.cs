using SwiftBets.Originals.Application;
using SwiftBets.Originals.Application.Instant;

namespace SwiftBets.Originals.Api.Endpoints;

/// <summary>The slots' API behind the gateway at <c>/originals</c>; the casino session token is the credential, validated on every wallet call.</summary>
public static class SlotEndpoints
{
    public const string PagePolicy =
        "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
        "connect-src 'self'; worker-src 'self' blob:; base-uri 'none'; form-action 'none'; frame-ancestors 'self'";

    public sealed record SpinBody(string? Session, long Stake, string? Key);

    public sealed record NextBody(string? Session, Guid RoundId);

    public static IEndpointRouteBuilder MapSlotEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // The casino's launch link: /play?session=…&game=…, one page per game.
        endpoints.MapGet("/play", (string? session, string? game, SlotEngines games, IWebHostEnvironment env, HttpContext context) =>
        {
            if (string.IsNullOrEmpty(session))
            {
                return Results.BadRequest("session is required");
            }

            if (games.Find(game) is null && Array.IndexOf(InstantGames.Ids, game) < 0)
            {
                return Results.NotFound();
            }

            // The page replaces the JSON API's baseline headers with its own, framed only by the same origin.
            context.Response.Headers.CacheControl = "no-store";
            // Crash's live round is a websocket to this host; older Safari does not count wss: as 'self'.
            context.Response.Headers.ContentSecurityPolicy = PagePolicy.Replace("connect-src 'self'", $"connect-src 'self' wss://{context.Request.Host}", StringComparison.Ordinal);
            context.Response.Headers.XFrameOptions = "SAMEORIGIN";
            return Results.File(Path.Combine(env.WebRootPath, "games", game!, "index.html"), "text/html; charset=utf-8");
        }).AllowAnonymous();

        var game = endpoints.MapGroup("/games/{gameId}").AllowAnonymous();
        game.MapGet("/state", async (string gameId, string? session, StateQuery query, HttpContext context, CancellationToken cancellationToken) =>
            ApiResults.Respond(await query.GetAsync(gameId, session ?? string.Empty, cancellationToken), context));

        game.MapPost("/spin", async (string gameId, SpinBody body, SpinHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            ApiResults.Respond(await handler.SpinAsync(new SpinRequest(gameId, body.Session ?? string.Empty, body.Stake, body.Key ?? string.Empty), cancellationToken), context));

        game.MapPost("/feature/next", async (string gameId, NextBody body, FeatureHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            ApiResults.Respond(await handler.NextAsync(gameId, body.Session ?? string.Empty, body.RoundId, cancellationToken), context));

        return endpoints;
    }
}
