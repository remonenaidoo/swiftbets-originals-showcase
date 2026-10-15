using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Originals.Application;

namespace SwiftBets.Originals.Api.Endpoints;

/// <summary>
/// The game's API, behind the public gateway at <c>/originals</c> (prefix stripped). The casino session token is the
/// credential: the casino validates it on every wallet call, so these endpoints are anonymous.
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
        endpoints.MapGet("/play", (string? session, IWebHostEnvironment env, HttpContext context) =>
        {
            if (string.IsNullOrEmpty(session))
            {
                return Results.BadRequest("session is required");
            }

            // The game page is HTML framed by the site, so it replaces the JSON API's baseline headers with its own:
            // only its own scripts and styles, only this origin for data, framed only by the same origin.
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.ContentSecurityPolicy = PagePolicy;
            context.Response.Headers.XFrameOptions = "SAMEORIGIN";
            return Results.File(Path.Combine(env.WebRootPath, "index.html"), "text/html; charset=utf-8");
        }).AllowAnonymous();

        endpoints.MapGet("/slot/state", async (string? session, StateQuery query, HttpContext context, CancellationToken cancellationToken) =>
            Respond(await query.GetAsync(session ?? string.Empty, cancellationToken), context)).AllowAnonymous();

        endpoints.MapPost("/slot/spin", async (SpinBody body, SpinHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            Respond(await handler.SpinAsync(new SpinRequest(body.Session ?? string.Empty, body.Stake, body.Key ?? string.Empty), cancellationToken), context)).AllowAnonymous();

        endpoints.MapPost("/slot/feature/next", async (NextBody body, FeatureHandler handler, HttpContext context, CancellationToken cancellationToken) =>
            Respond(await handler.NextAsync(body.Session ?? string.Empty, body.RoundId, cancellationToken), context)).AllowAnonymous();

        return endpoints;
    }

    private static IResult Respond<T>(Result<T> result, HttpContext context) =>
        result.Error is { } e ? ToError(e).ToHttpResult(context) : Results.Ok(result.Value);

    private static Error ToError(SpinError e) => e.Code switch
    {
        "invalid_stake" => new Error(e.Code, "That stake is not offered.", ErrorKind.Validation),
        "session_invalid" => new Error(e.Code, "Your game session has ended. Reopen the game.", ErrorKind.Unauthorized),
        "insufficient_funds" => new Error(e.Code, "Not enough balance for this stake.", ErrorKind.BusinessRule),
        "limit_exceeded" => new Error(e.Code, "You have reached a limit you set.", ErrorKind.Forbidden),
        "account_restricted" => new Error(e.Code, "Play is not available on this account right now.", ErrorKind.Forbidden),
        "round_not_found" => new Error(e.Code, "No such round.", ErrorKind.NotFound),
        "feature_in_progress" => new Error(e.Code, "Finish your free spins first.", ErrorKind.Conflict),
        "no_feature" => new Error(e.Code, "There are no free spins to play.", ErrorKind.Conflict),
        _ => new Error(e.Code, "The wallet is unavailable. Try again.", ErrorKind.Unavailable),
    };
}
