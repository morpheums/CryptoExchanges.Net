using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Kucoin;

/// <summary>
/// Configuration options for the KuCoin exchange client.
/// </summary>
public sealed class KucoinOptions
{
    /// <summary>The KuCoin REST API base URL. Default: https://api.kucoin.com</summary>
    public string BaseUrl { get; set; } = "https://api.kucoin.com";

    /// <summary>KuCoin API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>KuCoin API secret key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>KuCoin API passphrase (required for signed endpoints; HMAC-signed before transmission in version-2 mode).</summary>
    public string Passphrase { get; set; } = string.Empty;

    /// <summary>Request timeout in seconds. Default: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Builds <see cref="ExchangeCredentials"/> from <see cref="ApiKey"/>, <see cref="SecretKey"/>, and <see cref="Passphrase"/>.</summary>
    /// <returns>A credential set carrying the API key, secret, and passphrase.</returns>
    public ExchangeCredentials ToCredentials()
        => new(ApiKey, SecretKey, Passphrase);
}
