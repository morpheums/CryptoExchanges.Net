namespace CryptoExchanges.Net.Kucoin.Streaming;

/// <summary>
/// Configuration options for the KuCoin WebSocket streaming client.
/// </summary>
public sealed class KucoinStreamOptions
{
    /// <summary>
    /// The REST base URL used to reach the bullet-public negotiation endpoint.
    /// Default: <c>https://api.kucoin.com</c>.
    /// </summary>
    public string RestBaseUrl { get; set; } = "https://api.kucoin.com";
}
