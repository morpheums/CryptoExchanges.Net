namespace CryptoExchanges.Net.Okx.Streaming;

/// <summary>
/// Configuration options for the OKX WebSocket streaming client.
/// </summary>
public sealed class StreamOptions
{
    /// <summary>
    /// The WebSocket base URL for the OKX v5 public stream endpoint.
    /// Default: <c>wss://ws.okx.com:8443/ws/v5/public</c>.
    /// </summary>
    public string StreamBaseUrl { get; set; } = "wss://ws.okx.com:8443/ws/v5/public";
}
