using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Instant;

public sealed record TowersStart(string SessionToken, long Stake, string Level, string IdempotencyKey);

/// <summary>A Towers round as the client sees it; <paramref name="Traps"/> only once the round is over.</summary>
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

public sealed class TowersHandler
{
    private static readonly int WidestRow = Towers.Levels.Values.Max(l => l.Tiles);
    private readonly SteppedRounds<TowersView> rounds;
    private readonly IGameRates rates;

    public TowersHandler(IRoundStore store, IWallet wallet, ISeedStore seeds, TimeProvider time, IGameRates rates)
    {
        rounds = new(Towers.GameId, store, wallet, seeds, time, View);
        this.rates = rates;
    }

    public Task<Result<TowersView>> StartAsync(TowersStart request, CancellationToken cancellationToken)
    {
        if (Array.IndexOf(InstantStakes.Offered, request.Stake) < 0 || request.Level is null || !Towers.Levels.ContainsKey(request.Level) || !IdempotencyKey.IsValid(request.IdempotencyKey))
        {
            return Task.FromResult<Result<TowersView>>(GameError.InvalidStake);
        }

        var rate = rates.For(Towers.GameId);
        return rounds.StartAsync(request.SessionToken, request.Stake, request.IdempotencyKey, seeds =>
            new Draw(request.Level, Towers.Layout(Fair.Floats(seeds.ServerSeed, seeds.ClientSeed, seeds.Nonce, Towers.FloatsNeeded), request.Level), seeds.Nonce, seeds.ClientSeed, seeds.ServerSeedHash, rate),
            cancellationToken);
    }

    public Task<Result<TowersView>> PickAsync(string sessionToken, Guid roundId, int tile, CancellationToken cancellationToken)
    {
        if (tile < 0 || tile >= WidestRow)
        {
            return Task.FromResult<Result<TowersView>>(GameError.TileTaken);
        }

        return rounds.StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var draw = SteppedRounds<TowersView>.Drawn<Draw>(spins);
            var row = SteppedRounds<TowersView>.Moves(spins).Count;
            if (tile >= Towers.Levels[draw.Level].Tiles || row >= Towers.Rows)
            {
                return null;
            }

            var seq = spins.Count;
            if (draw.Layout[row].Contains(tile))
            {
                return RoundSteps.Lose(roundId, seq, [tile]);
            }

            return Towers.Finished(draw.Level, row + 1, draw.RateBp)
                ? RoundSteps.CashOut(roundId, seq, [tile], Multipliers.Pay(round.Stake, Towers.MultiplierBp(draw.Level, row + 1, draw.RateBp)))
                : RoundSteps.Continue(roundId, seq, [tile]);
        }, GameError.TileTaken, cancellationToken);
    }

    public Task<Result<TowersView>> CashoutAsync(string sessionToken, Guid roundId, CancellationToken cancellationToken) =>
        rounds.StepAsync(sessionToken, roundId, (round, spins) =>
        {
            var safe = SteppedRounds<TowersView>.Moves(spins).Count;
            if (safe == 0)
            {
                return null;
            }

            var draw = SteppedRounds<TowersView>.Drawn<Draw>(spins);
            return RoundSteps.CashOut(roundId, spins.Count, [], Multipliers.Pay(round.Stake, Towers.MultiplierBp(draw.Level, safe, draw.RateBp)));
        }, GameError.NothingToCashOut, cancellationToken);

    public Task<TowersView?> OpenAsync(string playerRef, long? balance, CancellationToken cancellationToken) => rounds.OpenAsync(playerRef, balance, cancellationToken);

    private static TowersView View(RoundRecord round, IReadOnlyList<SpinRecord> spins, long? balance)
    {
        var draw = SteppedRounds<TowersView>.Drawn<Draw>(spins);
        var picks = SteppedRounds<TowersView>.Moves(spins);
        var fell = picks.Select((p, row) => draw.Layout[row].Contains(p)).Any(trap => trap);
        var safe = picks.Count - (fell ? 1 : 0);
        var playedOut = round.Status is RoundStatus.Settled or RoundStatus.WinPending;
        var status = round.Status == RoundStatus.Open ? "open" : fell ? "busted" : playedOut ? "cashed" : round.Status;
        return new TowersView(round.RoundId, round.Stake, draw.Level, picks, Towers.MultiplierBp(draw.Level, safe, draw.RateBp), Towers.MultiplierBp(draw.Level, safe + 1, draw.RateBp),
            spins.Sum(s => s.Win), status, playedOut ? draw.Layout : null, draw.Nonce, draw.ClientSeed, draw.ServerSeedHash, balance);
    }

    private sealed record Draw(string Level, int[][] Layout, long Nonce, string ClientSeed, string ServerSeedHash, long RateBp = Multipliers.Base);
}
