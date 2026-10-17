using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Domain.Tests;

public sealed class FairTests
{
    [Fact]
    public void Floats_are_reproducible_from_the_revealed_seed()
    {
        var a = Fair.Floats("server-seed", "client", 7, 30);
        var b = Fair.Floats("server-seed", "client", 7, 30);

        b.ShouldBe(a);
        a.ShouldAllBe(f => f >= 0 && f < 1);
        Fair.Hash("server-seed").ShouldBe("91024ec49c5bec0b689e42892526320fce08337205c91de94c7a588c20d08eeb");
    }

    [Fact]
    public void A_different_nonce_changes_the_floats() =>
        Fair.Floats("server-seed", "client", 8, 4).ShouldNotBe(Fair.Floats("server-seed", "client", 7, 4));
}
