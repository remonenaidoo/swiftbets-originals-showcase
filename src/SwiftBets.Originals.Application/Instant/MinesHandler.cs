using System.Text.Json;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

public sealed record MinesStart(string SessionToken, long Stake, int Mines, string IdempotencyKey);

/// <summary>
/// A Mines round as the client sees it. The mine tiles are sent only once the round is over; until then the client
/// knows only the tiles it revealed.
/// </summary>
public sealed record MinesView(
    Guid RoundId,
    long Stake,
    int Mines,
    IReadOnlyList<int> Revealed,
    long MultiplierBp,
    long NextBp,
    long Win,
    string Status,
    IReadOnlyList<int>? MineTiles,
    long Nonce,
    string ClientSeed,
    string ServerSeedHash,
    long? Balance);

/// <summary>
/// Mines rounds: start takes the stake (the layout is drawn from the player's seeds and stored first), each pick is
/// one locked step, cash-out pays the multiplier reached. A mine ends the round with nothing to pay.
/// </summary>
public sealed class MinesHandler(IRoundStore store, IWallet wallet, ISeedStore seeds, TimeProvider time, IGameRates? rates = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<MinesView>> StartAsync(MinesStart request, CancellationToken cancellationToken)
    {
        if (Array.IndexOf(Mines.Stakes, request.Stake) < 0 || request.Mines is < 1 or > Mines.Tiles - 1 || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100)
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
            if (repeat.GameId != Mines.GameId)
            {
                return SpinError.KeyInUse;
            }

            // The first request may still be taking the stake: answer only once it has, never with a half-made round.
            return repeat.Status == RoundStatus.Pending ? SpinError.RoundInProgress : View(repeat, await store.SpinsAsync(repeat.RoundId, cancellationToken), balance);
        }

        if (await store.FindOpenAsync(playerRef!, Mines.GameId, cancellationToken) is not null)
        {
            return SpinError.RoundInProgress;
        }

        var draw = await seeds.TakeNonceAsync(playerRef!, cancellationToken);
        var layout = Mines.Layout(Fair.Floats(draw.ServerSeed, draw.ClientSeed, draw.Nonce, Mines.Tiles), request.Mines);
        var start = new StartOutcome(request.Mines, layout, draw.Nonce, draw.ClientSeed, draw.ServerSeedHash, (rates ?? FixedRates.Design).For(Mines.GameId));
        var roundId = Guid.NewGuid();
        var round = new RoundRecord(roundId, playerRef!, request.SessionToken, Mines.GameId, request.Stake, request.IdempotencyKey, RoundStatus.Pending, 0, 0, time.GetUtcNow());
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

    public async Task<Result<MinesView>> PickAsync(string sessionToken, Guid roundId, int tile, CancellationToken cancellationToken)
    {
        if (tile is < 0 or >= Mines.Tiles)
        {
            return SpinError.TileTaken;
        }

        return await StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var start = Start(spins);
            var picks = Picks(spins);
            if (picks.Contains(tile))
            {
                return null;
            }

            var seq = spins.Count;
            if (start.Layout.Contains(tile))
            {
                return (new SpinRecord(roundId, seq, [tile], "{}", 0, null, null, false), RoundStatus.Settled, 0L);
            }

            var safe = picks.Count + 1;
            if (!Mines.Finished(start.Mines, safe, start.RateBp))
            {
                return (new SpinRecord(roundId, seq, [tile], "{}", 0, null, null, false), RoundStatus.Open, 0L);
            }

            // Nothing more to win: the last safe tile, or the cap. The round cashes out on this pick.
            var win = Mult.Pay(round.Stake, Mines.MultiplierBp(start.Mines, safe, start.RateBp));
            return (new SpinRecord(roundId, seq, [tile], "{}", win, null, Play.WinTx(roundId, seq), false), RoundStatus.WinPending, win);
        }, tileTaken: true, cancellationToken);
    }

    public Task<Result<MinesView>> CashoutAsync(string sessionToken, Guid roundId, CancellationToken cancellationToken) =>
        StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var safe = Picks(spins).Count;
            if (safe == 0)
            {
                return null;
            }

            var seq = spins.Count;
            var win = Mult.Pay(round.Stake, Mines.MultiplierBp(Start(spins).Mines, safe, Start(spins).RateBp));
            return (new SpinRecord(roundId, seq, [], "{}", win, null, Play.WinTx(roundId, seq), false), RoundStatus.WinPending, win);
        }, tileTaken: false, cancellationToken);

    /// <summary>The player's open round, to resume after a reload.</summary>
    public async Task<MinesView?> OpenAsync(string playerRef, long? balance, CancellationToken cancellationToken) =>
        await store.FindOpenAsync(playerRef, Mines.GameId, cancellationToken) is { } round ? View(round, await store.SpinsAsync(round.RoundId, cancellationToken), balance) : null;

    private async Task<Result<MinesView>> StepAsync(string sessionToken, Guid roundId, Func<RoundRecord, IReadOnlyList<SpinRecord>, (SpinRecord Spin, string Status, long Paid)?> step, bool tileTaken, CancellationToken cancellationToken)
    {
        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await store.StepAsync(roundId, playerRef!, Mines.GameId, step, cancellationToken) is not var (round, spin))
        {
            // Refused: either the round is not open (over, or not this player's), or the step itself declined.
            var current = await store.GetAsync(roundId, cancellationToken);
            return current is null || current.PlayerRef != playerRef || current.GameId != Mines.GameId ? SpinError.RoundNotFound
                : current.Status != RoundStatus.Open ? SpinError.RoundOver
                : tileTaken ? SpinError.TileTaken : SpinError.NothingToCashOut;
        }

        balance = await Stake.PayAsync(store, wallet, round, spin, balance, cancellationToken);
        return View(round, await store.SpinsAsync(roundId, cancellationToken), balance);
    }

    private static StartOutcome Start(IReadOnlyList<SpinRecord> spins) => JsonSerializer.Deserialize<StartOutcome>(spins[0].OutcomeJson, Json)!;

    private static List<int> Picks(IReadOnlyList<SpinRecord> spins) => [.. spins.Skip(1).Where(s => s.Stops.Length == 1).Select(s => s.Stops[0])];

    private static MinesView View(RoundRecord round, IReadOnlyList<SpinRecord> spins, long? balance)
    {
        var start = Start(spins);
        var picks = Picks(spins);
        var busted = picks.Any(start.Layout.Contains);
        var safe = picks.Count(p => !start.Layout.Contains(p));
        var win = spins.Sum(s => s.Win);
        var status = round.Status == RoundStatus.Open ? "open" : busted ? "busted" : round.Status is RoundStatus.Settled or RoundStatus.WinPending ? "cashed" : round.Status;
        return new MinesView(round.RoundId, round.Stake, start.Mines, picks, Mines.MultiplierBp(start.Mines, safe, start.RateBp), Mines.MultiplierBp(start.Mines, safe + 1, start.RateBp), win, status,
            // The mines are shown only for a round played out; a refused or voided round's draw is never shown (or used).
            round.Status is RoundStatus.Settled or RoundStatus.WinPending ? start.Layout : null, start.Nonce, start.ClientSeed, start.ServerSeedHash, balance);
    }

    /// <summary>The first spin's stored outcome: the round's draw. Never sent while the round is open.</summary>
    /// <remarks><paramref name="RateBp"/> is the rate the round started at (rounds stored before rates had it are at the design rate).</remarks>
    private sealed record StartOutcome(int Mines, int[] Layout, long Nonce, string ClientSeed, string ServerSeedHash, long RateBp = Mult.Base);
}
