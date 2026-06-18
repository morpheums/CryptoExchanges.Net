namespace CryptoExchanges.Net.Bitget.Resilience;

/// <summary>Marks an outgoing request as requiring Bitget signing (timestamp + HMAC), applied per
/// attempt by the Bitget signing handler.</summary>
internal static class BitgetSigningRequest
{
    private static readonly HttpRequestOptionsKey<bool> SignedKey = new("bitget.signed");

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
