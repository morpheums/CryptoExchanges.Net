namespace CryptoExchanges.Net.Kucoin.Resilience;

/// <summary>Marks an outgoing request as requiring KuCoin KC-API signing (Unix-ms timestamp +
/// HMAC-SHA256 + signed passphrase), applied per attempt by the KuCoin signing handler.</summary>
internal static class KucoinSigningRequest
{
    private static readonly HttpRequestOptionsKey<bool> SignedKey = new("kucoin.signed");

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
