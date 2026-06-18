using System.Globalization;
using CryptoExchanges.Net.Bybit.Auth;
using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Bybit.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Bybit;

/// <summary>
/// Dependency-injection extensions for registering the Bybit exchange client.
/// Lives in the Bybit assembly so a consumer can depend on Bybit alone without
/// transitively pulling in other exchange assemblies (see ADR-001).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Named-client / resilience-pipeline name for the Bybit HTTP client — shared by the
    /// registration and the <c>CreateClient</c> call so they can't drift.</summary>
    private const string BybitClientName = "bybit";

    /// <summary>
    /// Registers the Bybit exchange client and all its dependencies as per-exchange keyed singletons,
    /// backed by a typed <see cref="System.Net.Http.HttpClient"/> with the full resilience handler chain.
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="BybitOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddBybitExchange(
        this IServiceCollection services,
        Action<BybitOptions>? configure = null) =>
        // Delegates to the shared Http registration helper; only the Bybit variation points differ.
        // Bybit sets NO default api-key header (it signs per-request), so configureHttpClient is null,
        // and BybitHttpClient's ctor takes (httpClient) only. The finalizer is ALWAYS registered: a
        // secretless client resolves to a no-op PassThroughHandler (mirrors Create()).
        ExchangeServiceRegistration.AddExchange<BybitOptions, IMapper>(
            services,
            ExchangeId.Bybit,
            BybitClientName,
            optionsName: "BybitOptions",
            applyEnvDefaults: ApplyEnvDefaults,
            configure: configure,
            timeoutSecondsSelector: o => o.TimeoutSeconds,
            baseUrlSelector: o => o.BaseUrl,
            symbolMapperFactory: () => new SymbolMapper(BybitSymbolFormat.Instance),
            mapperFactory: BybitClientComposer.CreateMapper,
            configureHttpClient: null,
            resilienceOptions: new ResilienceOptions { UsageHeaderName = "X-Bapi-Limit-Status" },
            translatorFactory: _ => new BybitErrorTranslator(),
            requestFinalizerFactory: sp =>
            {
                var o = sp.GetRequiredService<BybitOptions>();
                if (string.IsNullOrEmpty(o.SecretKey))
                    return new PassThroughHandler();
                var holder = sp.GetRequiredKeyedService<long[]>(ExchangeId.Bybit);
                return new BybitSigningHandler(
                    o.ApiKey,
                    new BybitSignatureService(o.SecretKey),
                    o.ReceiveWindow.ToString(CultureInfo.InvariantCulture),
                    () => Interlocked.Read(ref holder[0]));
            },
            exchangeClientFactory: (sp, httpClient, holder) =>
                BybitClientComposer.ComposeForDi(sp, new BybitHttpClient(httpClient), holder));

    private static void ApplyEnvDefaults(BybitOptions o)
    {
        var k = Environment.GetEnvironmentVariable("BYBIT_API_KEY");
        var s = Environment.GetEnvironmentVariable("BYBIT_SECRET_KEY");
        if (!string.IsNullOrEmpty(k)) o.ApiKey = k;
        if (!string.IsNullOrEmpty(s)) o.SecretKey = s;
    }
}
