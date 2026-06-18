using System.Net;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Okx.Resilience;

/// <summary>
/// Maps OKX V5 error responses (HTTP status + <c>{code,msg,data}</c> envelope) to the SDK's typed
/// exceptions. OKX returns a string <c>code</c> at the top level and, for order endpoints, a per-order
/// <c>sCode</c>/<c>sMsg</c> inside the <c>data</c> array; both are inspected here. OKX success is
/// <c>code == "0"</c> (a string), which must NEVER be treated as an error.
/// </summary>
/// <remarks>
/// Internal per ADR-001 conv #2: the in-assembly composer/AddOkxExchange construct this directly, so
/// there is no cross-assembly need for it to be public. OKX V5 error codes are documented as strings;
/// the mappings below use real V5 codes and map conservatively, commenting where coverage is partial.
/// </remarks>
internal sealed class OkxErrorTranslator : IExchangeErrorTranslator
{
    /// <inheritdoc />
    public ExchangeException Translate(HttpResponseMessage response, string body)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (code, msg) = Parse(body);
        var text = msg is null ? $"OKX HTTP {(int)response.StatusCode}" : $"OKX error {code}: {msg}";

        // A success envelope (code == "0") is not an error and must pass through untranslated. Callers
        // should not reach the translator with one, but guard defensively (mirrors Bybit's posture).
        if (code == "0")
            return new ExchangeApiException(text, ParseCode(code), body);

        var numeric = ParseCode(code);

        // Rate limiting: HTTP 429, and OKX's "too many requests" code 50011. RetryAfter from headers.
        if (response.StatusCode == HttpStatusCode.TooManyRequests || code is "50011" or "50013")
            return new RateLimitExceededException(text, RetryAfterReader.GetDelay(response), numeric, body);

        // Authentication / signature / permission / timestamp failures: HTTP 401/403, and OKX's
        // 5031x auth family. 50100 = API frozen; 50101 = broker mismatch; 50102 = timestamp expired;
        // 50103 = missing auth header; 50104 = missing passphrase; 50105 = wrong passphrase;
        // 50111 = invalid API key; 50112 = invalid timestamp; 50113 = invalid signature; 50114 = invalid auth.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || code is "50100" or "50101" or "50102" or "50103" or "50104" or "50105"
                or "50111" or "50112" or "50113" or "50114")
            return new AuthenticationException(text, numeric, body);

        // Insufficient balance: 51008 = insufficient balance; 51119 = order placement failed (no funds);
        // 51131 = insufficient balance.
        if (code is "51008" or "51119" or "51131")
            return new InsufficientBalanceException(text, numeric, body);

        // Order errors: 51000 = parameter error; 51001 = instrument does not exist;
        // 51002 = instrument id does not match; 51005 = order amount exceeds limit; 51006 = price out
        // of range; 51020 = order amount below minimum; 51400 = cancellation failed (order not exist);
        // 51402/51503 = order does not exist. The 51xxx family is the OKX V5 spot order-validation
        // range; map the common members and let the rest fall through to ExchangeApiException rather
        // than over-claiming coverage.
        if (code is "51000" or "51001" or "51002" or "51005" or "51006" or "51020"
            or "51400" or "51402" or "51503")
            return new InvalidOrderException(text, numeric, body);

        // Unknown / unmapped error codes: surface the venue code + raw body for diagnostics.
        return new ExchangeApiException(text, numeric, body);
    }

    /// <summary>
    /// Extracts the effective OKX error code + message: the top-level <c>code</c>/<c>msg</c>, but when
    /// the top-level <c>code</c> is "0"/absent and a per-order <c>sCode</c> in the first <c>data</c>
    /// element is non-zero, that per-order outcome is used (OKX reports order rejections that way).
    /// </summary>
    private static (string? code, string? msg) Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string? topCode = ReadString(root, "code");
            string? topMsg = ReadString(root, "msg");

            // Inspect per-order sCode only when the top-level code is success/absent: an order endpoint
            // can return code "0" overall yet reject an individual order via a non-zero sCode.
            if ((topCode is null or "0")
                && root.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array
                && data.GetArrayLength() > 0)
            {
                var first = data[0];
                if (first.ValueKind == JsonValueKind.Object)
                {
                    var sCode = ReadString(first, "sCode");
                    if (sCode is not null && sCode != "0")
                        return (sCode, ReadString(first, "sMsg") ?? topMsg);
                }
            }

            return (topCode, topMsg);
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

    /// <summary>Parses the OKX string code into the numeric code carried on the typed exception, or null.</summary>
    private static int? ParseCode(string? code)
        => int.TryParse(code, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
}
