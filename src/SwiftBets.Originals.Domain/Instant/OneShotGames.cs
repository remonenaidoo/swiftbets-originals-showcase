namespace SwiftBets.Originals.Domain.Instant;

public static class OneShotGames
{
    public static IReadOnlyDictionary<string, OneShotGame> All { get; } =
        new OneShotGame[]
        {
            new Dice(), new Limbo(), new PegDrop(), new Keno(), new WaysSlot(), new HoldSlot(), new CascadeSlot(),
            new SunburstWheel(), new CoinFlip(), new LuckyScratch(), new GemHand(), new Baccarat(),
            new WaysSlot("sardine-run", "Sardine Run"), new HoldSlot("kimberley-hold", "Kimberley Hold"), new CascadeSlot("fynbos-bloom", "Fynbos Bloom"),
        }.ToDictionary(g => g.Id);
}
