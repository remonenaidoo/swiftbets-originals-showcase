using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

/// <summary>The instant games this studio offers, by id (each also has a page at /play?game=).</summary>
public static class InstantGames
{
    public static readonly string[] Ids = [Mines.GameId, Crash.GameId, Towers.GameId, .. OneShotGames.All.Keys];
}

/// <summary>The fairness panel: the active seed's hash, the client seed and the next nonce, and the last revealed pair.</summary>
public sealed record SeedsView(string ServerSeedHash, string ClientSeed, long Nonce, string? PreviousServerSeed, string? PreviousClientSeed, long? PreviousNonce, string NextServerSeedHash)
{
    public static SeedsView Of(PlayerSeeds s) => new(s.ServerSeedHash, s.ClientSeed, s.Nonce, s.PreviousServerSeed, s.PreviousClientSeed, s.PreviousNonce, s.NextServerSeedHash);
}

public sealed record MinesInfo(string GameId, string Name, decimal Rtp, int MaxWinStakes, long[] Stakes, int Tiles, IReadOnlyList<long[]> Table);

public sealed record CrashInfo(string GameId, string Name, decimal Rtp, int MaxWinStakes, long[] Stakes, long MinAutoBp, double Rate, int BettingSeconds);

public sealed record CrashState(long? Balance, CrashPublic? Round, IReadOnlyList<CrashBetView> Bets, IReadOnlyList<BetPublic> Board, IReadOnlyList<long> History, IReadOnlyList<RoundSummary> Recent, CrashInfo Info);

/// <summary>What a one-shot game or Towers loads with: balance, the open Towers round if any, history, seeds and the game's rules data.</summary>
public sealed record InstantState(long? Balance, TowersView? Open, IReadOnlyList<RoundSummary> Recent, SeedsView Seeds, object Info);

public sealed record MinesState(long? Balance, MinesView? Open, IReadOnlyList<RoundSummary> Recent, SeedsView Seeds, MinesInfo Info);

/// <summary>What the Mines page loads with, and the player's seeds: shown, and rotated (which reveals the old server seed).</summary>
public sealed class InstantQueries(IRoundStore store, IWallet wallet, ISeedStore seeds, MinesHandler mines, CrashRoom crash, TowersHandler towers)
{
    public static object TowersInfo { get; } = new
    {
        gameId = Towers.GameId, name = "Towers", rtp = 96.00m, maxWinStakes = (int)(Mult.CapBp / Mult.One), stakes = Mines.Stakes, rows = Towers.Rows,
        levels = Towers.Levels.ToDictionary(l => l.Key, l => new
        {
            tiles = l.Value.Tiles, traps = l.Value.Traps,
            table = Enumerable.Range(1, Towers.Rows).TakeWhile(k => k == 1 || !Towers.Finished(l.Key, k - 1)).Select(k => Towers.MultiplierBp(l.Key, k)).ToArray(),
        }),
    };

    /// <summary>The page data of Dice, Limbo, Peg Drop, Keno or Towers.</summary>
    public async Task<Result<InstantState>> StateAsync(string gameId, string sessionToken, CancellationToken cancellationToken)
    {
        object info;
        if (gameId == Towers.GameId)
        {
            info = TowersInfo;
        }
        else if (OneShotGames.All.TryGetValue(gameId, out var game))
        {
            info = new { gameId = game.Id, name = game.Name, rtp = 96.00m, maxWinStakes = (int)(Mult.CapBp / Mult.One), stakes = game.Stakes, rules = game.Info };
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
            gameId == Towers.GameId ? await towers.OpenAsync(playerRef!, balance, cancellationToken) : null,
            [.. recent.Where(r => r.Status != RoundStatus.Open).Select(r => new RoundSummary(r.RoundId, r.Stake, r.PaidUnits, r.Status, r.CreatedAt))],
            SeedsView.Of(await seeds.GetAsync(playerRef!, cancellationToken)),
            info);
    }

    public static CrashInfo CrashInfo { get; } = new(Crash.GameId, "Crash", 96.00m, (int)(Mult.CapBp / Mult.One), Crash.Stakes, Crash.MinAutoBp, Crash.Rate, (int)Crash.Betting.TotalSeconds);

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
            CrashInfo);
    }

    public static MinesInfo MinesInfo { get; } = new(Mines.GameId, "Mines", 96.00m, (int)(Mult.CapBp / Mult.One), Mines.Stakes, Mines.Tiles,
        [.. Enumerable.Range(1, Mines.Tiles - 1).Select(m => Enumerable.Range(1, Mines.Tiles - m).TakeWhile(k => k == 1 || !Mines.Finished(m, k - 1)).Select(k => Mines.MultiplierBp(m, k)).ToArray())]);

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
            MinesInfo);
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
