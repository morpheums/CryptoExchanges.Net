using System.Net;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Coinbase.Internal;

/// <summary>
/// Maps Coinbase Advanced Trade error responses (HTTP status + <c>{preview_failure_reason, error, message}</c>
/// envelope) to the SDK's typed exceptions.
/// </summary>
internal sealed class CoinbaseErrorTranslator : IExchangeErrorTranslator
{
    /// <inheritdoc />
    public ExchangeException Translate(HttpResponseMessage response, string body)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (error, message) = Parse(body);
        var text = message is null ? $"Coinbase HTTP {(int)response.StatusCode}" : $"Coinbase error {error}: {message}";

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return new RateLimitExceededException(text, RetryAfterReader.GetDelay(response), null, body);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || error is "UNAUTHORIZED" or "INVALID_ARGUMENT" or "UNAUTHENTICATED")
            return new AuthenticationException(text, null, body);

        if (error is "INSUFFICIENT_FUND")
            return new InsufficientBalanceException(text, null, body);

        if (error is "INVALID_PRODUCT_ID" or "INVALID_LIMIT_PRICE" or "INVALID_ORDER" or "INVALID_SIDE"
            or "INVALID_ORDER_CONFIG" or "PREVIEW_INVALID_LIMIT_PRICE")
            return new InvalidOrderException(text, null, body);

        return new ExchangeApiException(text, null, body);
    }

    private static (string? error, string? message) Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var error = ReadString(root, "error");
            var message = ReadString(root, "message") ?? ReadString(root, "preview_failure_reason");
            return (error, message);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
