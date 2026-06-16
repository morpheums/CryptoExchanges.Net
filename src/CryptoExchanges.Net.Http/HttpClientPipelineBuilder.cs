using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace CryptoExchanges.Net.Http;

/// <summary>Builds a resilient <see cref="HttpClient"/> for the factory-less path
/// (e.g. an exchange client's <c>Create(...)</c>), composing the same pipeline used by DI.</summary>
public static class HttpClientPipelineBuilder
{
    /// <summary>
    /// Composes (outer→inner): throttle → exhaustion-mapping → Polly(retry/timeout) →
    /// [optional per-exchange request finalizer] → error-translation → inner transport.
    /// </summary>
    public static HttpClient Build(
        HttpMessageHandler innerHandler,
        ResilienceOptions options,
        IExchangeErrorTranslator translator,
        IRateLimitGate gate,
        DelegatingHandler? requestFinalizer)
    {
        ArgumentNullException.ThrowIfNull(innerHandler);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(gate);

        var pipelineBuilder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        ExchangeResiliencePipeline.Configure(pipelineBuilder, options);
        ResiliencePipeline<HttpResponseMessage> pipeline = pipelineBuilder.Build();

        var errorTranslation = new ErrorTranslationHandler(translator) { InnerHandler = innerHandler };

        DelegatingHandler belowPolly = errorTranslation;
        if (requestFinalizer is not null)
        {
            requestFinalizer.InnerHandler = errorTranslation;
            belowPolly = requestFinalizer;
        }

        // CA2000: handler chain ownership transfers to HttpClient on construction — it disposes them.
#pragma warning disable CA2000
        var resilience = new ResilienceHandler(pipeline) { InnerHandler = belowPolly };
        var exhaustion = new TransientExhaustionHandler { InnerHandler = resilience };
        var throttle = new RateLimitThrottleHandler(gate) { InnerHandler = exhaustion };
        return new HttpClient(throttle);
#pragma warning restore CA2000
    }
}
