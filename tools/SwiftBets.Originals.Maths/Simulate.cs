using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Maths;

/// <summary>
/// Plays rounds through the production <see cref="RoundEngine"/> with the production RNG and checks the exact RTP
/// lies inside the simulation's 99% confidence interval. When a round hits the cap, the forfeited remainder is
/// played on (outside the engine, measurement only) so the uncapped RTP can be compared and the cap's cost measured.
/// </summary>
public static class Simulate
{
    public static int Run(GameDefinition game, long rounds, string resultPath)
    {
        var exact = Rtp.Compute(game);
        var clock = Stopwatch.StartNew();
        var workers = Environment.ProcessorCount;
        var parts = new Tally[workers];
        Parallel.For(0, workers, w =>
        {
            var engine = new RoundEngine(game, RandomNumberGenerator.GetInt32);
            var tally = new Tally();
            var mine = (rounds / workers) + (w < rounds % workers ? 1 : 0);
            for (long i = 0; i < mine; i++)
            {
                var round = engine.PlayRound();
                var paid = (double)round.PaidUnits / game.LineCount;
                var forfeited = 0.0;
                if (round.CapReached)
                {
                    forfeited = Forfeited(engine, game, round) / game.LineCount;
                    tally.Capped++;
                }

                tally.Add(paid, paid + forfeited);
                if (w == 0 && i % 10_000_000 == 0 && i > 0)
                {
                    Console.Error.WriteLine($"  {i * workers:N0} rounds, {clock.Elapsed.TotalMinutes:F1} min");
                }
            }

            parts[w] = tally;
        });

        var t = parts.Aggregate(new Tally(), (a, b) => a.Merge(b));
        var mean = t.UncappedSum / t.N;
        var sigma = Math.Sqrt((t.UncappedSquares / t.N) - (mean * mean));
        var half = 2.576 * sigma / Math.Sqrt(t.N);
        var cappedRtp = t.Sum / t.N;
        var cappedSigma = Math.Sqrt((t.Squares / t.N) - (cappedRtp * cappedRtp));
        var inside = Math.Abs(exact.Total - mean) <= half;
        Console.WriteLine($"rounds        {t.N:N0}  ({clock.Elapsed.TotalMinutes:F1} min)");
        Console.WriteLine($"exact RTP     {exact.Total:P4} (uncapped)");
        Console.WriteLine($"simulated     {mean:P4} ± {half:P4} (99% CI, uncapped)  -> {(inside ? "INSIDE" : "OUTSIDE")}");
        Console.WriteLine($"as played     {cappedRtp:P4}  cap effect {mean - cappedRtp:P5}  capped rounds {t.Capped:N0}  max {t.Max:F2}x");
        var result = new SimulationResult((long)t.N, cappedRtp, mean - half, mean + half, mean - cappedRtp, cappedSigma, t.Capped, t.Max);
        File.WriteAllText(resultPath, JsonSerializer.Serialize(result));
        return inside ? 0 : 1;
    }

    private static double Forfeited(RoundEngine engine, GameDefinition game, RoundResult round)
    {
        long total = round.Spins.Sum(s => s.TotalUnits);
        var left = round.Spins.Count == 1 ? (round.Spins[0].TriggersFeature ? game.FreeSpinsAwarded : 0) : FreeLeft(game, round);
        while (left > 0)
        {
            var spin = engine.PlaySpin(free: true);
            total += spin.TotalUnits;
            left += (spin.TriggersFeature ? game.FreeSpinsAwarded : 0) - 1;
        }

        return total - round.PaidUnits;
    }

    private static int FreeLeft(GameDefinition game, RoundResult round)
    {
        var left = round.Spins[0].TriggersFeature ? game.FreeSpinsAwarded : 0;
        foreach (var spin in round.Spins.Skip(1))
        {
            left += (spin.TriggersFeature ? game.FreeSpinsAwarded : 0) - 1;
        }

        return left;
    }

    private sealed class Tally
    {
        public double N;
        public double Sum;
        public double Squares;
        public double UncappedSum;
        public double UncappedSquares;
        public long Capped;
        public double Max;

        public void Add(double paid, double uncapped)
        {
            N++;
            Sum += paid;
            Squares += paid * paid;
            UncappedSum += uncapped;
            UncappedSquares += uncapped * uncapped;
            Max = Math.Max(Max, paid);
        }

        public Tally Merge(Tally o)
        {
            N += o.N; Sum += o.Sum; Squares += o.Squares; UncappedSum += o.UncappedSum; UncappedSquares += o.UncappedSquares;
            Capped += o.Capped; Max = Math.Max(Max, o.Max);
            return this;
        }
    }
}
