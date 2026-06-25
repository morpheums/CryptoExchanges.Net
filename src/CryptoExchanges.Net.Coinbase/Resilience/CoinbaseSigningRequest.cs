namespace CryptoExchanges.Net.Coinbase.Resilience;

/// <summary>Marks an outgoing request as requiring Coinbase JWT signing, applied per attempt by the signing handler.</summary>
internal static class CoinbaseSigningRequest
{
    private static readonly HttpRequestOptionsKey<bool> SignedKey = new("coinbase.signed");

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
