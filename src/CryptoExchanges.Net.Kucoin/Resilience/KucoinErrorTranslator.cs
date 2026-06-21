using System.Net;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Kucoin.Resilience;

/// <summary>
/// Maps KuCoin V1 error responses (HTTP status + <c>{code,msg}</c> envelope) to the SDK's typed
/// exceptions. KuCoin returns a string <c>code</c> at the top level; success is
/// <c>code == "200000"</c> (a string), which must NEVER be treated as an error.
/// </summary>
/// <remarks>
/// Internal per ADR-001 conv #2: the in-assembly composer/AddKucoinExchange construct this
/// directly, so there is no cross-assembly need for it to be public. KuCoin error codes are
/// documented as strings; the mappings below use real KuCoin codes and map conservatively.
/// </remarks>
internal sealed class KucoinErrorTranslator : IExchangeErrorTranslator
{
    /// <inheritdoc />
    public ExchangeException Translate(HttpResponseMessage response, string body)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (code, msg) = Parse(body);
        var text = msg is null ? $"KuCoin HTTP {(int)response.StatusCode}" : $"KuCoin error {code}: {msg}";

        // Translate always returns an exception (it is only invoked on a failed/error response). When the
        // JSON envelope still reports code "200000" (e.g. an HTTP-level error with a success-shaped body),
        // we return the generic ExchangeApiException rather than mapping to a more specific typed exception.
        if (code == "200000")
            return new ExchangeApiException(text, ParseCode(code), body);

        var numeric = ParseCode(code);

        // Rate limiting: HTTP 429. KuCoin returns 429 directly for rate limit breaches.
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return new RateLimitExceededException(text, RetryAfterReader.GetDelay(response), numeric, body);

        // Authentication / signature / permission / timestamp failures: HTTP 401/403, and KuCoin
        // auth family: 400001 = timestamp expired; 400002 = invalid timestamp; 400003 = invalid
        // API key; 400004 = invalid passphrase; 400005 = invalid signature; 400006 = invalid IP;
        // 400007 = endpoint access forbidden; 411100 = account is frozen.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || code is "400001" or "400002" or "400003" or "400004" or "400005"
                or "400006" or "400007" or "411100")
            return new AuthenticationException(text, numeric, body);

        // Insufficient balance: 900014 = insufficient balance.
        if (code is "900014")
            return new InsufficientBalanceException(text, numeric, body);

        // Order errors: 900001 = invalid symbol; 900002 = invalid price; 900003 = invalid amount;
        // 900004 = invalid order type; 900005 = invalid order side; 900006 = amount too small;
        // 900007 = price out of range; 900008 = order does not exist; 900009 = order already
        // cancelled; 900010 = order already closed.
        if (code is "900001" or "900002" or "900003" or "900004" or "900005"
            or "900006" or "900007" or "900008" or "900009" or "900010")
            return new InvalidOrderException(text, numeric, body);

        // Unknown / unmapped error codes: surface the venue code + raw body for diagnostics.
        return new ExchangeApiException(text, numeric, body);
    }

    /// <summary>
    /// Extracts the KuCoin error code + message from the <c>{"code","msg"}</c> envelope.
    /// </summary>
    private static (string? code, string? msg) Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var code = ReadString(root, "code");
            var msg = ReadString(root, "msg");
            return (code, msg);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>Reads a string property, tolerating non-string value kinds (returns null) so a
    /// malformed body never throws InvalidOperationException out of the catch.</summary>
    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>Parses the KuCoin string code into the numeric code carried on the typed exception, or null.</summary>
    private static int? ParseCode(string? code)
        => int.TryParse(code, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
}
