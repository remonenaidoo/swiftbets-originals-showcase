namespace SwiftBets.Originals.Domain.Slot;

/// <summary>
/// Lays out a reel strip from symbol counts, spreading each symbol as evenly as it can (largest-deficit first,
/// ties in enum order). Deterministic: the same counts always give the same strip, so the counts are the design
/// and the strip printed in the PAR sheet is reproducible.
/// </summary>
public static class Strip
{
    public static IReadOnlyList<Symbol> Build(IReadOnlyDictionary<Symbol, int> counts)
    {
        var length = counts.Values.Sum();
        var placed = counts.Keys.ToDictionary(s => s, _ => 0);
        var strip = new Symbol[length];
        for (var position = 0; position < length; position++)
        {
            var best = default(Symbol);
            var bestDeficit = double.MinValue;
            foreach (var (symbol, count) in counts.OrderBy(c => c.Key))
            {
                if (placed[symbol] >= count)
                {
                    continue;
                }

                var deficit = ((position + 1) * (double)count / length) - placed[symbol];
                if (deficit > bestDeficit + 1e-12)
                {
                    best = symbol;
                    bestDeficit = deficit;
                }
            }

            strip[position] = best;
            placed[best]++;
        }

        return strip;
    }
}
