using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Bybit;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.DependencyInjection;

/// <summary>
/// Convenience extension that registers every available exchange at once. Per-exchange
/// registration lives in each exchange's own assembly (<c>AddBinanceExchange</c> in
/// CryptoExchanges.Net.Binance, <c>AddBybitExchange</c> in CryptoExchanges.Net.Bybit); this
/// aggregator simply delegates to them (see ADR-001).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all available exchange clients. This is a convenience
    /// method that delegates to each exchange's own Add*Exchange method.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CryptoExchangesOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCryptoExchanges(
        this IServiceCollection services,
        Action<CryptoExchangesOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new CryptoExchangesOptions();
        configure?.Invoke(options);

        services.AddBinanceExchange(opt =>
        {
            opt.BaseUrl = options.BinanceBaseUrl ?? opt.BaseUrl;
            opt.ApiKey = options.BinanceApiKey ?? opt.ApiKey;
            opt.SecretKey = options.BinanceSecretKey ?? opt.SecretKey;
        });

        services.AddBybitExchange(opt =>
        {
            opt.BaseUrl = options.BybitBaseUrl ?? opt.BaseUrl;
            opt.ApiKey = options.BybitApiKey ?? opt.ApiKey;
            opt.SecretKey = options.BybitSecretKey ?? opt.SecretKey;
        });

        return services;
    }
}

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
}
