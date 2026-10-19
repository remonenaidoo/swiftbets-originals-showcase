using System.Text.Json;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

public sealed record TowersStart(string SessionToken, long Stake, string Level, string IdempotencyKey);

/// <summary>A Towers round as the client sees it: the tile picked on each row so far; the traps only once it is over.</summary>
public sealed record TowersView(
    Guid RoundId,
    long Stake,
    string Level,
    IReadOnlyList<int> Picked,
    long MultiplierBp,
    long NextBp,
    long Win,
    string Status,
    IReadOnlyList<int[]>? Traps,
    long Nonce,
    string ClientSeed,
    string ServerSeedHash,
    long? Balance);

/// <summary>
/// Towers rounds, built like Mines: start takes the stake (the traps are drawn and stored first), each pick climbs one
/// row in a locked step, cash-out pays the row reached; a trap ends the round with nothing to pay.
/// </summary>
public sealed class TowersHandler(IRoundStore store, IWallet wallet, ISeedStore seeds, TimeProvider time, IGameRates? rates = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<TowersView>> StartAsync(TowersStart request, CancellationToken cancellationToken)
    {
        if (Array.IndexOf(Mines.Stakes, request.Stake) < 0 || request.Level is null || !Towers.Levels.ContainsKey(request.Level) || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100)
        {
            return SpinError.InvalidStake;
        }

        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, request.SessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await store.FindByKeyAsync(playerRef!, request.IdempotencyKey, cancellationToken) is { } repeat)
        {
            if (repeat.GameId != Towers.GameId)
            {
                return SpinError.KeyInUse;
            }

            // The first request may still be taking the stake: answer only once it has, never with a half-made round.
            return repeat.Status == RoundStatus.Pending ? SpinError.RoundInProgress : View(repeat, await store.SpinsAsync(repeat.RoundId, cancellationToken), balance);
        }

        if (await store.FindOpenAsync(playerRef!, Towers.GameId, cancellationToken) is not null)
        {
            return SpinError.RoundInProgress;
        }

        var draw = await seeds.TakeNonceAsync(playerRef!, cancellationToken);
        var layout = Towers.Layout(Fair.Floats(draw.ServerSeed, draw.ClientSeed, draw.Nonce, Towers.FloatsNeeded), request.Level);
        var start = new StartOutcome(request.Level, layout, draw.Nonce, draw.ClientSeed, draw.ServerSeedHash, (rates ?? FixedRates.Design).For(Towers.GameId));
        var roundId = Guid.NewGuid();
        var round = new RoundRecord(roundId, playerRef!, request.SessionToken, Towers.GameId, request.Stake, request.IdempotencyKey, RoundStatus.Pending, 0, 0, time.GetUtcNow());
        var first = new SpinRecord(roundId, 0, [], JsonSerializer.Serialize(start, Json), 0, Play.BetTx(roundId), null, false);
        await store.InsertAsync(round, first, cancellationToken);

        (balance, error) = await Stake.TakeAsync(store, wallet, round, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await store.UpdateAsync(roundId, RoundStatus.Open, 0, 0, cancellationToken);
        return View(round with { Status = RoundStatus.Open }, [first], balance);
    }

    public async Task<Result<TowersView>> PickAsync(string sessionToken, Guid roundId, int tile, CancellationToken cancellationToken)
    {
        if (tile is < 0 or > 3)
        {
            return SpinError.TileTaken;
        }

        return await StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var start = Start(spins);
            var row = Picks(spins).Count;
            if (tile >= Towers.Levels[start.Level].Tiles || row >= Towers.Rows)
            {
                return null;
            }

            var seq = spins.Count;
            if (start.Layout[row].Contains(tile))
            {
                return (new SpinRecord(roundId, seq, [tile], "{}", 0, null, null, false), RoundStatus.Settled, 0L);
            }

            if (!Towers.Finished(start.Level, row + 1, start.RateBp))
            {
                return (new SpinRecord(roundId, seq, [tile], "{}", 0, null, null, false), RoundStatus.Open, 0L);
            }

            // The top, or the cap: the round cashes out on this pick.
            var win = Mult.Pay(round.Stake, Towers.MultiplierBp(start.Level, row + 1, start.RateBp));
            return (new SpinRecord(roundId, seq, [tile], "{}", win, null, Play.WinTx(roundId, seq), false), RoundStatus.WinPending, win);
        }, tileTaken: true, cancellationToken);
    }

    public Task<Result<TowersView>> CashoutAsync(string sessionToken, Guid roundId, CancellationToken cancellationToken) =>
        StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var safe = Picks(spins).Count;
            if (safe == 0)
            {
                return null;
            }

            var seq = spins.Count;
            var win = Mult.Pay(round.Stake, Towers.MultiplierBp(Start(spins).Level, safe, Start(spins).RateBp));
            return (new SpinRecord(roundId, seq, [], "{}", win, null, Play.WinTx(roundId, seq), false), RoundStatus.WinPending, win);
        }, tileTaken: false, cancellationToken);

    /// <summary>The player's open round, to resume after a reload.</summary>
    public async Task<TowersView?> OpenAsync(string playerRef, long? balance, CancellationToken cancellationToken) =>
        await store.FindOpenAsync(playerRef, Towers.GameId, cancellationToken) is { } round ? View(round, await store.SpinsAsync(round.RoundId, cancellationToken), balance) : null;

    private async Task<Result<TowersView>> StepAsync(string sessionToken, Guid roundId, Func<RoundRecord, IReadOnlyList<SpinRecord>, (SpinRecord Spin, string Status, long Paid)?> step, bool tileTaken, CancellationToken cancellationToken)
    {
        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await store.StepAsync(roundId, playerRef!, Towers.GameId, step, cancellationToken) is not var (round, spin))
        {
            // Refused: either the round is not open (over, or not this player's), or the step itself declined.
            var current = await store.GetAsync(roundId, cancellationToken);
            return current is null || current.PlayerRef != playerRef || current.GameId != Towers.GameId ? SpinError.RoundNotFound
                : current.Status != RoundStatus.Open ? SpinError.RoundOver
                : tileTaken ? SpinError.TileTaken : SpinError.NothingToCashOut;
        }

        balance = await Stake.PayAsync(store, wallet, round, spin, balance, cancellationToken);
        return View(round, await store.SpinsAsync(roundId, cancellationToken), balance);
    }

    private static StartOutcome Start(IReadOnlyList<SpinRecord> spins) => JsonSerializer.Deserialize<StartOutcome>(spins[0].OutcomeJson, Json)!;

    private static List<int> Picks(IReadOnlyList<SpinRecord> spins) => [.. spins.Skip(1).Where(s => s.Stops.Length == 1).Select(s => s.Stops[0])];

    private static TowersView View(RoundRecord round, IReadOnlyList<SpinRecord> spins, long? balance)
    {
        var start = Start(spins);
        var picks = Picks(spins);
        var fell = picks.Select((p, row) => start.Layout[row].Contains(p)).Any(x => x);
        var safe = picks.Count - (fell ? 1 : 0);
        var win = spins.Sum(s => s.Win);
        var status = round.Status == RoundStatus.Open ? "open" : fell ? "busted" : round.Status is RoundStatus.Settled or RoundStatus.WinPending ? "cashed" : round.Status;
        return new TowersView(round.RoundId, round.Stake, start.Level, picks, Towers.MultiplierBp(start.Level, safe, start.RateBp), Towers.MultiplierBp(start.Level, safe + 1, start.RateBp), win, status,
            // The traps are shown only for a round played out; a refused or voided round's draw is never shown (or used).
            round.Status is RoundStatus.Settled or RoundStatus.WinPending ? start.Layout : null, start.Nonce, start.ClientSeed, start.ServerSeedHash, balance);
    }

    /// <summary>The first spin's stored outcome: the round's traps. Never sent while the round is open.</summary>
    /// <remarks><paramref name="RateBp"/> is the rate the round started at (rounds stored before rates had it are at the design rate).</remarks>
    private sealed record StartOutcome(string Level, int[][] Layout, long Nonce, string ClientSeed, string ServerSeedHash, long RateBp = Mult.Base);
}
