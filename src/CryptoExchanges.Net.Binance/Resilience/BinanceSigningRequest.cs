namespace CryptoExchanges.Net.Binance.Resilience;

/// <summary>Marks an outgoing request as requiring Binance signing (timestamp + HMAC), applied
/// per attempt by <see cref="BinanceSigningHandler"/>.</summary>
internal static class BinanceSigningRequest
{
    private static readonly HttpRequestOptionsKey<bool> SignedKey = new("binance.signed");

    /// <summary>Flags the request as signed.</summary>
    public static void MarkSigned(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Options.Set(SignedKey, true);
    }

    /// <summary>True when the request was flagged as signed.</summary>
    public static bool IsSigned(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Options.TryGetValue(SignedKey, out var v) && v;
    }
}
