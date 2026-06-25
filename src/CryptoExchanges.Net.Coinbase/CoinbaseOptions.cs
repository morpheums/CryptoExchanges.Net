namespace CryptoExchanges.Net.Coinbase;

/// <summary>
/// Configuration options for the Coinbase exchange client.
/// </summary>
public sealed class CoinbaseOptions
{
    /// <summary>The Coinbase REST API base URL. Default: https://api.coinbase.com</summary>
    public string BaseUrl { get; set; } = "https://api.coinbase.com";

    /// <summary>Coinbase API key name (key ID).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Coinbase EC private key in PEM format.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>Request timeout in seconds. Default: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
