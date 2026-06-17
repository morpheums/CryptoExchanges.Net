namespace CryptoExchanges.Net.Bybit;

/// <summary>
/// Configuration options for the Bybit exchange client.
/// </summary>
public sealed class BybitOptions
{
    /// <summary>The Bybit REST API base URL. Default: https://api.bybit.com</summary>
    public string BaseUrl { get; set; } = "https://api.bybit.com";

    /// <summary>Bybit API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Bybit API secret key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Request timeout in seconds. Default: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Receive window in milliseconds (decimal). Default: 5000.</summary>
    public decimal ReceiveWindow { get; set; } = 5000m;
}
