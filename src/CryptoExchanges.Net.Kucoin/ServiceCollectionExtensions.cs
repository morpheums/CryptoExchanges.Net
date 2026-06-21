using CryptoExchanges.Net.Kucoin.Auth;
using CryptoExchanges.Net.Kucoin.Internal;
using CryptoExchanges.Net.Kucoin.Resilience;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Kucoin;

/// <summary>
/// Dependency-injection extensions for registering the KuCoin exchange client. Lives in the KuCoin
/// assembly so a consumer can depend on KuCoin alone without transitively pulling in other exchange
/// assemblies (see ADR-001).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Named-client / resilience-pipeline name for the KuCoin HTTP client — shared by the
    /// registration and the <c>CreateClient</c> call so they can't drift.</summary>
    private const string KucoinClientName = "kucoin";

    /// <summary>
    /// Registers the KuCoin exchange client as a per-exchange keyed singleton with the full resilience
    /// handler chain. Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="KucoinOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddKucoinExchange(
        this IServiceCollection services,
        Action<KucoinOptions>? configure = null) =>
        ExchangeServiceRegistration.AddExchange<KucoinOptions, IMapper>(
            services,
            ExchangeId.Kucoin,
            KucoinClientName,
            optionsName: "KucoinOptions",
            applyEnvDefaults: ApplyEnvDefaults,
            configure: configure,
            timeoutSecondsSelector: o => o.TimeoutSeconds,
            // Host-only BaseAddress so RequestUri.PathAndQuery matches the signed prehash exactly.
            baseUrlSelector: o => ExchangeUrl.NormalizeHostRoot(o.BaseUrl),
            symbolMapperFactory: () => new KucoinSymbolMapper(),
            mapperFactory: KucoinClientComposer.CreateMapper,
            configureHttpClient: null,
            resilienceOptions: new ResilienceOptions(),
            translatorFactory: _ => new KucoinErrorTranslator(),
            // Gate on both SecretKey AND Passphrase; missing either → no-op PassThroughHandler.
            requestFinalizerFactory: sp =>
            {
                var o = sp.GetRequiredService<KucoinOptions>();
                if (string.IsNullOrEmpty(o.SecretKey) || string.IsNullOrEmpty(o.Passphrase))
                    return new PassThroughHandler();
                var holder = sp.GetRequiredKeyedService<long[]>(ExchangeId.Kucoin);
                return new KucoinSigningHandler(
                    o.ApiKey,
                    o.Passphrase,
                    new KucoinSignatureService(o.SecretKey),
                    () => Interlocked.Read(ref holder[0]));
            },
            exchangeClientFactory: (sp, httpClient, holder) =>
                KucoinClientComposer.ComposeForDi(sp, new KucoinHttpClient(httpClient), holder));

    private static void ApplyEnvDefaults(KucoinOptions o)
    {
        var k = Environment.GetEnvironmentVariable("KUCOIN_API_KEY");
        var s = Environment.GetEnvironmentVariable("KUCOIN_SECRET_KEY");
        var p = Environment.GetEnvironmentVariable("KUCOIN_PASSPHRASE");
        if (!string.IsNullOrEmpty(k)) o.ApiKey = k;
        if (!string.IsNullOrEmpty(s)) o.SecretKey = s;
        if (!string.IsNullOrEmpty(p)) o.Passphrase = p;
    }
}
