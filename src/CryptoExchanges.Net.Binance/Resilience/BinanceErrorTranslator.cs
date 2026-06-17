using System.Net;
using System.Text.Json;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Binance.Resilience;

/// <summary>Maps Binance error responses (status + {code,msg}) to the SDK's typed exceptions.</summary>
public sealed class BinanceErrorTranslator : IExchangeErrorTranslator
{
    /// <inheritdoc />
    public ExchangeException Translate(HttpResponseMessage response, string body)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (code, msg) = Parse(body);
        var text = msg is null ? $"Binance HTTP {(int)response.StatusCode}" : $"Binance error {code}: {msg}";

        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 418 || code == -1003)
            return new RateLimitExceededException(text, response.Headers.RetryAfter?.Delta, code, body);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || code is -1022 or -2014 or -2015 or -1021)
            return new AuthenticationException(text, code, body);

        if (code == -2010 && msg is not null && msg.Contains("insufficient balance", StringComparison.OrdinalIgnoreCase))
            return new InsufficientBalanceException(text, code, body);

        if (code is -2010 or -2011 or -1013 or -1100 or -1111 or -1121)
            return new InvalidOrderException(text, code, body);

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
            int? code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number
                ? c.GetInt32() : null;
            string? msg = root.TryGetProperty("msg", out var m) ? m.GetString() : null;
            return (code, msg);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
