using System.Net;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Coinbase.Resilience;

/// <summary>
/// Maps Coinbase Advanced Trade API error responses (HTTP status + <c>{"error":"TYPE","error_details":"...","message":"..."}</c>
/// envelope) to the SDK's typed exceptions.
/// </summary>
internal sealed class CoinbaseErrorTranslator : IExchangeErrorTranslator
{
    /// <inheritdoc />
    public ExchangeException Translate(HttpResponseMessage response, string body)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (error, message) = Parse(body);
        var text = message is null
            ? $"Coinbase HTTP {(int)response.StatusCode}"
            : $"Coinbase error {error}: {message}";

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return new RateLimitExceededException(text, RetryAfterReader.GetDelay(response), null, body);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || error is "UNAUTHORIZED" or "INVALID_API_KEY" or "INVALID_SIGNATURE" or "EXPIRED_TOKEN"
                or "AUTHENTICATION_REQUIRED")
            return new AuthenticationException(text, null, body);

        if (error is "INSUFFICIENT_FUNDS")
            return new InsufficientBalanceException(text, null, body);

        if (error is "INVALID_ARGUMENT" or "ORDER_CONFIGURATION_INVALID" or "BELOW_MIN_ORDER_SIZE"
            or "ABOVE_MAX_ORDER_SIZE" or "UNKNOWN_CANCEL_ORDER" or "DUPLICATE_ORDER")
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
            var details = ReadString(root, "error_details");
            var msg = ReadString(root, "message");
            return (error, details ?? msg);
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
