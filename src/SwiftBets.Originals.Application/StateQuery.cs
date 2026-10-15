using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

/// <summary>What the game loads with: the balance, an unfinished feature to resume, the last 50 rounds, and the rules.</summary>
public sealed class StateQuery(IRoundStore store, IWallet wallet)
{
    /// <summary>The RTP stated to players: the PAR sheet's total, to two decimals.</summary>
    public const decimal StatedRtp = 96.00m;

    public async Task<Result<StateView>> GetAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var game = GildedSavanna.Definition;
        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        SpinView? open = null;
        if (await store.FindOpenFeatureAsync(playerRef!, cancellationToken) is { } round)
        {
            var spins = await store.SpinsAsync(round.RoundId, cancellationToken);
            open = Play.View(round, spins[^1], game, balance);
        }

        var recent = await store.RecentAsync(playerRef!, 50, cancellationToken);
        return new StateView(
            balance,
            open,
            [.. recent.Select(r => new RoundSummary(r.RoundId, r.Stake, Play.Money(r.PaidUnits, r.Stake, game.LineCount), r.Status, r.CreatedAt))],
            Info(game));
    }

    public static GameInfo Info(GameDefinition game) => new(
        game.GameId,
        "Gilded Savanna",
        StatedRtp,
        game.MaxWinStakes,
        Play.Stakes,
        game.LineCount,
        game.LinePays.ToDictionary(p => p.Key.ToString(), p => p.Value),
        game.ScatterPays,
        game.FreeSpinsAwarded,
        game.FreeSpinMultiplier,
        game.Lines);
}
