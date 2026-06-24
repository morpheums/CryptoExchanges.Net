namespace CryptoExchanges.Net.Bybit.Streaming;

/// <summary>
/// Configuration options for the Bybit WebSocket streaming client.
/// </summary>
public sealed class StreamOptions
{
    /// <summary>
    /// The WebSocket base URL for the Bybit v5 public spot stream endpoint.
    /// Default: <c>wss://stream.bybit.com/v5/public/spot</c>.
    /// </summary>
    public string StreamBaseUrl { get; set; } = "wss://stream.bybit.com/v5/public/spot";
}
