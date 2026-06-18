using System.Globalization;
using CryptoExchanges.Net.Bybit.Auth;
using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Bybit.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
        Action<BybitOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IExchangeClientFactory, ExchangeClientFactory>();

        services.AddOptions<BybitOptions>()
            .Configure(ApplyEnvDefaults)
            .Configure(o => configure?.Invoke(o))
            .Validate(o => o.TimeoutSeconds > 0, "BybitOptions.TimeoutSeconds must be > 0.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "BybitOptions.BaseUrl is required.")
            .ValidateOnStart();

        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<BybitOptions>>().Value);

        // Shared clock-skew offset holder (single-element array so the signing handler's closure and
        // SyncServerTimeAsync read/write the SAME instance). One per exchange registration.
        // CA1861: this must be a FRESH mutable instance per registration (not a shared static field),
        // and the factory runs once for the singleton — so the "constant array" rule doesn't apply.
#pragma warning disable CA1861
        services.TryAddKeyedSingleton(ExchangeId.Bybit, (_, _) => new long[] { 0L });
#pragma warning restore CA1861
        services.TryAddKeyedSingleton<ISymbolMapper>(ExchangeId.Bybit,
            (_, _) => new SymbolMapper(BybitSymbolFormat.Instance));
        services.TryAddKeyedSingleton<IMapper>(ExchangeId.Bybit, (sp, _) =>
            BybitClientComposer.CreateMapper(sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Bybit)));

        // NAMED client (not typed): a typed client registers IBybitHttpClient as TRANSIENT, and
        // capturing it inside the keyed IExchangeClient SINGLETON below would be a captive dependency
        // (the HttpClient + handler chain would never rotate). Instead we resolve ONE long-lived
        // HttpClient via IHttpClientFactory.CreateClient(BybitClientName) in the singleton factory; DNS
        // rotation is handled by PooledConnectionLifetime on the primary handler.
        var http = services.AddHttpClient(BybitClientName, (sp, c) =>
            {
                var o = sp.GetRequiredService<BybitOptions>();
                c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/'));
                c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
                c.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });

        // Handler chain — applied via the Http project's shared seam so the DI path and the
        // container-free Create() path (HttpClientPipelineBuilder.Build) compose the SAME effective
        // pipeline. Order (outer→inner): throttle → exhaustion-mapping → Polly(retry/timeout) →
        // request-finalizer(signing) → error-translation → primary transport.
        //
        // The finalizer is ALWAYS registered: options are only final at resolution time, so the
        // no-secret gate is decided then — a secretless client gets a no-op PassThroughHandler instead
        // of a BybitSigningHandler (mirrors Create(), where signing is a PassThrough when SecretKey is empty).
        http.ApplyResiliencePipeline(
            BybitClientName,
            new ResilienceOptions { UsageHeaderName = "X-Bapi-Limit-Status" },
            translatorFactory: _ => new BybitErrorTranslator(),
            gateFactory: _ => new ReactiveRateLimitGate(),
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
            });

        services.AddKeyedSingleton<IExchangeClient>(ExchangeId.Bybit, (sp, _) =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(BybitClientName);
            var http = new BybitHttpClient(httpClient);
            var holder = sp.GetRequiredKeyedService<long[]>(ExchangeId.Bybit);
            return BybitClientComposer.ComposeForDi(sp, http, holder);
        });

        return services;
    }

    private static void ApplyEnvDefaults(BybitOptions o)
    {
        var k = Environment.GetEnvironmentVariable("BYBIT_API_KEY");
        var s = Environment.GetEnvironmentVariable("BYBIT_SECRET_KEY");
        if (!string.IsNullOrEmpty(k)) o.ApiKey = k;
        if (!string.IsNullOrEmpty(s)) o.SecretKey = s;
    }
}
