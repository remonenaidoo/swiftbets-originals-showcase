using Microsoft.Extensions.Options;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Tests;

public sealed class RatesTests
{
    private sealed class Monitor(RtpOptions value) : IOptionsMonitor<RtpOptions>
    {
        public RtpOptions CurrentValue => value;

        public RtpOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<RtpOptions, string?> listener) => null;
    }

    [Fact]
    public void A_game_set_on_its_own_plays_at_its_own_rate_and_the_rest_at_the_default()
    {
        var rates = new GameRates(new Monitor(new RtpOptions { Default = 80m, Games = { ["dice"] = 85.5m } }));

        rates.For("dice").ShouldBe(8_550);
        rates.For("crash").ShouldBe(8_000);
    }

    [Fact]
    public void A_setting_out_of_range_plays_at_the_design_rate() =>
        new GameRates(new Monitor(new RtpOptions { Default = 800m })).For("dice").ShouldBe(Mult.Base);
}
