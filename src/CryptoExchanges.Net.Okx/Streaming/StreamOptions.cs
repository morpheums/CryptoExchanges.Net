namespace CryptoExchanges.Net.Okx.Streaming;

/// <summary>
/// Configuration options for the OKX WebSocket streaming client.
/// </summary>
public sealed class StreamOptions
{
    /// <summary>
    /// The WebSocket base URL for the OKX v5 public stream endpoint.
    /// Default: <c>wss://ws.okx.com:8443/ws/v5/public</c>, which serves ticker, trade, and order-book
    /// channels. OKX serves kline (<c>candle*</c>) channels on the separate <em>business</em> endpoint
    /// (<c>wss://ws.okx.com:8443/ws/v5/business</c>); set this to that URL to receive kline streams.
    /// </summary>
    public string StreamBaseUrl { get; set; } = "wss://ws.okx.com:8443/ws/v5/public";
}
