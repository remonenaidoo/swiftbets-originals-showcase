namespace SwiftBets.Originals.Domain.Slot;

/// <summary>
/// Lays out a reel strip from symbol counts, spreading each symbol as evenly as it can (largest-deficit first,
/// ties in symbol-table order). Deterministic: the same counts always give the same strip, so the counts are the
/// design and the strip printed in the PAR sheet is reproducible.
/// </summary>
public static class Strip
{
    public static IReadOnlyList<byte> Build(IReadOnlyDictionary<byte, int> counts)
    {
        var length = counts.Values.Sum();
        var placed = counts.Keys.ToDictionary(s => s, _ => 0);
        var strip = new byte[length];
        for (var position = 0; position < length; position++)
        {
            var best = default(byte);
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

    /// <summary>Counts keyed by symbol name, for game definitions that read like their PAR sheets.</summary>
    public static IReadOnlyList<byte> Build(IReadOnlyList<SymbolDef> symbols, IReadOnlyDictionary<string, int> counts) =>
        Build(counts.Where(c => c.Value > 0).ToDictionary(c => (byte)IndexOf(symbols, c.Key), c => c.Value));

    private static int IndexOf(IReadOnlyList<SymbolDef> symbols, string name)
    {
        for (var i = 0; i < symbols.Count; i++)
        {
            if (symbols[i].Name == name)
            {
                return i;
            }
        }

        throw new ArgumentException($"No symbol named {name}.", nameof(name));
    }
}
