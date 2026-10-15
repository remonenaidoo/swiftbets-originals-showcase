using System.Numerics;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Maths;

/// <summary>
/// The exact RTP from the two enumerations:
/// base = ΣW / (cycle × lines); P(trigger) = triggers / cycle;
/// per free spin w = ΣW(×multiplier) / cycle units, r = extra spins awarded per free spin;
/// expected spins N = n / (1 − r); feature value V = w × N; feature RTP = P(trigger) × V / lines.
/// </summary>
public sealed record Rtp(ReelStats Base, ReelStats Free, int Lines, int Awarded)
{
    public double BaseRtp => (double)Base.TotalUnits / ((double)Base.Cycle * Lines);
    public double TriggerProbability => (double)Base.Triggers / (double)Base.Cycle;
    public double WinPerFreeSpin => (double)Free.TotalUnits / (double)Free.Cycle;
    public double RetriggerSpinsPerSpin => (double)Free.RetriggerSpinsAwarded / (double)Free.Cycle;
    public double ExpectedFreeSpins => Awarded / (1 - RetriggerSpinsPerSpin);
    public double FeatureValueUnits => WinPerFreeSpin * ExpectedFreeSpins;
    public double FeatureRtp => TriggerProbability * FeatureValueUnits / Lines;
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

    public static Rtp Compute(GameDefinition game) =>
        new(Enumerate.Run(game, game.BaseReels, 1), Enumerate.Run(game, game.FreeReels, game.FreeSpinMultiplier), game.LineCount, game.FreeSpinsAwarded);

    public static string Exact(BigInteger numerator, BigInteger denominator)
    {
        var g = BigInteger.GreatestCommonDivisor(numerator, denominator);
        return $"{numerator / g}/{denominator / g}";
    }
}
