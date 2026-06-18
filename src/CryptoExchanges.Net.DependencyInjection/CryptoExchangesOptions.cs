namespace CryptoExchanges.Net.DependencyInjection;

/// <summary>
/// Top-level configuration for all exchange clients registered via
/// <see cref="ServiceCollectionExtensions.AddCryptoExchanges"/>.
/// </summary>
public sealed class CryptoExchangesOptions
{
    /// <summary>Binance API base URL override.</summary>
    public string? BinanceBaseUrl { get; set; }

    /// <summary>Binance API key.</summary>
    public string? BinanceApiKey { get; set; }

    /// <summary>Binance API secret key.</summary>
    public string? BinanceSecretKey { get; set; }

    /// <summary>Bybit API base URL override.</summary>
    public string? BybitBaseUrl { get; set; }

    /// <summary>Bybit API key.</summary>
    public string? BybitApiKey { get; set; }

    /// <summary>Bybit API secret key.</summary>
    public string? BybitSecretKey { get; set; }

    /// <summary>OKX API base URL override.</summary>
    public string? OkxBaseUrl { get; set; }

    /// <summary>OKX API key.</summary>
    public string? OkxApiKey { get; set; }

    /// <summary>OKX API secret key.</summary>
    public string? OkxSecretKey { get; set; }

    /// <summary>OKX API passphrase (the third OKX credential, required for signed endpoints).</summary>
    public string? OkxPassphrase { get; set; }

    /// <summary>Bitget API base URL override.</summary>
    public string? BitgetBaseUrl { get; set; }

    /// <summary>Bitget API key.</summary>
    public string? BitgetApiKey { get; set; }

    /// <summary>Bitget API secret key.</summary>
    public string? BitgetSecretKey { get; set; }

    /// <summary>Bitget API passphrase (the third Bitget credential, required for signed endpoints).</summary>
    public string? BitgetPassphrase { get; set; }
}
