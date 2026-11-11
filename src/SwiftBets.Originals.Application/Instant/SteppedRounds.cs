using System.Text.Json;
using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Application.Instant;

/// <summary>The round lifecycle Mines, Towers and Higher or Lower share: a stored draw, the stake, then locked steps until a cash-out or a loss.</summary>
internal sealed class SteppedRounds<TView>(
    string gameId,
    IRoundStore store,
    IWallet wallet,
    ISeedStore seeds,
    TimeProvider time,
    Func<RoundRecord, IReadOnlyList<SpinRecord>, long?, TView> view)
    where TView : class
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Cashier cashier = new(store, wallet);

    public async Task<Result<TView>> StartAsync<TDraw>(string sessionToken, long stake, string idempotencyKey, Func<PlayerSeeds, TDraw> draw, CancellationToken cancellationToken)
    {
        var identity = await wallet.IdentifyAsync(sessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        var player = identity.Value;
        if (await store.FindByKeyAsync(player.Ref, idempotencyKey, cancellationToken) is { } repeat)
        {
            if (repeat.GameId != gameId)
            {
                return GameError.KeyInUse;
            }

            // The first request may still be taking the stake: never answer with a half-made round.
            return repeat.Status == RoundStatus.Pending ? GameError.RoundInProgress : view(repeat, await store.SpinsAsync(repeat.RoundId, cancellationToken), player.Balance);
        }

        if (await store.FindOpenAsync(player.Ref, gameId, cancellationToken) is not null)
        {
            return GameError.RoundInProgress;
        }

        var drawn = draw(await seeds.TakeNonceAsync(player.Ref, cancellationToken));
        var roundId = Guid.NewGuid();
        var round = new RoundRecord(roundId, player.Ref, sessionToken, gameId, stake, idempotencyKey, RoundStatus.Pending, 0, 0, time.GetUtcNow());
        var first = new SpinRecord(roundId, 0, [], JsonSerializer.Serialize(drawn, Json), 0, TransactionIds.Bet(roundId), null, false);
        await store.InsertAsync(round, first, cancellationToken);

        var staked = await cashier.TakeStakeAsync(round, cancellationToken);
        if (staked.Failed)
        {
            return staked.Error;
        }

        await store.UpdateAsync(roundId, RoundStatus.Open, 0, 0, cancellationToken);
        return view(round with { Status = RoundStatus.Open }, [first], staked.Value);
    }

    /// <summary>Applies one step under the round's lock; <paramref name="declined"/> is the error when the step itself returns null.</summary>
    public async Task<Result<TView>> StepAsync(string sessionToken, Guid roundId, Func<RoundRecord, IReadOnlyList<SpinRecord>, RoundStep?> step, GameError declined, CancellationToken cancellationToken)
    {
        var identity = await wallet.IdentifyAsync(sessionToken, cancellationToken);
        if (identity.Failed)
        {
            return identity.Error;
        }

        var player = identity.Value;
        if (await store.StepAsync(roundId, player.Ref, gameId, step, cancellationToken) is not { } played)
        {
            var current = await store.GetAsync(roundId, cancellationToken);
            return current is null || current.PlayerRef != player.Ref || current.GameId != gameId ? GameError.RoundNotFound
                : current.Status != RoundStatus.Open ? GameError.RoundOver
                : declined;
        }

        var balance = await cashier.PayWinAsync(played.Round, played.Spin, player.Balance, cancellationToken);
        return view(played.Round, await store.SpinsAsync(roundId, cancellationToken), balance);
    }

    public async Task<TView?> OpenAsync(string playerRef, long? balance, CancellationToken cancellationToken) =>
        await store.FindOpenAsync(playerRef, gameId, cancellationToken) is { } round ? view(round, await store.SpinsAsync(round.RoundId, cancellationToken), balance) : null;

    public static TDraw Drawn<TDraw>(IReadOnlyList<SpinRecord> spins) => JsonSerializer.Deserialize<TDraw>(spins[0].OutcomeJson, Json)!;

    /// <summary>The tile or guess of each step after the draw; a cash-out step has none.</summary>
    public static List<int> Moves(IReadOnlyList<SpinRecord> spins) => [.. spins.Skip(1).Where(s => s.Stops.Length == 1).Select(s => s.Stops[0])];
}

internal static class RoundSteps
{
    public static RoundStep Continue(Guid roundId, int seq, int[] stops) => new(new SpinRecord(roundId, seq, stops, "{}", 0, null, null, false), RoundStatus.Open, 0);

    public static RoundStep Lose(Guid roundId, int seq, int[] stops) => new(new SpinRecord(roundId, seq, stops, "{}", 0, null, null, false), RoundStatus.Settled, 0);

    public static RoundStep CashOut(Guid roundId, int seq, int[] stops, long win) =>
        new(new SpinRecord(roundId, seq, stops, "{}", win, null, TransactionIds.Win(roundId, seq), false), RoundStatus.WinPending, win);
}
