namespace SwiftBets.Originals.Domain.Slot;

/// <summary>A whole round: the base spin, the free spins it triggered, and what was paid under the cap.</summary>
public sealed record RoundResult(IReadOnlyList<SpinOutcome> Spins, long PaidUnits, bool CapReached);

/// <summary>
/// Plays the game. The live service calls <see cref="PlaySpin"/> one spin at a time (its round state lives in the
/// database); the simulation calls <see cref="PlayRound"/>. Both draw through the same <c>draw</c> and evaluate
/// with the same evaluator, so the simulation tests the code that pays players.
/// </summary>
/// <param name="game">The game definition.</param>
/// <param name="draw">Returns a uniform integer in [0, n); production passes <c>RandomNumberGenerator.GetInt32</c>.</param>
public sealed class RoundEngine(GameDefinition game, Func<int, int> draw)
{
    public GameDefinition Game => game;

    public SpinOutcome PlaySpin(bool free)
    {
        var reels = free ? game.FreeReels : game.BaseReels;
        var stops = new int[reels.Count];
        for (var reel = 0; reel < reels.Count; reel++)
        {
            stops[reel] = draw(reels[reel].Count);
        }

        return Evaluator.Evaluate(game, reels, stops, free ? game.FreeSpinMultiplier : 1);
    }

    public RoundResult PlayRound()
    {
        var spins = new List<SpinOutcome>();
        var first = PlaySpin(free: false);
        spins.Add(first);
        var (paid, capped) = Cap.Apply(0, first.TotalUnits, game.CapUnits);
        var left = first.TriggersFeature ? game.FreeSpinsAwarded : 0;
        while (left > 0 && !capped)
        {
            var spin = PlaySpin(free: true);
            spins.Add(spin);
            var (more, reached) = Cap.Apply(paid, spin.TotalUnits, game.CapUnits);
            paid += more;
            capped = reached;
            left += (spin.TriggersFeature ? game.FreeSpinsAwarded : 0) - 1;
        }

        return new RoundResult(spins, paid, capped);
    }
}
