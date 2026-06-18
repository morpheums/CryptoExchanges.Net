using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Bitget;

/// <summary>Configuration options for the Bitget exchange client.</summary>
public sealed class BitgetOptions
{
    /// <summary>The Bitget REST API base URL. Default: https://api.bitget.com</summary>
    public string BaseUrl { get; set; } = "https://api.bitget.com";

    /// <summary>Bitget API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Bitget API secret key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Bitget API passphrase; required for signed/private endpoints.</summary>
    public string Passphrase { get; set; } = string.Empty;

    /// <summary>Request timeout in seconds. Default: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Builds an <see cref="ExchangeCredentials"/> carrying the API key, secret, and passphrase.</summary>
    public ExchangeCredentials ToCredentials()
        => new(ApiKey, SecretKey, Passphrase);
}
