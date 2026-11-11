namespace SwiftBets.Originals.Domain.Slot;

public sealed record RoundResult(IReadOnlyList<SpinOutcome> Spins, long PaidUnits, bool CapReached);

/// <summary>The live service and the simulation draw and evaluate through this same code, so the simulation tests what pays players.</summary>
public sealed class RoundEngine(GameDefinition game, Func<int, int> draw)
{
    public GameDefinition Game => game;

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
