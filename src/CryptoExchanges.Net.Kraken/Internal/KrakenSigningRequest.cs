namespace CryptoExchanges.Net.Kraken.Internal;

/// <summary>Marks an outgoing request as requiring Kraken signing (nonce + HMAC-SHA512), applied
/// per attempt by the Kraken signing handler.</summary>
internal static class KrakenSigningRequest
{
    private static readonly HttpRequestOptionsKey<bool> SignedKey = new("kraken.signed");

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
