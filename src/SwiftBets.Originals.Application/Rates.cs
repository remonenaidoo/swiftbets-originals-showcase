using Microsoft.Extensions.Options;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application;

/// <summary>
/// Each game's RTP, set by the operator: <c>Originals:Rtp:Default</c> (percent) and per game
/// <c>Originals:Rtp:Games:{gameId}</c>. Read on every round, so a change applies from the next round; a round already
/// in play keeps the rate it started with.
/// </summary>
public sealed class RtpOptions
{
    public decimal Default { get; set; } = 80m;

    public Dictionary<string, decimal> Games { get; set; } = [];
}

/// <summary>The rate a game plays at now, in basis points (8,000 = 80%).</summary>
public interface IGameRates
{
    long For(string gameId);
}

public sealed class GameRates(IOptionsMonitor<RtpOptions> options) : IGameRates
{
    /// <summary>The range a setting may take; anything outside (a typo) plays at the design rate instead of paying wildly.</summary>
    public const decimal Lowest = 50m, Highest = 99.5m;

    public long For(string gameId)
    {
        var o = options.CurrentValue;
        var percent = o.Games.TryGetValue(gameId, out var game) ? game : o.Default;
        return percent is >= Lowest and <= Highest ? (long)Math.Round(percent * 100) : Mult.Base;
    }
}

/// <summary>One rate for every game: the design rate unless told otherwise (tests, tools).</summary>
public sealed class FixedRates(long rateBp = Mult.Base) : IGameRates
{
    public static readonly FixedRates Design = new();

    public long For(string gameId) => rateBp;
}
