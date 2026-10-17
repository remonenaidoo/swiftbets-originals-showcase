using System.Diagnostics;
using System.Security.Cryptography;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Maths;

/// <summary>A feature valued by simulation: mean win per feature (line-bet units, uncapped) with its 99% confidence half-width.</summary>
public sealed record FeatureValue(long Features, double MeanUnits, double HalfWidthUnits, double MeanSpins, double SigmaUnits);

/// <summary>
/// For features that carry state from spin to spin (locked symbols), which full enumeration cannot value: plays
/// whole features through the production <see cref="RoundEngine"/> with the production RNG, without the cap (the
/// cap's cost is measured by the round simulation), and reports the mean with a 99% confidence interval.
/// </summary>
public static class FeatureSim
{
    public static FeatureValue Run(GameDefinition game, long features, bool quiet = false)
    {
        var uncapped = game with { MaxWinStakes = int.MaxValue };
        var clock = Stopwatch.StartNew();
        var workers = Environment.ProcessorCount;
        var sums = new double[workers];
        var squares = new double[workers];
        var spins = new long[workers];
        Parallel.For(0, workers, w =>
        {
            var engine = new RoundEngine(uncapped, RandomNumberGenerator.GetInt32);
            var list = new List<SpinOutcome>(16);
            var mine = (features / workers) + (w < features % workers ? 1 : 0);
            double sum = 0, square = 0;
            long played = 0;
            for (long i = 0; i < mine; i++)
            {
                list.Clear();
                var (paid, _) = engine.PlayFeature(0, list);
                sum += paid;
                square += (double)paid * paid;
                played += list.Count;
                if (!quiet && w == 0 && i % 2_000_000 == 0 && i > 0)
                {
                    Console.Error.WriteLine($"  {i * workers:N0} features, {clock.Elapsed.TotalMinutes:F1} min");
                }
            }

            sums[w] = sum;
            squares[w] = square;
            spins[w] = played;
        });

        double n = features;
        var mean = sums.Sum() / n;
        var sigma = Math.Sqrt((squares.Sum() / n) - (mean * mean));
        return new FeatureValue(features, mean, 2.576 * sigma / Math.Sqrt(n), spins.Sum() / n, sigma);
    }
}
