using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

public sealed record HigherLowerStart(string SessionToken, long Stake, string IdempotencyKey);

/// <summary>A Higher or Lower round; <paramref name="HigherBp"/> and <paramref name="LowerBp"/> are what each guess would make the multiplier, 0 where not allowed.</summary>
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

public sealed class HigherLowerHandler
{
    private readonly SteppedRounds<HigherLowerView> rounds;
    private readonly IGameRates rates;

    public HigherLowerHandler(IRoundStore store, IWallet wallet, ISeedStore seeds, TimeProvider time, IGameRates rates)
    {
        rounds = new(HigherLower.GameId, store, wallet, seeds, time, View);
        this.rates = rates;
    }

    public Task<Result<HigherLowerView>> StartAsync(HigherLowerStart request, CancellationToken cancellationToken)
    {
        if (Array.IndexOf(InstantStakes.Offered, request.Stake) < 0 || !IdempotencyKey.IsValid(request.IdempotencyKey))
        {
            return Task.FromResult<Result<HigherLowerView>>(GameError.InvalidStake);
        }

        var rate = rates.For(HigherLower.GameId);
        return rounds.StartAsync(request.SessionToken, request.Stake, request.IdempotencyKey, seeds =>
            new Draw(HigherLower.Deal(Fair.Floats(seeds.ServerSeed, seeds.ClientSeed, seeds.Nonce, HigherLower.Cards)), seeds.Nonce, seeds.ClientSeed, seeds.ServerSeedHash, rate),
            cancellationToken);
    }

    public Task<Result<HigherLowerView>> GuessAsync(string sessionToken, Guid roundId, int guess, CancellationToken cancellationToken)
    {
        if (guess is not (HigherLower.Skip or HigherLower.Higher or HigherLower.Lower))
        {
            return Task.FromResult<Result<HigherLowerView>>(GameError.GuessNotAllowed);
        }

        return rounds.StepAsync(sessionToken, roundId, (round, spins) => Guess(round, spins, guess), GameError.GuessNotAllowed, cancellationToken);
    }

    public Task<Result<HigherLowerView>> CashoutAsync(string sessionToken, Guid roundId, CancellationToken cancellationToken) =>
        rounds.StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var draw = SteppedRounds<HigherLowerView>.Drawn<Draw>(spins);
            var counts = WinningCounts(draw.Deck, SteppedRounds<HigherLowerView>.Moves(spins));
            return counts.Count == 0 ? null : RoundSteps.CashOut(roundId, spins.Count, [], Multipliers.Pay(round.Stake, HigherLower.MultiplierBp(counts, draw.RateBp)));
        }, GameError.NothingToCashOut, cancellationToken);

    public Task<HigherLowerView?> OpenAsync(string playerRef, long? balance, CancellationToken cancellationToken) => rounds.OpenAsync(playerRef, balance, cancellationToken);

    private static RoundStep? Guess(RoundRecord round, IReadOnlyList<SpinRecord> spins, int guess)
    {
        var draw = SteppedRounds<HigherLowerView>.Drawn<Draw>(spins);
        var guesses = SteppedRounds<HigherLowerView>.Moves(spins);
        var at = guesses.Count;
        if (at >= HigherLower.Cards - 1)
        {
            return null;
        }

        var roundId = round.RoundId;
        var seq = spins.Count;
        var (rank, next) = (HigherLower.Rank(draw.Deck[at]), HigherLower.Rank(draw.Deck[at + 1]));
        var counts = WinningCounts(draw.Deck, guesses);
        var onLastCard = at + 1 >= HigherLower.Cards - 1;
        if (guess == HigherLower.Skip)
        {
            if (!onLastCard)
            {
                return RoundSteps.Continue(roundId, seq, [guess]);
            }

            // Skipped onto the last card: no guess is left, so the round pays what it reached.
            var reached = counts.Count == 0 ? 0 : Multipliers.Pay(round.Stake, HigherLower.MultiplierBp(counts, draw.RateBp));
            return reached == 0 ? RoundSteps.Lose(roundId, seq, [guess]) : RoundSteps.CashOut(roundId, seq, [guess], reached);
        }

        if (!HigherLower.Allowed(counts, rank, guess, draw.RateBp))
        {
            return null;
        }

        if (!HigherLower.Wins(rank, next, guess))
        {
            return RoundSteps.Lose(roundId, seq, [guess]);
        }

        counts.Add(HigherLower.Count(rank, guess));
        var anyGuessLeft = HigherLower.Allowed(counts, next, HigherLower.Higher, draw.RateBp) || HigherLower.Allowed(counts, next, HigherLower.Lower, draw.RateBp);
        return !onLastCard && anyGuessLeft
            ? RoundSteps.Continue(roundId, seq, [guess])
            : RoundSteps.CashOut(roundId, seq, [guess], Multipliers.Pay(round.Stake, HigherLower.MultiplierBp(counts, draw.RateBp)));
    }

    /// <summary>The winning counts of the guesses won so far; skips add none.</summary>
    private static List<int> WinningCounts(int[] deck, List<int> guesses)
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
        var draw = SteppedRounds<HigherLowerView>.Drawn<Draw>(spins);
        var guesses = SteppedRounds<HigherLowerView>.Moves(spins);
        var lost = guesses.Where((g, i) => g != HigherLower.Skip && !HigherLower.Wins(HigherLower.Rank(draw.Deck[i]), HigherLower.Rank(draw.Deck[i + 1]), g)).Any();
        var counts = WinningCounts(draw.Deck, guesses);

        // Only the cards already turned are ever sent.
        var turned = draw.Deck.Take(guesses.Count + 1).ToArray();
        var open = round.Status == RoundStatus.Open;
        var rank = HigherLower.Rank(turned[^1]);
        long Next(int guess) => open && guesses.Count < HigherLower.Cards - 1 && HigherLower.Allowed(counts, rank, guess, draw.RateBp)
            ? HigherLower.MultiplierBp([.. counts, HigherLower.Count(rank, guess)], draw.RateBp)
            : 0;
        var status = open ? "open" : lost ? "busted" : round.Status is RoundStatus.Settled or RoundStatus.WinPending ? "cashed" : round.Status;
        return new HigherLowerView(round.RoundId, round.Stake, turned, guesses, HigherLower.MultiplierBp(counts, draw.RateBp), Next(HigherLower.Higher), Next(HigherLower.Lower),
            spins.Sum(s => s.Win), status, draw.Nonce, draw.ClientSeed, draw.ServerSeedHash, balance);
    }

    private sealed record Draw(int[] Deck, long Nonce, string ClientSeed, string ServerSeedHash, long RateBp = Multipliers.Base);
}
