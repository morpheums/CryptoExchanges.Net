using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CryptoExchanges.Net.Http;

/// <summary>Shared body of every <c>AddXxxExchange</c> registration; each exchange supplies only its variation points.</summary>
internal static class ExchangeServiceRegistration
{
    /// <summary>
    /// Registers an exchange client and all its dependencies as per-exchange keyed singletons.
    /// </summary>
    /// <typeparam name="TOptions">The per-exchange options type.</typeparam>
    /// <typeparam name="TMapper">
    /// The per-exchange mapper type (supplied as a generic argument to preserve the Http layer's
    /// layering invariant — no mapping-library reference in Http). Registered as a keyed singleton
    /// resolved by the composer's <c>ComposeForDi</c>.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="exchangeId">The per-exchange key for all keyed singletons.</param>
    /// <param name="clientName">The named-client / resilience-pipeline name.</param>
    /// <param name="optionsName">The options type name used to build identical validation messages.</param>
    /// <param name="applyEnvDefaults">Applies environment-variable defaults to the options.</param>
    /// <param name="configure">The user-supplied options configuration (may be null).</param>
    /// <param name="timeoutSecondsSelector">Reads <c>TimeoutSeconds</c> off the options (keeps options types untouched).</param>
    /// <param name="baseUrlSelector">Reads <c>BaseUrl</c> off the options (keeps options types untouched).</param>
    /// <param name="symbolMapperFactory">Builds the per-exchange <see cref="ISymbolMapper"/>.</param>
    /// <param name="mapperFactory">Builds the per-exchange <typeparamref name="TMapper"/> from the symbol mapper.</param>
    /// <param name="configureHttpClient">
    /// Optional per-exchange extra HttpClient configuration applied AFTER the shared base config
    /// (BaseAddress/Timeout/User-Agent). Binance uses this to add its default <c>X-MBX-APIKEY</c> header;
    /// Bybit/OKX pass null (they set the api key per-request in the signing handler).
    /// </param>
    /// <param name="resilienceOptions">The resilience configuration (carries the usage-header name).</param>
    /// <param name="translatorFactory">Factory for the per-exchange error translator.</param>
    /// <param name="requestFinalizerFactory">Factory for the per-exchange request finalizer (signing/pass-through gate).</param>
    /// <param name="exchangeClientFactory">
    /// Builds the <see cref="IExchangeClient"/> from the resolved long-lived HttpClient and the keyed
    /// offset holder. Per-exchange because the typed http-client ctors differ (BinanceHttpClient takes
    /// (httpClient, options); Bybit/OkxHttpClient take (httpClient) only).
    /// </param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddExchange<TOptions, TMapper>(
        IServiceCollection services,
        ExchangeId exchangeId,
        string clientName,
        string optionsName,
        Action<TOptions> applyEnvDefaults,
        Action<TOptions>? configure,
        Func<TOptions, int> timeoutSecondsSelector,
        Func<TOptions, string> baseUrlSelector,
        Func<ISymbolMapper> symbolMapperFactory,
        Func<ISymbolMapper, TMapper> mapperFactory,
        Action<HttpClient, TOptions>? configureHttpClient,
        ResilienceOptions resilienceOptions,
        Func<IServiceProvider, IExchangeErrorTranslator> translatorFactory,
        Func<IServiceProvider, DelegatingHandler> requestFinalizerFactory,
        Func<IServiceProvider, HttpClient, long[], IExchangeClient> exchangeClientFactory)
        where TOptions : class
        where TMapper : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IExchangeClientFactory, ExchangeClientFactory>();

        // Exchange-agnostic clock-skew calculator. Non-keyed + TryAdd so a consumer can override it
        // once (a prior registration wins) and every exchange shares the same implementation.
        services.TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>();

        services.AddOptions<TOptions>()
            .Configure(applyEnvDefaults)
            .Configure(o => configure?.Invoke(o))
            .Validate(o => timeoutSecondsSelector(o) > 0, $"{optionsName}.TimeoutSeconds must be > 0.")
            .Validate(o => !string.IsNullOrWhiteSpace(baseUrlSelector(o)), $"{optionsName}.BaseUrl is required.")
            .ValidateOnStart();

        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<TOptions>>().Value);

        // Shared clock-skew offset holder (single-element array so the signing handler's closure and
        // SyncServerTimeAsync read/write the SAME instance). One per exchange registration.
        // CA1861: this must be a FRESH mutable instance per registration (not a shared static field),
        // and the factory runs once for the singleton — so the "constant array" rule doesn't apply.
#pragma warning disable CA1861
        services.TryAddKeyedSingleton(exchangeId, (_, _) => new long[] { 0L });
#pragma warning restore CA1861
        services.TryAddKeyedSingleton<ISymbolMapper>(exchangeId, (_, _) => symbolMapperFactory());
        services.TryAddKeyedSingleton<TMapper>(exchangeId, (sp, _) =>
            mapperFactory(sp.GetRequiredKeyedService<ISymbolMapper>(exchangeId)));

        // NAMED client (not typed): a typed client registers IXxxHttpClient as TRANSIENT, and capturing
        // it inside the keyed IExchangeClient SINGLETON below would be a captive dependency (the
        // HttpClient + handler chain would never rotate). Instead we resolve ONE long-lived HttpClient
        // via IHttpClientFactory.CreateClient(clientName) in the singleton factory; DNS rotation is
        // handled by PooledConnectionLifetime on the primary handler.
        var http = services.AddHttpClient(clientName, (sp, c) =>
            {
                var o = sp.GetRequiredService<TOptions>();
                c.BaseAddress = new Uri(baseUrlSelector(o).TrimEnd('/'));
                c.Timeout = TimeSpan.FromSeconds(timeoutSecondsSelector(o));
                c.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
                configureHttpClient?.Invoke(c, o);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });

        // Handler chain — applied via the Http project's shared seam so the DI path and the
        // container-free Create() path (HttpClientPipelineBuilder.Build) compose the SAME effective
        // pipeline. Order (outer→inner): throttle → exhaustion-mapping → Polly(retry/timeout) →
        // request-finalizer(signing) → error-translation → primary transport.
        //
        // The finalizer is ALWAYS registered: options are only final at resolution time, so the
        // no-secret gate is decided then — a credentialless client gets a no-op PassThroughHandler.
        http.ApplyResiliencePipeline(
            clientName,
            resilienceOptions,
            translatorFactory: translatorFactory,
            gateFactory: _ => new ReactiveRateLimitGate(),
            requestFinalizerFactory: requestFinalizerFactory);

        services.AddKeyedSingleton<IExchangeClient>(exchangeId, (sp, _) =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
            var holder = sp.GetRequiredKeyedService<long[]>(exchangeId);
            return exchangeClientFactory(sp, httpClient, holder);
        });

        return services;
    }
}
