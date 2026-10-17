namespace SwiftBets.Originals.Domain.Slot;

/// <summary>A whole round: the base spin, the free spins it triggered, and what was paid under the cap.</summary>
public sealed record RoundResult(IReadOnlyList<SpinOutcome> Spins, long PaidUnits, bool CapReached);

/// <summary>
/// Plays a game. The live service calls <see cref="PlaySpin"/> one spin at a time (its round state, including any
/// locked cells, lives in the database); the simulation calls <see cref="PlayRound"/> or <see cref="PlayFeature"/>.
/// All draw through the same <c>draw</c> and evaluate with the same evaluator, so the simulation tests the code
/// that pays players.
/// </summary>
/// <param name="game">The game definition.</param>
/// <param name="draw">Returns a uniform integer in [0, n); production passes <c>RandomNumberGenerator.GetInt32</c>.</param>
public sealed class RoundEngine(GameDefinition game, Func<int, int> draw)
{
    public GameDefinition Game => game;

    /// <summary>A base spin, or a free spin with the feature's locked cells so far.</summary>
    public SpinOutcome PlaySpin(bool free, IReadOnlyList<LockedCell>? locks = null)
    {
        var reels = free ? game.FreeReels : game.BaseReels;
        var stops = new int[reels.Count];
        for (var reel = 0; reel < reels.Count; reel++)
        {
            stops[reel] = draw(reels[reel].Count);
        }

        return free
            ? Evaluator.Evaluate(game, reels, stops, game.Feature.Multiplier, locks, game.Feature.LockSplits)
            : Evaluator.Evaluate(game, reels, stops, 1);
    }

    /// <summary>Spins left after a free spin: one used, plus a retrigger's award when the game retriggers.</summary>
    public int SpinsLeftAfter(int left, SpinOutcome freeSpin) =>
        left - 1 + (freeSpin.TriggersFeature && game.Feature.Retrigger ? game.Feature.Awarded : 0);

    public RoundResult PlayRound()
    {
        var spins = new List<SpinOutcome>();
        var first = PlaySpin(free: false);
        spins.Add(first);
        var (paid, capped) = Cap.Apply(0, first.TotalUnits, game.CapUnits);
        if (first.TriggersFeature && !capped)
        {
            (paid, capped) = PlayFeature(paid, spins);
        }

        return new RoundResult(spins, paid, capped);
    }

    /// <summary>Plays a whole feature after <paramref name="paidSoFar"/>, appending its spins; returns the round's paid units and whether the cap was reached.</summary>
    public (long Paid, bool Capped) PlayFeature(long paidSoFar, List<SpinOutcome> spins)
    {
        var paid = paidSoFar;
        var capped = false;
        var left = game.Feature.Awarded;
        IReadOnlyList<LockedCell> locks = [];
        while (left > 0 && !capped)
        {
            var spin = PlaySpin(free: true, locks);
            spins.Add(spin);
            var (more, reached) = Cap.Apply(paid, spin.TotalUnits, game.CapUnits);
            paid += more;
            capped = reached;
            locks = spin.Locked;
            left = SpinsLeftAfter(left, spin);
        }

        return (paid, capped);
    }
}
