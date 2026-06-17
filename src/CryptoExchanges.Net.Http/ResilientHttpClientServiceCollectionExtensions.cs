using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Http;

/// <summary>DI registration for the resilient HTTP pipeline (IHttpClientFactory path).</summary>
public static class ResilientHttpClientServiceCollectionExtensions
{
    /// <summary>
    /// Configures a named HttpClient with the shared resilience pipeline. The caller supplies the
    /// per-exchange translator/gate/options and an optional request finalizer (e.g. a signing handler).
    /// </summary>
    public static IHttpClientBuilder AddResilientHttpClient(
        this IServiceCollection services,
        string name,
        ResilienceOptions options,
        Func<IServiceProvider, IExchangeErrorTranslator> translatorFactory,
        Func<IServiceProvider, IRateLimitGate> gateFactory,
        Func<IServiceProvider, DelegatingHandler>? requestFinalizerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(translatorFactory);
        ArgumentNullException.ThrowIfNull(gateFactory);

        var builder = services.AddHttpClient(name);
        return builder.ApplyResiliencePipeline(
            name, options, translatorFactory, gateFactory, requestFinalizerFactory);
    }

    /// <summary>
    /// Applies the shared resilience handler chain to an already-created <see cref="IHttpClientBuilder"/>
    /// (named OR typed). This is the single source of truth for the DI handler order, mirroring
    /// <c>HttpClientPipelineBuilder.Build</c> exactly (outer→inner):
    /// throttle → exhaustion-mapping → Polly(retry/timeout) → [optional request finalizer] →
    /// error-translation → primary transport.
    /// </summary>
    /// <param name="builder">The HTTP client builder to augment.</param>
    /// <param name="pipelineName">A unique name for the Polly resilience handler.</param>
    /// <param name="options">The resilience configuration.</param>
    /// <param name="translatorFactory">Factory for the per-exchange error translator.</param>
    /// <param name="gateFactory">Factory for the per-exchange rate-limit gate.</param>
    /// <param name="requestFinalizerFactory">Optional factory for a request finalizer (e.g. signing).</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static IHttpClientBuilder ApplyResiliencePipeline(
        this IHttpClientBuilder builder,
        string pipelineName,
        ResilienceOptions options,
        Func<IServiceProvider, IExchangeErrorTranslator> translatorFactory,
        Func<IServiceProvider, IRateLimitGate> gateFactory,
        Func<IServiceProvider, DelegatingHandler>? requestFinalizerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(translatorFactory);
        ArgumentNullException.ThrowIfNull(gateFactory);

        builder.AddHttpMessageHandler(sp => new RateLimitThrottleHandler(gateFactory(sp)));
        builder.AddHttpMessageHandler(_ => new TransientExhaustionHandler());
        builder.AddResilienceHandler(pipelineName + "-pipeline",
            b => ExchangeResiliencePipeline.Configure(b, options));
        if (requestFinalizerFactory is not null)
            builder.AddHttpMessageHandler(requestFinalizerFactory);
        builder.AddHttpMessageHandler(sp => new ErrorTranslationHandler(translatorFactory(sp)));

        return builder;
    }
}
