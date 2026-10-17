using System.Numerics;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Maths;

/// <summary>
/// The RTP from the enumerations: base = ΣW / (cycle × lines); P(trigger) = triggers / cycle. A stateless feature
/// is exact in closed form: per free spin w = ΣW(×multiplier) / cycle units, r = extra spins awarded per free spin
/// (0 without retrigger), expected spins N = n / (1 − r), value V = w × N. A feature with locked symbols takes V
/// from <see cref="FeatureSim"/>. Feature RTP = P(trigger) × V / lines.
/// </summary>
public sealed record Rtp(ReelStats Base, ReelStats Free, int Lines, int Awarded, FeatureValue? Simulated)
{
    public bool Exact => Simulated is null;
    public double BaseRtp => (double)Base.TotalUnits / ((double)Base.Cycle * Lines);
    public double TriggerProbability => (double)Base.Triggers / (double)Base.Cycle;
    public double WinPerFreeSpin => (double)Free.TotalUnits / (double)Free.Cycle;
    public double RetriggerSpinsPerSpin => (double)Free.RetriggerSpinsAwarded / (double)Free.Cycle;
    public double ExpectedFreeSpins => Simulated?.MeanSpins ?? Awarded / (1 - RetriggerSpinsPerSpin);
    public double FeatureValueUnits => Simulated?.MeanUnits ?? WinPerFreeSpin * ExpectedFreeSpins;
    public double FeatureRtp => TriggerProbability * FeatureValueUnits / Lines;

    /// <summary>The 99% half-width of the feature RTP (0 when exact).</summary>
    public double FeatureRtpHalfWidth => Simulated is { } s ? TriggerProbability * s.HalfWidthUnits / Lines : 0;
    public double Total => BaseRtp + FeatureRtp;
    public double HitFrequency => (double)Base.Hits / (double)Base.Cycle;

    /// <summary>Standard deviation of one base spin's win, in total stakes, exact from the win distribution.</summary>
    public double BaseSigma
    {
        get
        {
            double mean = 0, square = 0, cycle = (double)Base.Cycle;
            foreach (var (units, count) in Base.CountByWinUnits)
            {
                var x = (double)units / Lines;
                mean += x * (double)count / cycle;
                square += x * x * (double)count / cycle;
            }

            return Math.Sqrt(square - (mean * mean));
        }
    }

    /// <summary>The RTP; a locking feature is simulated with <paramref name="features"/> features.</summary>
    public static Rtp Compute(GameDefinition game, long features = 100_000_000, bool quiet = false) =>
        new(Enumerate.Run(game, game.BaseReels, 1), Enumerate.Run(game, game.FreeReels, game.FreeSpinMultiplier), game.LineCount, game.FreeSpinsAwarded,
            game.Feature.LockSplits ? FeatureSim.Run(game, features, quiet) : null);

    public static string Fraction(BigInteger numerator, BigInteger denominator)
    {
        var g = BigInteger.GreatestCommonDivisor(numerator, denominator);
        return $"{numerator / g}/{denominator / g}";
    }
}
