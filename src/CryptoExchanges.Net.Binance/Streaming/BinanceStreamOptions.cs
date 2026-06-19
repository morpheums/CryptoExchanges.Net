namespace CryptoExchanges.Net.Binance.Streaming;

/// <summary>
/// Configuration options for the Binance WebSocket streaming client.
/// </summary>
public sealed class BinanceStreamOptions
{
    /// <summary>
    /// The WebSocket base URL for the combined-stream endpoint.
    /// Default: <c>wss://stream.binance.com:9443</c>.
    /// </summary>
    public string StreamBaseUrl { get; set; } = "wss://stream.binance.com:9443";
}
