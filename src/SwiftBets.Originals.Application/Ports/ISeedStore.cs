namespace SwiftBets.Originals.Application.Ports;

/// <summary>A player's provably-fair seeds: the active server seed is only ever shown hashed, the previous pair is revealed after a rotation.</summary>
public sealed record PlayerSeeds(string ServerSeed, string ServerSeedHash, string ClientSeed, long Nonce, string? PreviousServerSeed, string? PreviousClientSeed, long? PreviousNonce, string NextServerSeedHash = "");

public interface ISeedStore
{
    /// <summary>The player's seeds, created on first use.</summary>
    Task<PlayerSeeds> GetAsync(string playerRef, CancellationToken cancellationToken);

    /// <summary>Atomically takes the next nonce; the returned seeds carry the nonce this round uses.</summary>
    Task<PlayerSeeds> TakeNonceAsync(string playerRef, CancellationToken cancellationToken);

    /// <summary>Reveals the active seed and starts the committed next one; null while a round could still be using the active seed.</summary>
    Task<PlayerSeeds?> RotateAsync(string playerRef, string clientSeed, CancellationToken cancellationToken);
}
