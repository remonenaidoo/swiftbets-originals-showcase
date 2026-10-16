using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Originals.Application;
using SwiftBets.Originals.Application.Instant;

namespace SwiftBets.Originals.Api.Endpoints;

/// <summary>
/// The games' API, behind the public gateway at <c>/originals</c> (prefix stripped), one set of routes per game id.
/// The casino session token is the credential: the casino validates it on every wallet call, so these endpoints are
/// anonymous.
/// </summary>
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

            // The game page is HTML framed by the site, so it replaces the JSON API's baseline headers with its own:
            // only its own scripts and styles, only this origin for data, framed only by the same origin.
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.ContentSecurityPolicy = PagePolicy;
            context.Response.Headers.XFrameOptions = "SAMEORIGIN";
            return Results.File(Path.Combine(env.WebRootPath, "games", game!, "index.html"), "text/html; charset=utf-8");
        }).AllowAnonymous();

        var game = endpoints.MapGroup("/games/{gameId}").AllowAnonymous();
        game.MapGet("/state", async (string gameId, string? session, StateQuery query, HttpContext context, CancellationToken cancellationToken) =>
            Respond(await query.GetAsync(gameId, session ?? string.Empty, cancellationToken), context));

        game.MapPost("/spin", async (string gameId, SpinBody body, SpinHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            Respond(await handler.SpinAsync(new SpinRequest(gameId, body.Session ?? string.Empty, body.Stake, body.Key ?? string.Empty), cancellationToken), context));

        game.MapPost("/feature/next", async (string gameId, NextBody body, FeatureHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            Respond(await handler.NextAsync(gameId, body.Session ?? string.Empty, body.RoundId, cancellationToken), context));

        return endpoints;
    }

    internal static IResult Respond<T>(Result<T> result, HttpContext context) =>
        result.Error is { } e ? ToError(e).ToHttpResult(context) : Results.Ok(result.Value);

    private static Error ToError(SpinError e) => e.Code switch
    {
        "invalid_stake" => new Error(e.Code, "That stake is not offered.", ErrorKind.Validation),
        "session_invalid" => new Error(e.Code, "Your game session has ended. Reopen the game.", ErrorKind.Unauthorized),
        "insufficient_funds" => new Error(e.Code, "Not enough balance for this stake.", ErrorKind.BusinessRule),
        "limit_exceeded" => new Error(e.Code, "You have reached a limit you set.", ErrorKind.Forbidden),
        "account_restricted" => new Error(e.Code, "Play is not available on this account right now.", ErrorKind.Forbidden),
        "game_not_found" => new Error(e.Code, "No such game.", ErrorKind.NotFound),
        "round_not_found" => new Error(e.Code, "No such round.", ErrorKind.NotFound),
        "feature_in_progress" => new Error(e.Code, "Finish your free spins first.", ErrorKind.Conflict),
        "key_in_use" => new Error(e.Code, "That request key was already used for another game.", ErrorKind.Conflict),
        "no_feature" => new Error(e.Code, "There are no free spins to play.", ErrorKind.Conflict),
        "invalid_seed" => new Error(e.Code, "A client seed is 1 to 64 letters, digits, - or _.", ErrorKind.Validation),
        "round_in_progress" => new Error(e.Code, "Finish the round you are playing first.", ErrorKind.Conflict),
        "round_over" => new Error(e.Code, "That round is already over.", ErrorKind.Conflict),
        "tile_taken" => new Error(e.Code, "Pick a tile you have not revealed.", ErrorKind.Validation),
        "nothing_to_cash_out" => new Error(e.Code, "Reveal a tile before cashing out.", ErrorKind.Conflict),
        _ => new Error(e.Code, "The wallet is unavailable. Try again.", ErrorKind.Unavailable),
    };
}
