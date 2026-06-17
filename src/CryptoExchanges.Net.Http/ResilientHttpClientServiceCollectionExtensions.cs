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

        builder.AddHttpMessageHandler(sp => new RateLimitThrottleHandler(gateFactory(sp)));
        builder.AddHttpMessageHandler(_ => new TransientExhaustionHandler());
        builder.AddResilienceHandler(name + "-pipeline",
            b => ExchangeResiliencePipeline.Configure(b, options));
        if (requestFinalizerFactory is not null)
            builder.AddHttpMessageHandler(requestFinalizerFactory);
        builder.AddHttpMessageHandler(sp => new ErrorTranslationHandler(translatorFactory(sp)));

        return builder;
    }
}
