namespace CryptoExchanges.Net.Coinbase.Streaming;

/// <summary>
/// Configuration options for the Coinbase Advanced Trade WebSocket streaming client.
/// </summary>
public sealed class StreamOptions
{
    /// <summary>
    /// The WebSocket base URL for the Coinbase Advanced Trade public stream endpoint.
    /// Default: <c>wss://advanced-trade-ws.coinbase.com</c>.
    /// </summary>
    public string StreamBaseUrl { get; set; } = "wss://advanced-trade-ws.coinbase.com";
}
