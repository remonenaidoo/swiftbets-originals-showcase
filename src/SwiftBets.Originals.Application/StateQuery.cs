using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

public sealed class StateQuery(IRoundStore store, IWallet wallet, SlotEngines games, IGameRates rates)
{
    public async Task<Result<StateView>> GetAsync(string gameId, string sessionToken, CancellationToken cancellationToken)
    {
        if (games.Find(gameId) is not { } slot)
        {
            return GameError.GameNotFound;
        }

        var game = slot.Game.Definition;
        var rate = rates.For(game.GameId);
        var identity = await wallet.IdentifyAsync(sessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        var player = identity.Value;
        SpinView? openFeature = null;
        if (await store.FindOpenFeatureAsync(player.Ref, game.GameId, cancellationToken) is { } round)
        {
            var spins = await store.SpinsAsync(round.RoundId, cancellationToken);
            openFeature = SlotSpins.View(round, spins[^1], game, player.Balance, rate);
        }

        var recent = await store.RecentAsync(player.Ref, game.GameId, 50, cancellationToken);
        return new StateView(
            player.Balance,
            openFeature,
            [.. recent.Select(r => new RoundSummary(r.RoundId, r.Stake, SlotSpins.Money(r.PaidUnits, r.Stake, game.LineCount, rate), r.Status, r.CreatedAt))],
            Info(slot.Game, rate));
    }

    public static GameInfo Info(SlotGame slot, long rateBp = Multipliers.Base)
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
