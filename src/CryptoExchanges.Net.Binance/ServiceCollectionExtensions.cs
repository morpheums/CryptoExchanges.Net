using CryptoExchanges.Net.Binance.Auth;
using CryptoExchanges.Net.Binance.Internal;
using CryptoExchanges.Net.Binance.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Binance;

/// <summary>
/// Dependency-injection extensions for registering the Binance exchange client.
/// Lives in the Binance assembly so a consumer can depend on Binance alone without
/// transitively pulling in other exchange assemblies (see ADR-001).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Named-client / resilience-pipeline name for the Binance HTTP client — shared by the
    /// registration and the <c>CreateClient</c> call so they can't drift.</summary>
    private const string ClientName = "binance";

    /// <summary>
    /// Registers the Binance exchange client and all its dependencies as per-exchange keyed singletons,
    /// backed by a typed <see cref="System.Net.Http.HttpClient"/> with the full resilience handler chain.
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="BinanceOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddBinanceExchange(
        this IServiceCollection services,
        Action<BinanceOptions>? configure = null) =>
        // Delegates to the shared Http registration helper; only the Binance variation points differ.
        // Wrinkles handled here: (1) Binance sets a default X-MBX-APIKEY header on the HttpClient
        // (Bybit/OKX do not — they sign per-request); (2) BinanceHttpClient's ctor takes (httpClient,
        // options) whereas Bybit/OkxHttpClient take (httpClient) only. The finalizer is ALWAYS
        // registered: a secretless client resolves to a no-op PassThroughHandler (mirrors Create()).
        ExchangeServiceRegistration.AddExchange<BinanceOptions, IMapper>(
            services,
            ExchangeId.Binance,
            ClientName,
            optionsName: "BinanceOptions",
            applyEnvDefaults: ApplyEnvDefaults,
            configure: configure,
            timeoutSecondsSelector: o => o.TimeoutSeconds,
            baseUrlSelector: o => o.BaseUrl,
            symbolMapperFactory: () => new SymbolMapper(BinanceSymbolFormat.Instance),
            mapperFactory: BinanceClientComposer.CreateMapper,
            configureHttpClient: (c, o) =>
            {
                if (!string.IsNullOrEmpty(o.ApiKey))
                    c.DefaultRequestHeaders.Add("X-MBX-APIKEY", o.ApiKey);
            },
            resilienceOptions: new ResilienceOptions { UsageHeaderName = "X-MBX-USED-WEIGHT-1m" },
            translatorFactory: _ => new BinanceErrorTranslator(),
            requestFinalizerFactory: sp =>
            {
                var o = sp.GetRequiredService<BinanceOptions>();
                if (string.IsNullOrEmpty(o.SecretKey))
                    return new PassThroughHandler();
                var holder = sp.GetRequiredKeyedService<long[]>(ExchangeId.Binance);
                return new BinanceSigningHandler(o.ApiKey, new BinanceSignatureService(o.SecretKey),
                    () => Interlocked.Read(ref holder[0]));
            },
            exchangeClientFactory: (sp, httpClient, holder) =>
            {
                var options = sp.GetRequiredService<BinanceOptions>();
                var http = new BinanceHttpClient(httpClient, options);
                return BinanceClientComposer.ComposeForDi(sp, http, holder);
            });

    private static void ApplyEnvDefaults(BinanceOptions o)
    {
        var k = Environment.GetEnvironmentVariable("BINANCE_API_KEY");
        var s = Environment.GetEnvironmentVariable("BINANCE_SECRET_KEY");
        if (!string.IsNullOrEmpty(k)) o.ApiKey = k;
        if (!string.IsNullOrEmpty(s)) o.SecretKey = s;
    }
}
