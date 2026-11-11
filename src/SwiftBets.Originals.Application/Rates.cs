using Microsoft.Extensions.Options;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application;

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
        return percent is >= Lowest and <= Highest ? (long)Math.Round(percent * 100) : Multipliers.Base;
    }
}

/// <summary>One rate for every game: the design rate unless told otherwise (tests, tools).</summary>
public sealed class FixedRates(long rateBp = Multipliers.Base) : IGameRates
{
    public static readonly FixedRates Design = new();

    public long For(string gameId) => rateBp;
}
