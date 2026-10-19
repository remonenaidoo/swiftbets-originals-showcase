using System.Text.Json;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

public sealed record HigherLowerStart(string SessionToken, long Stake, string IdempotencyKey);

/// <summary>
/// A Higher or Lower round as the client sees it: the cards turned so far and each guess on them; the next card is
/// never sent before it is turned. <paramref name="HigherBp"/> and <paramref name="LowerBp"/> are what each guess on the
/// current card would make the multiplier, or 0 where that guess is not allowed.
/// </summary>
public sealed record HigherLowerView(
    Guid RoundId,
    long Stake,
    IReadOnlyList<int> Cards,
    IReadOnlyList<int> Guesses,
    long MultiplierBp,
    long HigherBp,
    long LowerBp,
    long Win,
    string Status,
    long Nonce,
    string ClientSeed,
    string ServerSeedHash,
    long? Balance);

/// <summary>
/// Higher or Lower rounds, built like Towers: start takes the stake (the deck is drawn and stored first), each guess
/// or skip turns one card in a locked step, cash-out pays the multiplier reached; a lost guess ends the round.
/// </summary>
public sealed class HigherLowerHandler(IRoundStore store, IWallet wallet, ISeedStore seeds, TimeProvider time)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<HigherLowerView>> StartAsync(HigherLowerStart request, CancellationToken cancellationToken)
    {
        if (Array.IndexOf(Mines.Stakes, request.Stake) < 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100)
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
            if (repeat.GameId != HigherLower.GameId)
            {
                return SpinError.KeyInUse;
            }

            return repeat.Status == RoundStatus.Pending ? SpinError.RoundInProgress : View(repeat, await store.SpinsAsync(repeat.RoundId, cancellationToken), balance);
        }

        if (await store.FindOpenAsync(playerRef!, HigherLower.GameId, cancellationToken) is not null)
        {
            return SpinError.RoundInProgress;
        }

        var draw = await seeds.TakeNonceAsync(playerRef!, cancellationToken);
        var deck = HigherLower.Deal(Fair.Floats(draw.ServerSeed, draw.ClientSeed, draw.Nonce, HigherLower.Cards));
        var start = new StartOutcome(deck, draw.Nonce, draw.ClientSeed, draw.ServerSeedHash);
        var roundId = Guid.NewGuid();
        var round = new RoundRecord(roundId, playerRef!, request.SessionToken, HigherLower.GameId, request.Stake, request.IdempotencyKey, RoundStatus.Pending, 0, 0, time.GetUtcNow());
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

    public async Task<Result<HigherLowerView>> GuessAsync(string sessionToken, Guid roundId, int guess, CancellationToken cancellationToken)
    {
        if (guess is not (HigherLower.Skip or HigherLower.Higher or HigherLower.Lower))
        {
            return SpinError.GuessNotAllowed;
        }

        return await StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var deck = Start(spins).Deck;
            var guesses = Guesses(spins);
            var at = guesses.Count;
            if (at >= HigherLower.Cards - 1)
            {
                return null;
            }

            var (rank, next) = (HigherLower.Rank(deck[at]), HigherLower.Rank(deck[at + 1]));
            var counts = Counts(deck, guesses);
            var seq = spins.Count;
            if (guess == HigherLower.Skip)
            {
                return (new SpinRecord(roundId, seq, [guess], "{}", 0, null, null, false), RoundStatus.Open, 0L);
            }

            if (!HigherLower.Allowed(counts, rank, guess))
            {
                return null;
            }

            if (!HigherLower.Wins(rank, next, guess))
            {
                return (new SpinRecord(roundId, seq, [guess], "{}", 0, null, null, false), RoundStatus.Settled, 0L);
            }

            counts.Add(HigherLower.Count(rank, guess));
            // The last card, or no guess left under the cap: the round cashes out on this guess.
            var stuck = at + 1 >= HigherLower.Cards - 1 || !(HigherLower.Allowed(counts, next, HigherLower.Higher) || HigherLower.Allowed(counts, next, HigherLower.Lower));
            if (!stuck)
            {
                return (new SpinRecord(roundId, seq, [guess], "{}", 0, null, null, false), RoundStatus.Open, 0L);
            }

            var win = Mult.Pay(round.Stake, HigherLower.MultiplierBp(counts));
            return (new SpinRecord(roundId, seq, [guess], "{}", win, null, Play.WinTx(roundId, seq), false), RoundStatus.WinPending, win);
        }, refused: SpinError.GuessNotAllowed, cancellationToken);
    }

    public Task<Result<HigherLowerView>> CashoutAsync(string sessionToken, Guid roundId, CancellationToken cancellationToken) =>
        StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var counts = Counts(Start(spins).Deck, Guesses(spins));
            if (counts.Count == 0)
            {
                return null;
            }

            var seq = spins.Count;
            var win = Mult.Pay(round.Stake, HigherLower.MultiplierBp(counts));
            return (new SpinRecord(roundId, seq, [], "{}", win, null, Play.WinTx(roundId, seq), false), RoundStatus.WinPending, win);
        }, refused: SpinError.NothingToCashOut, cancellationToken);

    /// <summary>The player's open round, to resume after a reload.</summary>
    public async Task<HigherLowerView?> OpenAsync(string playerRef, long? balance, CancellationToken cancellationToken) =>
        await store.FindOpenAsync(playerRef, HigherLower.GameId, cancellationToken) is { } round ? View(round, await store.SpinsAsync(round.RoundId, cancellationToken), balance) : null;

    private async Task<Result<HigherLowerView>> StepAsync(string sessionToken, Guid roundId, Func<RoundRecord, IReadOnlyList<SpinRecord>, (SpinRecord Spin, string Status, long Paid)?> step, SpinError refused, CancellationToken cancellationToken)
    {
        var (playerRef, balance, error) = await Play.IdentifyAsync(wallet, sessionToken, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await store.StepAsync(roundId, playerRef!, HigherLower.GameId, step, cancellationToken) is not var (round, spin))
        {
            var current = await store.GetAsync(roundId, cancellationToken);
            return current is null || current.PlayerRef != playerRef || current.GameId != HigherLower.GameId ? SpinError.RoundNotFound
                : current.Status != RoundStatus.Open ? SpinError.RoundOver
                : refused;
        }

        balance = await Stake.PayAsync(store, wallet, round, spin, balance, cancellationToken);
        return View(round, await store.SpinsAsync(roundId, cancellationToken), balance);
    }

    private static StartOutcome Start(IReadOnlyList<SpinRecord> spins) => JsonSerializer.Deserialize<StartOutcome>(spins[0].OutcomeJson, Json)!;

    private static List<int> Guesses(IReadOnlyList<SpinRecord> spins) => [.. spins.Skip(1).Where(s => s.Stops.Length == 1).Select(s => s.Stops[0])];

    /// <summary>The winning counts of the guesses won so far (skips and a lost last guess add none).</summary>
    private static List<int> Counts(int[] deck, List<int> guesses)
    {
        List<int> counts = [];
        for (var i = 0; i < guesses.Count; i++)
        {
            var rank = HigherLower.Rank(deck[i]);
            if (guesses[i] != HigherLower.Skip && HigherLower.Wins(rank, HigherLower.Rank(deck[i + 1]), guesses[i]))
            {
                counts.Add(HigherLower.Count(rank, guesses[i]));
            }
        }

        return counts;
    }

    private static HigherLowerView View(RoundRecord round, IReadOnlyList<SpinRecord> spins, long? balance)
    {
        var start = Start(spins);
        var guesses = Guesses(spins);
        var lost = guesses.Select((g, i) => g != HigherLower.Skip && !HigherLower.Wins(HigherLower.Rank(start.Deck[i]), HigherLower.Rank(start.Deck[i + 1]), g)).Any(x => x);
        var counts = Counts(start.Deck, guesses);
        var turned = start.Deck.Take(guesses.Count + 1).ToArray();
        var open = round.Status == RoundStatus.Open;
        var rank = HigherLower.Rank(turned[^1]);
        long Next(int g) => open && HigherLower.Allowed(counts, rank, g) ? HigherLower.MultiplierBp([.. counts, HigherLower.Count(rank, g)]) : 0;
        var status = open ? "open" : lost ? "busted" : round.Status is RoundStatus.Settled or RoundStatus.WinPending ? "cashed" : round.Status;
        return new HigherLowerView(round.RoundId, round.Stake, turned, guesses, HigherLower.MultiplierBp(counts), Next(HigherLower.Higher), Next(HigherLower.Lower),
            spins.Sum(s => s.Win), status, start.Nonce, start.ClientSeed, start.ServerSeedHash, balance);
    }

    /// <summary>The first spin's stored outcome: the round's deck. Only cards already turned are ever sent.</summary>
    private sealed record StartOutcome(int[] Deck, long Nonce, string ClientSeed, string ServerSeedHash);
}
