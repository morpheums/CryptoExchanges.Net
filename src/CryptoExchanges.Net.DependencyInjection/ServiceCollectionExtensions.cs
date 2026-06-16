using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.DependencyInjection;

/// <summary>
/// Extension methods for registering CryptoExchanges.Net services
/// with the Microsoft dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Binance exchange client and all its dependencies.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="BinanceOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddBinanceExchange(
        this IServiceCollection services,
        Action<BinanceOptions>? configure = null)
    {
        var options = new BinanceOptions();

        // Apply environment variables as defaults
        var envApiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY");
        var envSecretKey = Environment.GetEnvironmentVariable("BINANCE_SECRET_KEY");

        if (!string.IsNullOrEmpty(envApiKey))
            options.ApiKey = envApiKey;
        if (!string.IsNullOrEmpty(envSecretKey))
            options.SecretKey = envSecretKey;

        configure?.Invoke(options);

        // Register the options instance
        services.AddSingleton(options);

        // Register the exchange client as a keyed singleton for service resolution by exchange ID
        services.AddKeyedSingleton<IExchangeClient>(ExchangeId.Binance, (sp, _) =>
        {
            var opts = sp.GetRequiredService<BinanceOptions>();
            return BinanceExchangeClient.Create(opts);
        });

        // Also register as concrete type for direct usage
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<BinanceOptions>();
            return BinanceExchangeClient.Create(opts);
        });

        return services;
    }

    /// <summary>
    /// Registers all available exchange clients. This is a convenience
    /// method that calls each individual Add*Exchange method.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CryptoExchangesOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCryptoExchanges(
        this IServiceCollection services,
        Action<CryptoExchangesOptions>? configure = null)
    {
        var options = new CryptoExchangesOptions();
        configure?.Invoke(options);

        services.AddBinanceExchange(opt =>
        {
            opt.BaseUrl = options.BinanceBaseUrl ?? opt.BaseUrl;
            opt.ApiKey = options.BinanceApiKey ?? opt.ApiKey;
            opt.SecretKey = options.BinanceSecretKey ?? opt.SecretKey;
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
}
