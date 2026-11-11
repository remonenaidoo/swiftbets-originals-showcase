using System.Diagnostics.CodeAnalysis;

namespace SwiftBets.Originals.Application;

/// <summary>A refusal; the API turns <see cref="Code"/> into its error envelope.</summary>
public sealed record GameError(string Code)
{
    public static readonly GameError InvalidStake = new("invalid_stake");
    public static readonly GameError SessionInvalid = new("session_invalid");
    public static readonly GameError InsufficientFunds = new("insufficient_funds");
    public static readonly GameError LimitExceeded = new("limit_exceeded");
    public static readonly GameError AccountRestricted = new("account_restricted");
    public static readonly GameError GameNotFound = new("game_not_found");
    public static readonly GameError RoundNotFound = new("round_not_found");
    public static readonly GameError FeatureInProgress = new("feature_in_progress");
    public static readonly GameError KeyInUse = new("key_in_use");
    public static readonly GameError NoFeature = new("no_feature");
    public static readonly GameError InvalidSeed = new("invalid_seed");
    public static readonly GameError BettingClosed = new("betting_closed");
    public static readonly GameError NotFlying = new("not_flying");
    public static readonly GameError RoundInProgress = new("round_in_progress");
    public static readonly GameError RoundOver = new("round_over");
    public static readonly GameError TileTaken = new("tile_taken");
    public static readonly GameError NothingToCashOut = new("nothing_to_cash_out");
    public static readonly GameError GuessNotAllowed = new("guess_not_allowed");
    public static readonly GameError WalletUnavailable = new("wallet_unavailable");
}

public sealed record Result<T>(T? Value, GameError? Error)
{
    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool Failed => Error is not null;

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(GameError error) => new(default, error);
}
