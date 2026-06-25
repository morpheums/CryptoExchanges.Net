using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Bybit;
using CryptoExchanges.Net.Okx;
using CryptoExchanges.Net.Bitget;
using CryptoExchanges.Net.Kucoin;
using CryptoExchanges.Net.Coinbase;
using CryptoExchanges.Net.Kraken;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net;

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

        services.AddOkxExchange(opt =>
        {
            opt.BaseUrl = options.OkxBaseUrl ?? opt.BaseUrl;
            opt.ApiKey = options.OkxApiKey ?? opt.ApiKey;
            opt.SecretKey = options.OkxSecretKey ?? opt.SecretKey;
            opt.Passphrase = options.OkxPassphrase ?? opt.Passphrase;
        });

        services.AddBitgetExchange(opt =>
        {
            opt.BaseUrl = options.BitgetBaseUrl ?? opt.BaseUrl;
            opt.ApiKey = options.BitgetApiKey ?? opt.ApiKey;
            opt.SecretKey = options.BitgetSecretKey ?? opt.SecretKey;
            opt.Passphrase = options.BitgetPassphrase ?? opt.Passphrase;
        });

        services.AddKucoinExchange(opt =>
        {
            opt.BaseUrl = options.KucoinBaseUrl ?? opt.BaseUrl;
            opt.ApiKey = options.KucoinApiKey ?? opt.ApiKey;
            opt.SecretKey = options.KucoinSecretKey ?? opt.SecretKey;
            opt.Passphrase = options.KucoinPassphrase ?? opt.Passphrase;
        });

        services.AddCoinbaseExchange(opt =>
        {
            opt.BaseUrl = options.CoinbaseBaseUrl ?? opt.BaseUrl;
            opt.ApiKey = options.CoinbaseApiKey ?? opt.ApiKey;
            opt.PrivateKey = options.CoinbasePrivateKey ?? opt.PrivateKey;
        });

        services.AddKrakenExchange(opt =>
        {
            opt.BaseUrl = options.KrakenBaseUrl ?? opt.BaseUrl;
            opt.ApiKey = options.KrakenApiKey ?? opt.ApiKey;
            opt.ApiSecret = options.KrakenApiSecret ?? opt.ApiSecret;
        });

        return services;
    }
}
