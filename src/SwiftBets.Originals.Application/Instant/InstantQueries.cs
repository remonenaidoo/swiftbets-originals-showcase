using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

public static class InstantGames
{
    public static readonly string[] Ids = [Mines.GameId, Crash.GameId, Towers.GameId, HigherLower.GameId, .. OneShotGames.All.Keys];
}

/// <summary>The fairness panel: the active seed's hash, the client seed and next nonce, and the last revealed pair.</summary>
public sealed record SeedsView(string ServerSeedHash, string ClientSeed, long Nonce, string? PreviousServerSeed, string? PreviousClientSeed, long? PreviousNonce, string NextServerSeedHash)
{
    public static SeedsView Of(PlayerSeeds s) => new(s.ServerSeedHash, s.ClientSeed, s.Nonce, s.PreviousServerSeed, s.PreviousClientSeed, s.PreviousNonce, s.NextServerSeedHash);
}

public sealed record MinesInfo(string GameId, string Name, long RateBp, int MaxWinStakes, long[] Stakes, int Tiles, IReadOnlyList<long[]> Table);

public sealed record CrashInfo(string GameId, string Name, long RateBp, int MaxWinStakes, long[] Stakes, long MinAutoBp, double Rate, int BettingSeconds);

public sealed record CrashState(long? Balance, CrashPublic? Round, IReadOnlyList<CrashBetView> Bets, IReadOnlyList<BetPublic> Board, IReadOnlyList<long> History, IReadOnlyList<RoundSummary> Recent, CrashInfo Info);

/// <summary>What a one-shot game, Towers or Higher or Lower loads with; <paramref name="Open"/> is the unfinished round, if the game has one.</summary>
public sealed record InstantState(long? Balance, object? Open, IReadOnlyList<RoundSummary> Recent, SeedsView Seeds, object Info);

public sealed record MinesState(long? Balance, MinesView? Open, IReadOnlyList<RoundSummary> Recent, SeedsView Seeds, MinesInfo Info);

public sealed class InstantQueries(IRoundStore store, IWallet wallet, ISeedStore seeds, MinesHandler mines, CrashRoom crash, TowersHandler towers, HigherLowerHandler higherLower, IGameRates rates)
{
    public static object TowersInfo(long rateBp) => new
    {
        gameId = Towers.GameId, name = "Towers", rateBp, maxWinStakes = Multipliers.MaxWinStakes, stakes = InstantStakes.Offered, rows = Towers.Rows,
        levels = Towers.Levels.ToDictionary(l => l.Key, l => new
        {
            tiles = l.Value.Tiles, traps = l.Value.Traps,
            table = Enumerable.Range(1, Towers.Rows).TakeWhile(k => k == 1 || !Towers.Finished(l.Key, k - 1, rateBp)).Select(k => Towers.MultiplierBp(l.Key, k, rateBp)).ToArray(),
        }),
    };

    public static object HigherLowerInfo(long rateBp) => new
    {
        gameId = HigherLower.GameId, name = "Higher or Lower", rateBp, maxWinStakes = Multipliers.MaxWinStakes, stakes = InstantStakes.Offered, cards = HigherLower.Cards,
    };

    public static CrashInfo CrashInfo(long rateBp) => new(Crash.GameId, "Crash", rateBp, Multipliers.MaxWinStakes, InstantStakes.Offered, Crash.MinAutoBp, Crash.Rate, (int)Crash.Betting.TotalSeconds);

    public static MinesInfo MinesInfo(long rateBp) => new(Mines.GameId, "Mines", rateBp, Multipliers.MaxWinStakes, InstantStakes.Offered, Mines.Tiles,
        [.. Enumerable.Range(1, Mines.Tiles - 1).Select(m => Enumerable.Range(1, Mines.Tiles - m).TakeWhile(k => k == 1 || !Mines.Finished(m, k - 1, rateBp)).Select(k => Mines.MultiplierBp(m, k, rateBp)).ToArray())]);

    public async Task<Result<InstantState>> StateAsync(string gameId, string sessionToken, CancellationToken cancellationToken)
    {
        var rate = rates.For(gameId);
        object? info = gameId == Towers.GameId ? TowersInfo(rate)
            : gameId == HigherLower.GameId ? HigherLowerInfo(rate)
            : OneShotGames.All.TryGetValue(gameId, out var game) ? new { gameId = game.Id, name = game.Name, rateBp = rate, maxWinStakes = Multipliers.MaxWinStakes, stakes = game.Stakes, rules = game.InfoAt(rate) }
            : null;
        if (info is null)
        {
            return GameError.GameNotFound;
        }

        var identity = await wallet.IdentifyAsync(sessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        var player = identity.Value;
        object? open = gameId == Towers.GameId ? await towers.OpenAsync(player.Ref, player.Balance, cancellationToken)
            : gameId == HigherLower.GameId ? await higherLower.OpenAsync(player.Ref, player.Balance, cancellationToken)
            : null;
        return new InstantState(
            player.Balance,
            open,
            await FinishedRoundsAsync(player.Ref, gameId, cancellationToken),
            SeedsView.Of(await seeds.GetAsync(player.Ref, cancellationToken)),
            info);
    }

    public async Task<Result<CrashState>> CrashAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var identity = await wallet.IdentifyAsync(sessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        var player = identity.Value;
        var recent = await FinishedRoundsAsync(player.Ref, Crash.GameId, cancellationToken);
        return new CrashState(
            player.Balance,
            crash.Public(),
            await crash.PlayerBetsAsync(player.Ref, player.Balance, cancellationToken),
            crash.Board(),
            await crash.HistoryAsync(cancellationToken),
            recent,
            CrashInfo(rates.For(Crash.GameId)));
    }

    public async Task<Result<MinesState>> MinesAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var identity = await wallet.IdentifyAsync(sessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        var player = identity.Value;
        var recent = await FinishedRoundsAsync(player.Ref, Mines.GameId, cancellationToken);
        return new MinesState(
            player.Balance,
            await mines.OpenAsync(player.Ref, player.Balance, cancellationToken),
            recent,
            SeedsView.Of(await seeds.GetAsync(player.Ref, cancellationToken)),
            MinesInfo(rates.For(Mines.GameId)));
    }

    /// <summary>Refused while an instant round is unfinished: revealing the old seed would reveal that round's outcome.</summary>
    public async Task<Result<SeedsView>> RotateAsync(string sessionToken, string clientSeed, CancellationToken cancellationToken)
    {
        if (clientSeed is not { Length: > 0 and <= 64 } || !clientSeed.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return GameError.InvalidSeed;
        }

        var identity = await wallet.IdentifyAsync(sessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        foreach (var id in InstantGames.Ids)
        {
            if (await store.FindOpenAsync(identity.Value.Ref, id, cancellationToken) is not null)
            {
                return GameError.RoundInProgress;
            }
        }

        return await seeds.RotateAsync(identity.Value.Ref, clientSeed, cancellationToken) is { } rotated ? SeedsView.Of(rotated) : GameError.RoundInProgress;
    }

    private async Task<IReadOnlyList<RoundSummary>> FinishedRoundsAsync(string playerRef, string gameId, CancellationToken cancellationToken) =>
        [.. (await store.RecentAsync(playerRef, gameId, 50, cancellationToken)).Where(r => r.Status != RoundStatus.Open).Select(r => new RoundSummary(r.RoundId, r.Stake, r.PaidUnits, r.Status, r.CreatedAt))];
}
