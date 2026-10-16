namespace SwiftBets.Originals.Application.Ports;

/// <summary>
/// A player's provably-fair seeds. <paramref name="ServerSeed"/> never leaves the server while active; the previous
/// pair is revealed after a rotation, covering nonces below <paramref name="PreviousNonce"/>. The next server seed is
/// fixed in advance and only its hash is shown, so it cannot be chosen to suit the client seed the player sets.
/// </summary>
public sealed record PlayerSeeds(string ServerSeed, string ServerSeedHash, string ClientSeed, long Nonce, string? PreviousServerSeed, string? PreviousClientSeed, long? PreviousNonce, string NextServerSeedHash = "");

public interface ISeedStore
{
    /// <summary>The player's seeds, created on first use.</summary>
    Task<PlayerSeeds> GetAsync(string playerRef, CancellationToken cancellationToken);

    /// <summary>Atomically takes the next nonce: the returned seeds carry the nonce this round uses.</summary>
    Task<PlayerSeeds> TakeNonceAsync(string playerRef, CancellationToken cancellationToken);

    /// <summary>
    /// Reveals the active server seed (as previous) and starts a new one with <paramref name="clientSeed"/> and nonce
    /// 0. Null (refused) when a nonce was taken in the last 30 seconds or an instant round is unfinished: a round being
    /// started under the active seed must never have that seed revealed.
    /// </summary>
    Task<PlayerSeeds?> RotateAsync(string playerRef, string clientSeed, CancellationToken cancellationToken);
}
