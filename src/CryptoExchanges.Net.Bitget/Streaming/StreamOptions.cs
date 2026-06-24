namespace CryptoExchanges.Net.Bitget.Streaming;

/// <summary>
/// Configuration options for the Bitget WebSocket streaming client.
/// </summary>
public sealed class StreamOptions
{
    /// <summary>
    /// The WebSocket base URL for the Bitget v2 public spot stream endpoint.
    /// Default: <c>wss://ws.bitget.com/v2/ws/public</c>.
    /// </summary>
    public string StreamBaseUrl { get; set; } = "wss://ws.bitget.com/v2/ws/public";
}
