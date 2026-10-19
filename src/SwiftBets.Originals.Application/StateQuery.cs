using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

/// <summary>What a game loads with: the balance, an unfinished feature of this game to resume, its last 50 rounds, and the rules.</summary>
public sealed class StateQuery(IRoundStore store, IWallet wallet, SlotEngines games, IGameRates? rates = null)
{
    public async Task<Result<StateView>> GetAsync(string gameId, string sessionToken, CancellationToken cancellationToken)
    {
        if (games.Find(gameId) is not var (slot, _))
        {
            return SpinError.GameNotFound;
        }

        var game = slot.Definition;
        var rate = (rates ?? FixedRates.Design).For(game.GameId);
        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        SpinView? open = null;
        if (await store.FindOpenFeatureAsync(playerRef!, game.GameId, cancellationToken) is { } round)
        {
            var spins = await store.SpinsAsync(round.RoundId, cancellationToken);
            open = Play.View(round, spins[^1], game, balance, rate);
        }

        var recent = await store.RecentAsync(playerRef!, game.GameId, 50, cancellationToken);
        return new StateView(
            balance,
            open,
            [.. recent.Select(r => new RoundSummary(r.RoundId, r.Stake, Play.Money(r.PaidUnits, r.Stake, game.LineCount, rate), r.Status, r.CreatedAt))],
            Info(slot, rate));
    }

    public static GameInfo Info(SlotGame slot, long rateBp = Domain.Instant.Mult.Base)
    {
        var game = slot.Definition;
        return new GameInfo(
            game.GameId,
            slot.Profile.Name,
            rateBp,
            game.MaxWinStakes,
            slot.Profile.Stakes,
            game.LineCount,
            [.. game.Symbols.Select(s => new SymbolInfo(s.Name, s.Kind.ToString(), s.Weight, s.PaysAs ?? s.Name))],
            game.LinePays.ToDictionary(p => game.Name(p.Key), p => p.Value),
            game.ScatterPays,
            game.Feature.Awarded,
            game.Feature.Multiplier,
            game.Feature.Retrigger,
            game.Feature.LockSplits,
            game.Lines,
            [.. game.BaseReels.Select(r => r.Select(game.Name).ToArray())],
            [.. game.FreeReels.Select(r => r.Select(game.Name).ToArray())]);
    }
}
