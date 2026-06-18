using System.Net;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Bitget.Resilience;

/// <summary>
/// Maps Bitget V2 error responses (HTTP status + <c>{code,msg,data}</c> envelope) to the SDK's typed
/// exceptions. Bitget returns a string <c>code</c> at the top level; SUCCESS is the string
/// <c>"00000"</c>, which must NEVER be treated as an error.
/// </summary>
/// <remarks>
/// Internal per ADR-001 conv #2: the in-assembly composer/AddBitgetExchange construct this directly, so
/// there is no cross-assembly need for it to be public. Bitget V2 error codes are documented as strings;
/// the mappings below use real V2 codes and map conservatively, commenting where coverage is partial.
/// </remarks>
internal sealed class BitgetErrorTranslator : IExchangeErrorTranslator
{
    /// <summary>Bitget V2 success code (a string, NOT zero).</summary>
    private const string SuccessCode = "00000";

    /// <inheritdoc />
    public ExchangeException Translate(HttpResponseMessage response, string body)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (code, msg) = Parse(body);
        var text = msg is null ? $"Bitget HTTP {(int)response.StatusCode}" : $"Bitget error {code}: {msg}";

        // Translate always returns an exception (it is only invoked on a failed/error response). When the
        // JSON envelope still reports the success code (e.g. an HTTP-level error with a success-shaped
        // body), return the generic ExchangeApiException rather than a more specific typed exception.
        if (code == SuccessCode)
            return new ExchangeApiException(text, ParseCode(code), body);

        var numeric = ParseCode(code);

        // Rate limiting: HTTP 429, and Bitget's "too many requests" codes. RetryAfter from headers.
        if (response.StatusCode == HttpStatusCode.TooManyRequests || code is "429" or "30007" or "40404")
            return new RateLimitExceededException(text, RetryAfterReader.GetDelay(response), numeric, body);

        // Authentication / signature / permission / timestamp failures: HTTP 401/403, and Bitget's
        // 400xx auth family. 40006 = invalid sign; 40009 = sign error; 40011 = missing access key;
        // 40012 = apikey/passphrase error; 40014 = incorrect permission; 40018 = IP not whitelisted;
        // 40037 = apikey does not exist; 40002/40008 = request timestamp expired.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || code is "40006" or "40009" or "40011" or "40012" or "40014" or "40018"
                or "40037" or "40002" or "40008")
            return new AuthenticationException(text, numeric, body);

        // Insufficient balance: 43012 = insufficient balance; 43011 = insufficient funds for order.
        if (code is "43012" or "43011")
            return new InsufficientBalanceException(text, numeric, body);

        // Order errors: 40808 = parameter verification exception; 43001 = order does not exist;
        // 43002 = pending order failed; 43025 = plan order quantity error; 45110 = less than minimum
        // amount; 400172 = invalid/unknown symbol. The 43xxx/45xxx families are the Bitget V2 spot
        // order-validation range; map the common members and let the rest fall through to
        // ExchangeApiException rather than over-claiming coverage.
        if (code is "40808" or "43001" or "43002" or "43025" or "45110" or "400172")
            return new InvalidOrderException(text, numeric, body);

        // Unknown / unmapped error codes: surface the venue code + raw body for diagnostics.
        return new ExchangeApiException(text, numeric, body);
    }

    /// <summary>Extracts the top-level Bitget <c>code</c>/<c>msg</c> from the envelope.</summary>
    private static (string? code, string? msg) Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return (ReadString(root, "code"), ReadString(root, "msg"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>Reads a string property, tolerating non-string value kinds (returns null) so a
    /// malformed body never throws InvalidOperationException out of the catch (ADR-001 conv 3 — guard
    /// JsonElement.ValueKind before GetString()).</summary>
    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>Parses the Bitget string code into the numeric code carried on the typed exception, or null.
    /// Bitget's success code "00000" parses to 0; non-numeric codes yield null.</summary>
    private static int? ParseCode(string? code)
        => int.TryParse(code, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
}
