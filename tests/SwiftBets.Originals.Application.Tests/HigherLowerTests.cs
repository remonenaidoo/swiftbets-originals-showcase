using SwiftBets.Originals.Application.Instant;
using SwiftBets.Originals.Domain.Instant;

namespace SwiftBets.Originals.Application.Tests;

public sealed class HigherLowerTests : IDisposable
{
    private readonly FakeStore store = new();
    private readonly FakeWallet wallet = new();
    private readonly FakeSeeds seeds = new();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    public void Dispose() => store.Dispose();

    private HigherLowerHandler Handler() => new(store, wallet, seeds, TimeProvider.System);

    /// <summary>The fake seeds' first round (nonce 0): the deck in order.</summary>
    private static int[] Ranks() => [.. HigherLower.Deal(Fair.Floats("server-seed", "client", 0, HigherLower.Cards)).Select(HigherLower.Rank)];

    /// <summary>The guess that wins on card <paramref name="i"/> (both can, at a tie), or −1 if only a refused guess would.</summary>
    private static int Winning(int i)
    {
        var (rank, next) = (Ranks()[i], Ranks()[i + 1]);
        foreach (var g in new[] { HigherLower.Higher, HigherLower.Lower })
        {
            if (HigherLower.Wins(rank, next, g) && HigherLower.Count(rank, g) < 13)
            {
                return g;
            }
        }

        return -1;
    }

    [Fact]
    public async Task A_winning_guess_then_cashout_pays_its_multiplier()
    {
        var start = (await Handler().StartAsync(new HigherLowerStart("s", 1000, "k1"), ct)).Value!;
        // Skip until a card a guess can win on, then guess it and cash out.
        var i = 0;
        while (Winning(i) < 0)
        {
            await Handler().GuessAsync("s", start.RoundId, HigherLower.Skip, ct);
            i++;
        }

        var guess = Winning(i);
        await Handler().GuessAsync("s", start.RoundId, guess, ct);
        var cash = (await Handler().CashoutAsync("s", start.RoundId, ct)).Value!;

        cash.Win.ShouldBe(Mult.Pay(1000, HigherLower.MultiplierBp([HigherLower.Count(Ranks()[i], guess)])));
        wallet.Balance.ShouldBe(100_000 - 1000 + cash.Win);
    }

    [Fact]
    public async Task A_guess_after_a_loss_is_refused()
    {
        var start = (await Handler().StartAsync(new HigherLowerStart("s", 1000, "k1"), ct)).Value!;
        var i = 0;
        // Skip until a guess can lose, then make it.
        int losing() => new[] { HigherLower.Higher, HigherLower.Lower }.FirstOrDefault(g => HigherLower.Count(Ranks()[i], g) < 13 && !HigherLower.Wins(Ranks()[i], Ranks()[i + 1], g), -1);
        while (losing() < 0)
        {
            await Handler().GuessAsync("s", start.RoundId, HigherLower.Skip, ct);
            i++;
        }

        (await Handler().GuessAsync("s", start.RoundId, losing(), ct)).Value!.Status.ShouldBe("busted");
        var again = await Handler().GuessAsync("s", start.RoundId, HigherLower.Skip, ct);

        again.Error.ShouldBe(SpinError.RoundOver);
        wallet.Balance.ShouldBe(100_000 - 1000);
    }

    [Fact]
    public async Task Skipping_to_the_last_card_ends_the_round_so_a_new_one_can_start()
    {
        var start = (await Handler().StartAsync(new HigherLowerStart("s", 1000, "k1"), ct)).Value!;
        HigherLowerView last = start;
        for (var i = 0; i < HigherLower.Cards - 1 && last.Status == "open"; i++)
        {
            last = (await Handler().GuessAsync("s", start.RoundId, HigherLower.Skip, ct)).Value!;
        }

        last.Status.ShouldNotBe("open");
        (await Handler().StartAsync(new HigherLowerStart("s", 1000, "k2"), ct)).Error.ShouldBeNull();
    }
}
