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

    /// <summary>
    /// KuCoin API passphrase. This is the third KuCoin credential (alongside the API key and secret)
    /// and is <b>required for signed/private endpoints</b>; it is the passphrase chosen when the API
    /// key was created on KuCoin. With KC-API-KEY-VERSION 2 the passphrase is itself HMAC-SHA256-signed
    /// before transmission. Leave empty when only public market-data endpoints are used.
    /// </summary>
    public string Passphrase { get; set; } = string.Empty;

    /// <summary>Request timeout in seconds. Default: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Builds an <see cref="ExchangeCredentials"/> from these options, including the KuCoin
    /// <see cref="Passphrase"/>. Intended for signing wire-up in later tasks.
    /// </summary>
    /// <returns>A credential set carrying the API key, secret, and passphrase.</returns>
    /// <exception cref="ArgumentException">
    /// <see cref="ApiKey"/> or <see cref="SecretKey"/> is empty/whitespace, or <see cref="Passphrase"/>
    /// is empty/whitespace (KuCoin always requires a passphrase for signed requests).
    /// </exception>
    public ExchangeCredentials ToCredentials()
        => new(ApiKey, SecretKey, Passphrase);
}
