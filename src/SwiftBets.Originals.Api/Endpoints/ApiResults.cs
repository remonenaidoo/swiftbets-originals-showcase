using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Originals.Application;

namespace SwiftBets.Originals.Api.Endpoints;

internal static class ApiResults
{
    public static IResult Respond<T>(Result<T> result, HttpContext context) =>
        result.Failed ? ToError(result.Error).ToHttpResult(context) : Results.Ok(result.Value);

    private static Error ToError(GameError e) => e.Code switch
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
        "betting_closed" => new Error(e.Code, "Betting has closed for this round. Bet on the next one.", ErrorKind.Conflict),
        "not_flying" => new Error(e.Code, "The round has not started yet.", ErrorKind.Conflict),
        "guess_not_allowed" => new Error(e.Code, "That guess is not open on this card.", ErrorKind.Conflict),
        _ => new Error(e.Code, "The wallet is unavailable. Try again.", ErrorKind.Unavailable),
    };
}
