using CryptoExchanges.Net.Kraken.Auth;
using CryptoExchanges.Net.Kraken.Internal;
using CryptoExchanges.Net.Kraken.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CryptoExchanges.Net.Kraken;

/// <summary>
/// Dependency-injection extensions for registering the Kraken exchange client. Lives in the Kraken
/// assembly so a consumer can depend on Kraken alone without transitively pulling in other exchange
/// assemblies (see ADR-001).
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string KrakenClientName = "kraken";

    /// <summary>
    /// Registers the Kraken exchange client and all its dependencies as per-exchange keyed singletons.
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="KrakenOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddKrakenExchange(
        this IServiceCollection services,
        Action<KrakenOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>();

        services.AddOptions<KrakenOptions>()
            .Configure(ApplyEnvDefaults)
            .Configure(o => configure?.Invoke(o))
            .Validate(o => o.TimeoutSeconds > 0, "KrakenOptions.TimeoutSeconds must be > 0.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "KrakenOptions.BaseUrl is required.")
            .ValidateOnStart();

        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<KrakenOptions>>().Value);

#pragma warning disable CA1861
        services.TryAddKeyedSingleton(ExchangeId.Kraken, (_, _) => new long[] { 0L });
#pragma warning restore CA1861
        services.TryAddKeyedSingleton<ISymbolMapper>(ExchangeId.Kraken,
            (_, _) => (ISymbolMapper)new SymbolMapper(KrakenSymbolFormat.Instance));
        services.TryAddKeyedSingleton<IMapper>(ExchangeId.Kraken, (sp, _) =>
            KrakenClientComposer.CreateMapper(
                sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Kraken)));

        services.AddHttpClient(KrakenClientName, (sp, c) =>
            {
                var o = sp.GetRequiredService<KrakenOptions>();
                c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/'));
                c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
                c.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) })
            .ApplyResiliencePipeline(
                KrakenClientName,
                new ResilienceOptions(),
                translatorFactory: _ => new KrakenErrorTranslator(),
                gateFactory: _ => new ReactiveRateLimitGate(),
                requestFinalizerFactory: sp =>
                {
                    var o = sp.GetRequiredService<KrakenOptions>();
                    if (string.IsNullOrEmpty(o.ApiSecret))
                        return new PassThroughHandler();
                    return new KrakenSigningHandler(o.ApiKey, new KrakenSignatureService(o.ApiSecret));
                });

        return services;
    }

    private static void ApplyEnvDefaults(KrakenOptions o)
    {
        var k = Environment.GetEnvironmentVariable("KRAKEN_API_KEY");
        var s = Environment.GetEnvironmentVariable("KRAKEN_API_SECRET");
        if (!string.IsNullOrEmpty(k)) o.ApiKey = k;
        if (!string.IsNullOrEmpty(s)) o.ApiSecret = s;
    }
}
