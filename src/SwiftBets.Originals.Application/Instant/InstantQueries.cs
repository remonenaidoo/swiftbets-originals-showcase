using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

/// <summary>The instant games this studio offers, by id (each also has a page at /play?game=).</summary>
public static class InstantGames
{
    public static readonly string[] Ids = [Mines.GameId, Crash.GameId, Towers.GameId, HigherLower.GameId, .. OneShotGames.All.Keys];
}

/// <summary>The fairness panel: the active seed's hash, the client seed and the next nonce, and the last revealed pair.</summary>
public sealed record SeedsView(string ServerSeedHash, string ClientSeed, long Nonce, string? PreviousServerSeed, string? PreviousClientSeed, long? PreviousNonce, string NextServerSeedHash)
{
    public static SeedsView Of(PlayerSeeds s) => new(s.ServerSeedHash, s.ClientSeed, s.Nonce, s.PreviousServerSeed, s.PreviousClientSeed, s.PreviousNonce, s.NextServerSeedHash);
}

/// <remarks><paramref name="RateBp"/> is the rate the game plays at (its multipliers already include it); the pages never show it.</remarks>
public sealed record MinesInfo(string GameId, string Name, long RateBp, int MaxWinStakes, long[] Stakes, int Tiles, IReadOnlyList<long[]> Table);

public sealed record CrashInfo(string GameId, string Name, long RateBp, int MaxWinStakes, long[] Stakes, long MinAutoBp, double Rate, int BettingSeconds);

public sealed record CrashState(long? Balance, CrashPublic? Round, IReadOnlyList<CrashBetView> Bets, IReadOnlyList<BetPublic> Board, IReadOnlyList<long> History, IReadOnlyList<RoundSummary> Recent, CrashInfo Info);

/// <summary>What a one-shot game or Towers loads with: balance, the open Towers round if any, history, seeds and the game's rules data.</summary>
public sealed record InstantState(long? Balance, object? Open, IReadOnlyList<RoundSummary> Recent, SeedsView Seeds, object Info);

public sealed record MinesState(long? Balance, MinesView? Open, IReadOnlyList<RoundSummary> Recent, SeedsView Seeds, MinesInfo Info);

/// <summary>What the Mines page loads with, and the player's seeds: shown, and rotated (which reveals the old server seed).</summary>
public sealed class InstantQueries(IRoundStore store, IWallet wallet, ISeedStore seeds, MinesHandler mines, CrashRoom crash, TowersHandler towers, HigherLowerHandler higherLower, IGameRates? rates = null)
{
    private long Rate(string gameId) => (rates ?? FixedRates.Design).For(gameId);

    public static object TowersInfo(long rateBp) => new
    {
        gameId = Towers.GameId, name = "Towers", rateBp, maxWinStakes = (int)(Mult.CapBp / Mult.One), stakes = Mines.Stakes, rows = Towers.Rows,
        levels = Towers.Levels.ToDictionary(l => l.Key, l => new
        {
            tiles = l.Value.Tiles, traps = l.Value.Traps,
            table = Enumerable.Range(1, Towers.Rows).TakeWhile(k => k == 1 || !Towers.Finished(l.Key, k - 1, rateBp)).Select(k => Towers.MultiplierBp(l.Key, k, rateBp)).ToArray(),
        }),
    };

    public static object HigherLowerInfo(long rateBp) => new
    {
        gameId = HigherLower.GameId, name = "Higher or Lower", rateBp, maxWinStakes = (int)(Mult.CapBp / Mult.One), stakes = Mines.Stakes, cards = HigherLower.Cards,
    };

    /// <summary>The page data of a one-shot game, Towers or Higher or Lower.</summary>
    public async Task<Result<InstantState>> StateAsync(string gameId, string sessionToken, CancellationToken cancellationToken)
    {
        object info;
        if (gameId == Towers.GameId)
        {
            info = TowersInfo(Rate(gameId));
        }
        else if (gameId == HigherLower.GameId)
        {
            info = HigherLowerInfo(Rate(gameId));
        }
        else if (OneShotGames.All.TryGetValue(gameId, out var game))
        {
            info = new { gameId = game.Id, name = game.Name, rateBp = Rate(gameId), maxWinStakes = (int)(Mult.CapBp / Mult.One), stakes = game.Stakes, rules = game.InfoAt(Rate(gameId)) };
        }
        else
        {
            return SpinError.GameNotFound;
        }

        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var recent = await store.RecentAsync(playerRef!, gameId, 50, cancellationToken);
        return new InstantState(
            balance,
            gameId == Towers.GameId ? await towers.OpenAsync(playerRef!, balance, cancellationToken)
                : gameId == HigherLower.GameId ? await higherLower.OpenAsync(playerRef!, balance, cancellationToken) : null,
            [.. recent.Where(r => r.Status != RoundStatus.Open).Select(r => new RoundSummary(r.RoundId, r.Stake, r.PaidUnits, r.Status, r.CreatedAt))],
            SeedsView.Of(await seeds.GetAsync(playerRef!, cancellationToken)),
            info);
    }

    public static CrashInfo CrashInfo(long rateBp) => new(Crash.GameId, "Crash", rateBp, (int)(Mult.CapBp / Mult.One), Crash.Stakes, Crash.MinAutoBp, Crash.Rate, (int)Crash.Betting.TotalSeconds);

    public async Task<Result<CrashState>> CrashAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var recent = await store.RecentAsync(playerRef!, Crash.GameId, 50, cancellationToken);
        return new CrashState(
            balance,
            crash.Public(),
            await crash.PlayerBetsAsync(playerRef!, balance, cancellationToken),
            crash.Board(),
            await crash.HistoryAsync(cancellationToken),
            [.. recent.Where(r => r.Status != RoundStatus.Open).Select(r => new RoundSummary(r.RoundId, r.Stake, r.PaidUnits, r.Status, r.CreatedAt))],
            CrashInfo(Rate(Crash.GameId)));
    }

    public static MinesInfo MinesInfo(long rateBp) => new(Mines.GameId, "Mines", rateBp, (int)(Mult.CapBp / Mult.One), Mines.Stakes, Mines.Tiles,
        [.. Enumerable.Range(1, Mines.Tiles - 1).Select(m => Enumerable.Range(1, Mines.Tiles - m).TakeWhile(k => k == 1 || !Mines.Finished(m, k - 1, rateBp)).Select(k => Mines.MultiplierBp(m, k, rateBp)).ToArray())]);

    public async Task<Result<MinesState>> MinesAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var recent = await store.RecentAsync(playerRef!, Mines.GameId, 50, cancellationToken);
        return new MinesState(
            balance,
            await mines.OpenAsync(playerRef!, balance, cancellationToken),
            [.. recent.Where(r => r.Status != RoundStatus.Open).Select(r => new RoundSummary(r.RoundId, r.Stake, r.PaidUnits, r.Status, r.CreatedAt))],
            SeedsView.Of(await seeds.GetAsync(playerRef!, cancellationToken)),
            MinesInfo(Rate(Mines.GameId)));
    }

    /// <summary>
    /// Starts a new server seed. Refused while an instant round is open: revealing the old seed would reveal that
    /// round's outcome (where its mines are).
    /// </summary>
    public async Task<Result<SeedsView>> RotateAsync(string sessionToken, string clientSeed, CancellationToken cancellationToken)
    {
        if (clientSeed is not { Length: > 0 and <= 64 } || !clientSeed.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return SpinError.InvalidSeed;
        }

        var (playerRef, _, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        foreach (var id in InstantGames.Ids)
        {
            if (await store.FindOpenAsync(playerRef!, id, cancellationToken) is not null)
            {
                return SpinError.RoundInProgress;
            }
        }

        return await seeds.RotateAsync(playerRef!, clientSeed, cancellationToken) is { } rotated ? SeedsView.Of(rotated) : SpinError.RoundInProgress;
    }
}
