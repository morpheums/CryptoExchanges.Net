namespace CryptoExchanges.Net.Kraken;

/// <summary>
/// Configuration options for the Kraken exchange client.
/// </summary>
public sealed class KrakenOptions
{
    /// <summary>The Kraken REST API base URL. Default: https://api.kraken.com</summary>
    public string BaseUrl { get; set; } = "https://api.kraken.com";

    /// <summary>Kraken API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Kraken API secret (base64-encoded).</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Request timeout in seconds. Default: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
