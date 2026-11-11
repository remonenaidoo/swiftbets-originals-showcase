using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

public sealed record MinesStart(string SessionToken, long Stake, int Mines, string IdempotencyKey);

/// <summary>A Mines round as the client sees it; <paramref name="MineTiles"/> only once the round is over.</summary>
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

public sealed class MinesHandler
{
    private readonly SteppedRounds<MinesView> rounds;
    private readonly IGameRates rates;

    public MinesHandler(IRoundStore store, IWallet wallet, ISeedStore seeds, TimeProvider time, IGameRates rates)
    {
        rounds = new(Mines.GameId, store, wallet, seeds, time, View);
        this.rates = rates;
    }

    public Task<Result<MinesView>> StartAsync(MinesStart request, CancellationToken cancellationToken)
    {
        if (Array.IndexOf(InstantStakes.Offered, request.Stake) < 0 || request.Mines is < 1 or > Mines.Tiles - 1 || !IdempotencyKey.IsValid(request.IdempotencyKey))
        {
            return Task.FromResult<Result<MinesView>>(GameError.InvalidStake);
        }

        var rate = rates.For(Mines.GameId);
        return rounds.StartAsync(request.SessionToken, request.Stake, request.IdempotencyKey, seeds =>
            new Draw(request.Mines, Mines.Layout(Fair.Floats(seeds.ServerSeed, seeds.ClientSeed, seeds.Nonce, Mines.Tiles), request.Mines), seeds.Nonce, seeds.ClientSeed, seeds.ServerSeedHash, rate),
            cancellationToken);
    }

    public Task<Result<MinesView>> PickAsync(string sessionToken, Guid roundId, int tile, CancellationToken cancellationToken)
    {
        if (tile is < 0 or >= Mines.Tiles)
        {
            return Task.FromResult<Result<MinesView>>(GameError.TileTaken);
        }

        return rounds.StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var draw = SteppedRounds<MinesView>.Drawn<Draw>(spins);
            var picks = SteppedRounds<MinesView>.Moves(spins);
            if (picks.Contains(tile))
            {
                return null;
            }

            var seq = spins.Count;
            if (draw.Layout.Contains(tile))
            {
                return RoundSteps.Lose(roundId, seq, [tile]);
            }

            var safe = picks.Count + 1;
            return Mines.Finished(draw.Mines, safe, draw.RateBp)
                ? RoundSteps.CashOut(roundId, seq, [tile], Multipliers.Pay(round.Stake, Mines.MultiplierBp(draw.Mines, safe, draw.RateBp)))
                : RoundSteps.Continue(roundId, seq, [tile]);
        }, GameError.TileTaken, cancellationToken);
    }

    public Task<Result<MinesView>> CashoutAsync(string sessionToken, Guid roundId, CancellationToken cancellationToken) =>
        rounds.StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var safe = SteppedRounds<MinesView>.Moves(spins).Count;
            if (safe == 0)
            {
                return null;
            }

            var draw = SteppedRounds<MinesView>.Drawn<Draw>(spins);
            return RoundSteps.CashOut(roundId, spins.Count, [], Multipliers.Pay(round.Stake, Mines.MultiplierBp(draw.Mines, safe, draw.RateBp)));
        }, GameError.NothingToCashOut, cancellationToken);

    public Task<MinesView?> OpenAsync(string playerRef, long? balance, CancellationToken cancellationToken) => rounds.OpenAsync(playerRef, balance, cancellationToken);

    private static MinesView View(RoundRecord round, IReadOnlyList<SpinRecord> spins, long? balance)
    {
        var draw = SteppedRounds<MinesView>.Drawn<Draw>(spins);
        var picks = SteppedRounds<MinesView>.Moves(spins);
        var busted = picks.Any(draw.Layout.Contains);
        var safe = picks.Count(p => !draw.Layout.Contains(p));
        var playedOut = round.Status is RoundStatus.Settled or RoundStatus.WinPending;
        var status = round.Status == RoundStatus.Open ? "open" : busted ? "busted" : playedOut ? "cashed" : round.Status;

        // A refused or voided round's draw is never shown.
        return new MinesView(round.RoundId, round.Stake, draw.Mines, picks, Mines.MultiplierBp(draw.Mines, safe, draw.RateBp), Mines.MultiplierBp(draw.Mines, safe + 1, draw.RateBp),
            spins.Sum(s => s.Win), status, playedOut ? draw.Layout : null, draw.Nonce, draw.ClientSeed, draw.ServerSeedHash, balance);
    }

    /// <summary>Stored as the round's first spin; rounds stored before per-game rates default to the design rate.</summary>
    private sealed record Draw(int Mines, int[] Layout, long Nonce, string ClientSeed, string ServerSeedHash, long RateBp = Multipliers.Base);
}
