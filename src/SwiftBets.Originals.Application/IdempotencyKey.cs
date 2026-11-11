namespace SwiftBets.Originals.Application;

internal static class IdempotencyKey
{
    public const int MaxLength = 100;

    public static bool IsValid(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= MaxLength;
}
