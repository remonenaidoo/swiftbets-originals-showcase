using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Infrastructure;

/// <summary>
/// Calls the casino's seamless-wallet API as provider <c>swift-originals</c>: one signed JSON POST per action.
/// No answer (timeout or connection failure) is <see cref="WalletStatus.Unreachable"/>; the caller decides whether
/// that means roll back (a bet) or retry later (a win).
/// </summary>
public sealed class WalletClient(HttpClient http, IOptions<OriginalsOptions> options) : IWallet
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<WalletReply> BalanceAsync(string sessionToken, CancellationToken cancellationToken) =>
        CallAsync("balance", sessionToken, $"balance:{Guid.NewGuid():N}", string.Empty, 0, null, cancellationToken);

    public Task<WalletReply> BetAsync(string sessionToken, string transactionId, string roundId, long amount, CancellationToken cancellationToken) =>
        CallAsync("bet", sessionToken, transactionId, roundId, amount, null, cancellationToken);

    public Task<WalletReply> WinAsync(string sessionToken, string transactionId, string roundId, long amount, CancellationToken cancellationToken) =>
        CallAsync("win", sessionToken, transactionId, roundId, amount, null, cancellationToken);

    public Task<WalletReply> RollbackAsync(string sessionToken, string transactionId, string roundId, string betTransactionId, CancellationToken cancellationToken) =>
        CallAsync("rollback", sessionToken, transactionId, roundId, 0, betTransactionId, cancellationToken);

    private async Task<WalletReply> CallAsync(string action, string sessionToken, string transactionId, string roundId, long amount, string? reference, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            sessionToken,
            providerTransactionId = transactionId,
            roundId,
            gameId = GildedSavanna.GameId,
            amount,
            currency = o.Currency,
            referenceTransactionId = reference,
        }, Json);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{o.CasinoAddress.TrimEnd('/')}/providers/{Uri.EscapeDataString(o.ProviderId)}/wallet/{action}")
        {
            Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } },
        };
        request.Headers.Add(ProviderSignature.Header, ProviderSignature.Compute(o.Secret, body));
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            var reply = await response.Content.ReadFromJsonAsync<WalletReply>(Json, cancellationToken);
            return reply ?? new WalletReply(WalletStatus.Unreachable, null, null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new WalletReply(WalletStatus.Unreachable, null, null);
        }
    }
}
