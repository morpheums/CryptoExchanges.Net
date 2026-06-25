using CryptoExchanges.Net.Coinbase.Auth;
using CryptoExchanges.Net.Coinbase.Internal;
using CryptoExchanges.Net.Coinbase.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Coinbase;

/// <summary>
/// Dependency-injection extensions for registering the Coinbase exchange client. Lives in the Coinbase
/// assembly so a consumer can depend on Coinbase alone without transitively pulling in other exchange
/// assemblies (see ADR-001).
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string CoinbaseClientName = "coinbase";

    /// <summary>
    /// Registers the Coinbase exchange client and all its dependencies as per-exchange keyed singletons,
    /// backed by a typed <see cref="System.Net.Http.HttpClient"/> with the full resilience handler chain.
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CoinbaseOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCoinbaseExchange(
        this IServiceCollection services,
        Action<CoinbaseOptions>? configure = null) =>
        ExchangeServiceRegistration.AddExchange<CoinbaseOptions, IMapper>(
            services,
            ExchangeId.Coinbase,
            CoinbaseClientName,
            optionsName: "CoinbaseOptions",
            applyEnvDefaults: ApplyEnvDefaults,
            configure: configure,
            timeoutSecondsSelector: o => o.TimeoutSeconds,
            // Host-only BaseAddress (no path) so RequestUri.PathAndQuery equals the JWT uri claim path.
            // ExchangeUrl.NormalizeHostRoot fails fast if a path segment is present (would break signing).
            baseUrlSelector: o => ExchangeUrl.NormalizeHostRoot(o.BaseUrl),
            symbolMapperFactory: () => new SymbolMapper(CoinbaseSymbolFormat.Instance),
            mapperFactory: CoinbaseClientComposer.CreateMapper,
            configureHttpClient: null,
            resilienceOptions: new ResilienceOptions(),
            translatorFactory: _ => new CoinbaseErrorTranslator(),
            requestFinalizerFactory: sp =>
            {
                var o = sp.GetRequiredService<CoinbaseOptions>();
                if (string.IsNullOrEmpty(o.ApiKey) || string.IsNullOrEmpty(o.PrivateKey))
                    return new PassThroughHandler();
                return new CoinbaseSigningHandler(new CoinbaseJwtSigner(o.ApiKey, o.PrivateKey));
            },
            exchangeClientFactory: (sp, httpClient, holder) =>
                CoinbaseClientComposer.ComposeForDi(sp, httpClient, holder));

    private static void ApplyEnvDefaults(CoinbaseOptions o)
    {
        var k = Environment.GetEnvironmentVariable("COINBASE_API_KEY");
        var p = Environment.GetEnvironmentVariable("COINBASE_PRIVATE_KEY");
        if (!string.IsNullOrEmpty(k)) o.ApiKey = k;
        if (!string.IsNullOrEmpty(p)) o.PrivateKey = p;
    }
}
