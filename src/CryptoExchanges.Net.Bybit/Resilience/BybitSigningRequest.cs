namespace CryptoExchanges.Net.Bybit.Resilience;

/// <summary>Marks an outgoing request as requiring Bybit signing (timestamp + HMAC), applied
/// per attempt by the Bybit signing handler.</summary>
public static class BybitSigningRequest
{
    private static readonly HttpRequestOptionsKey<bool> SignedKey = new("bybit.signed");

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
