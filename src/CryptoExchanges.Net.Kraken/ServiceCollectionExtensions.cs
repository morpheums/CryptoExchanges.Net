using CryptoExchanges.Net.Kraken.Auth;
using CryptoExchanges.Net.Kraken.Internal;
using CryptoExchanges.Net.Kraken.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

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
    /// Registers the Kraken exchange client and all its dependencies as per-exchange keyed singletons,
    /// backed by a typed <see cref="System.Net.Http.HttpClient"/> with the full resilience handler chain.
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="KrakenOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddKrakenExchange(
        this IServiceCollection services,
        Action<KrakenOptions>? configure = null) =>
        ExchangeServiceRegistration.AddExchange<KrakenOptions, IMapper>(
            services,
            ExchangeId.Kraken,
            KrakenClientName,
            optionsName: "KrakenOptions",
            applyEnvDefaults: ApplyEnvDefaults,
            configure: configure,
            timeoutSecondsSelector: o => o.TimeoutSeconds,
            baseUrlSelector: o => o.BaseUrl,
            symbolMapperFactory: () => new SymbolMapper(KrakenSymbolFormat.Instance),
            mapperFactory: KrakenClientComposer.CreateMapper,
            configureHttpClient: null,
            resilienceOptions: new ResilienceOptions(),
            translatorFactory: _ => new KrakenErrorTranslator(),
            requestFinalizerFactory: sp =>
            {
                var o = sp.GetRequiredService<KrakenOptions>();
                if (string.IsNullOrEmpty(o.ApiKey) || string.IsNullOrEmpty(o.ApiSecret))
                    return new PassThroughHandler();
                return new KrakenSigningHandler(o.ApiKey, new KrakenSignatureService(o.ApiSecret));
            },
            exchangeClientFactory: (sp, httpClient, holder) =>
                KrakenClientComposer.ComposeForDi(sp, httpClient, holder));

    private static void ApplyEnvDefaults(KrakenOptions o)
    {
        var k = Environment.GetEnvironmentVariable("KRAKEN_API_KEY");
        var s = Environment.GetEnvironmentVariable("KRAKEN_API_SECRET");
        if (!string.IsNullOrEmpty(k)) o.ApiKey = k;
        if (!string.IsNullOrEmpty(s)) o.ApiSecret = s;
    }
}
