namespace CryptoExchanges.Net.Kraken.Streaming;

/// <summary>
/// Configuration options for the Kraken WebSocket streaming client.
/// </summary>
public sealed class StreamOptions
{
    /// <summary>
    /// The WebSocket base URL for the Kraken v2 public stream endpoint.
    /// Default: <c>wss://ws.kraken.com/v2</c>.
    /// </summary>
    public string StreamBaseUrl { get; set; } = "wss://ws.kraken.com/v2";
}
