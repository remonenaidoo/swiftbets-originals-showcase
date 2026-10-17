using System.Security.Cryptography;
using System.Text;

namespace SwiftBets.Originals.Domain.Instant;

/// <summary>
/// Provably fair draws. The server seed (from the OS CSPRNG) is shown hashed before play and revealed when the player
/// rotates it, so any round can be replayed: each block of floats is HMAC-SHA256(serverSeed, "clientSeed:nonce:cursor"),
/// read 4 bytes per float.
/// </summary>
public static class Fair
{
    public static string NewSeed() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string serverSeed) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(serverSeed)));

    public static double[] Floats(string serverSeed, string clientSeed, long nonce, int count)
    {
        var key = Encoding.UTF8.GetBytes(serverSeed);
        var floats = new double[count];
        for (var cursor = 0; cursor * 8 < count; cursor++)
        {
            var block = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{clientSeed}:{nonce}:{cursor}"));
            for (var i = 0; i < 8 && cursor * 8 + i < count; i++)
            {
                // Four bytes as a fraction in [0, 1): b0/256 + b1/256² + b2/256³ + b3/256⁴.
                floats[cursor * 8 + i] = (block[i * 4] / 256d) + (block[(i * 4) + 1] / 65536d) + (block[(i * 4) + 2] / 16777216d) + (block[(i * 4) + 3] / 4294967296d);
            }
        }

        return floats;
    }
}
