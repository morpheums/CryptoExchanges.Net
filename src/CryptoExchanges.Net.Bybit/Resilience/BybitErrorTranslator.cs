using System.Net;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Bybit.Resilience;

/// <summary>Maps Bybit V5 error responses (status + <c>{retCode,retMsg}</c> envelope) to the SDK's typed exceptions.</summary>
public sealed class BybitErrorTranslator : IExchangeErrorTranslator
{
    /// <inheritdoc />
    public ExchangeException Translate(HttpResponseMessage response, string body)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (code, msg) = Parse(body);
        var text = msg is null ? $"Bybit HTTP {(int)response.StatusCode}" : $"Bybit error {code}: {msg}";

        // A success envelope (retCode == 0) is not an error and must pass through untranslated.
        // Callers should not reach the translator with one, but guard defensively.
        if (code == 0)
            return new ExchangeApiException(text, code, body);

        // Rate limiting: HTTP 429, and Bybit's rate-limit retCodes. RetryAfter comes from headers.
        // 10006 = too many visits (IP rate limit); 10018 = exceeded the IP rate limit.
        if (response.StatusCode == HttpStatusCode.TooManyRequests || code is 10006 or 10018)
            return new RateLimitExceededException(text, RetryAfterReader.GetDelay(response), code, body);

        // Authentication / signature / permission failures: HTTP 401/403, and Bybit auth retCodes.
        // 10003 = invalid API key; 10004 = invalid signature; 10005 = permission denied;
        // 10010 = unmatched IP (IP allowlist); 33004 = API key expired.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || code is 10003 or 10004 or 10005 or 10010 or 33004)
            return new AuthenticationException(text, code, body);

        // Insufficient balance: 110007 = insufficient available balance;
        // 170131 = insufficient balance (spot).
        if (code is 110007 or 170131)
            return new InsufficientBalanceException(text, code, body);

        // Order errors: 110001 = order does not exist; 110003 = price out of permissible range;
        // 110004 = wallet balance / order qty issue; 170140 = order value below minimum;
        // 170135/170136 = price/qty precision or filter errors. The 170xxx family is the Bybit
        // V5 spot order-validation range; map the common members here and let the rest fall through
        // to ExchangeApiException rather than over-claiming coverage.
        if (code is 110001 or 110003 or 110004 or 170135 or 170136 or 170140)
            return new InvalidOrderException(text, code, body);

        // Unknown / unmapped error codes: surface the venue retCode + raw body for diagnostics.
        return new ExchangeApiException(text, code, body);
    }

    private static (int? code, string? msg) Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            int? code = root.TryGetProperty("retCode", out var c) && c.ValueKind == JsonValueKind.Number
                ? c.GetInt32() : null;
            string? msg = root.TryGetProperty("retMsg", out var m) ? m.GetString() : null;
            return (code, msg);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
