using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Maths;

/// <summary>
/// Plays rounds through the production <see cref="RoundEngine"/> with the production RNG and checks the computed
/// RTP lies inside the simulation's 99% confidence interval. Rounds are played without the cap and the cap is
/// applied to each round's total afterwards: the same as capping spin by spin (once the cap is reached nothing more
/// is paid), and it gives the uncapped RTP to compare and the cap's cost in one run.
/// </summary>
public static class Simulate
{
    public static int Run(GameDefinition game, Rtp computed, long rounds, string resultPath)
    {
        var uncapped = game with { MaxWinStakes = int.MaxValue };
        var clock = Stopwatch.StartNew();
        var workers = Environment.ProcessorCount;
        var parts = new Tally[workers];
        Parallel.For(0, workers, w =>
        {
            var engine = new RoundEngine(uncapped, RandomNumberGenerator.GetInt32);
            var tally = new Tally();
            var mine = (rounds / workers) + (w < rounds % workers ? 1 : 0);
            for (long i = 0; i < mine; i++)
            {
                var total = engine.PlayRound().PaidUnits;
                var paid = Math.Min(total, game.CapUnits);
                if (paid < total)
                {
                    tally.Capped++;
                }

                tally.Add((double)paid / game.LineCount, (double)total / game.LineCount);
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
        // A simulated feature is itself an estimate: compare within both intervals combined.
        var tolerance = Math.Sqrt((half * half) + (computed.FeatureRtpHalfWidth * computed.FeatureRtpHalfWidth));
        var inside = Math.Abs(computed.Total - mean) <= tolerance;
        Console.WriteLine($"rounds        {t.N:N0}  ({clock.Elapsed.TotalMinutes:F1} min)");
        Console.WriteLine($"computed RTP  {computed.Total:P4} (uncapped{(computed.Exact ? ", exact" : $", feature ± {computed.FeatureRtpHalfWidth:P4}")})");
        Console.WriteLine($"simulated     {mean:P4} ± {half:P4} (99% CI, uncapped)  -> {(inside ? "INSIDE" : "OUTSIDE")}");
        Console.WriteLine($"as played     {cappedRtp:P4}  cap effect {mean - cappedRtp:P5}  capped rounds {t.Capped:N0}  max {t.Max:F2}x");
        var result = new SimulationResult((long)t.N, cappedRtp, mean - half, mean + half, mean - cappedRtp, cappedSigma, t.Capped, t.Max);
        File.WriteAllText(resultPath, JsonSerializer.Serialize(result));
        return inside ? 0 : 1;
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
