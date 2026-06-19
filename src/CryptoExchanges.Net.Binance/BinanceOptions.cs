namespace CryptoExchanges.Net.Binance;

/// <summary>
/// Configuration options for the Binance exchange client.
/// </summary>
public sealed class BinanceOptions
{
    /// <summary>The Binance REST API base URL. Default: https://api.binance.com</summary>
    public string BaseUrl { get; set; } = "https://api.binance.com";

    /// <summary>Binance API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Binance API secret key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Request timeout in seconds. Default: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Receive window in milliseconds (decimal). Default: 5000.</summary>
    public decimal ReceiveWindow { get; set; } = 5000m;
}
