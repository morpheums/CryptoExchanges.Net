using System.Net;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Kraken.Internal;

/// <summary>
/// Maps Kraken REST error responses (<c>{ error:[...], result:{...} }</c>) to the SDK's typed
/// exceptions. Kraken signals errors as a non-empty <c>error</c> array in an otherwise 200
/// response; the HTTP status is used only for network-level failures.
/// </summary>
internal sealed class KrakenErrorTranslator : IExchangeErrorTranslator
{
    /// <inheritdoc />
    public ExchangeException Translate(HttpResponseMessage response, string body)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (code, msg) = Parse(body);
        var text = msg is null ? $"Kraken HTTP {(int)response.StatusCode}" : $"Kraken error {code}: {msg}";

        if (response.StatusCode == HttpStatusCode.TooManyRequests
            || code is "EAPI:Rate limit exceeded" or "EOrder:Rate limit exceeded")
            return new RateLimitExceededException(text, RetryAfterReader.GetDelay(response), null, body);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || code is "EAPI:Invalid key" or "EAPI:Invalid nonce" or "EAPI:Invalid signature" or "EGeneral:Permission denied")
            return new AuthenticationException(text, null, body);

        if (code is "EGeneral:Invalid arguments" or "EOrder:Invalid order" or "EOrder:Invalid price" or "EOrder:Minimum order size")
            return new InvalidOrderException(text, null, body);

        if (code is "EOrder:Insufficient funds" or "EOrder:Insufficient initial margin")
            return new InsufficientBalanceException(text, null, body);

        return new ExchangeApiException(text, null, body);
    }

    private static (string? code, string? msg) Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0)
            {
                var first = errors[0].GetString();
                return (first, first);
            }
            return (null, null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
