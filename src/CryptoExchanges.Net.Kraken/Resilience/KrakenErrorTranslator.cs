using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Kraken.Resilience;

/// <summary>
/// Maps Kraken REST error responses to the SDK's typed exceptions. Kraken always returns HTTP 200;
/// errors are signalled via the <c>error[]</c> array in the body. Because
/// <see cref="ErrorTranslationHandler"/> only fires on non-2xx responses, call this translator
/// explicitly after each 200 response — parse the body, and throw when <c>error[]</c> is non-empty.
/// </summary>
internal sealed class KrakenErrorTranslator : IExchangeErrorTranslator
{
    /// <inheritdoc />
    public ExchangeException Translate(HttpResponseMessage response, string body)
    {
        ArgumentNullException.ThrowIfNull(response);

        var firstError = ParseFirstError(body);

        if (firstError is null)
        {
            var fallbackText = $"Kraken HTTP {(int)response.StatusCode}";
            return new ExchangeApiException(fallbackText, null, body);
        }

        var text = $"Kraken error: {firstError}";

        // Prefix table per ADR-010-005: map Kraken's EXxx: prefixes to typed exceptions.
        if (firstError.StartsWith("EAuth:", StringComparison.Ordinal))
            return new AuthenticationException(text, null, body);

        if (firstError.StartsWith("EOrder:", StringComparison.Ordinal))
            return new InvalidOrderException(text, null, body);

        if (firstError.StartsWith("EAPI:Rate limit exceeded", StringComparison.Ordinal))
            return new RateLimitExceededException(text, retryAfter: null, code: null, rawBody: body);

        if (firstError.StartsWith("EGeneral:Insufficient", StringComparison.Ordinal))
            return new InsufficientBalanceException(text, null, body);

        if (firstError.StartsWith("EGeneral:", StringComparison.Ordinal)
            || firstError.StartsWith("EService:", StringComparison.Ordinal))
            return new ExchangeApiException(text, null, body);

        return new ExchangeApiException(text, null, body);
    }

    /// <summary>Returns the first non-empty string from <c>error[]</c>, or null when absent/empty/invalid JSON.</summary>
    private static string? ParseFirstError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var errArray)
                || errArray.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var element in errArray.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var s = element.GetString();
                    if (!string.IsNullOrEmpty(s))
                        return s;
                }
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
