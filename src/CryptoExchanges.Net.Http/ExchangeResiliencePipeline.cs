using System.Net;
using CryptoExchanges.Net.Core.Resilience;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace CryptoExchanges.Net.Http;

/// <summary>Shared definition of the resilience pipeline (single source of truth for both
/// the DI and factory-less paths).</summary>
public static class ExchangeResiliencePipeline
{
    private static bool IsTransientStatus(HttpStatusCode s)
        => (int)s >= 500 || s == HttpStatusCode.RequestTimeout
        || s == HttpStatusCode.TooManyRequests || (int)s == 418;

    /// <summary>Configures retry (GET-only) + per-attempt timeout on the builder.</summary>
    public static void Configure(
        ResiliencePipelineBuilder<HttpResponseMessage> builder, ResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetries,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = options.BaseDelay,
            MaxDelay = options.MaxDelay,
            ShouldHandle = args =>
            {
                // On an exception outcome, Result is null; recover the in-flight request from the
                // resilience context so the GET-only guard still works on the exception path.
                var req = args.Outcome.Result?.RequestMessage ?? args.Context.GetRequestMessage();
                if (req?.Method != HttpMethod.Get)
                    return ValueTask.FromResult(false);
                if (args.Outcome.Exception is HttpRequestException or TimeoutRejectedException)
                    return ValueTask.FromResult(true);
                var status = args.Outcome.Result?.StatusCode;
                return ValueTask.FromResult(status is { } s && IsTransientStatus(s));
            },
            DelayGenerator = args =>
            {
                var retryAfter = args.Outcome.Result?.Headers.RetryAfter?.Delta;
                return ValueTask.FromResult(retryAfter);
            }
        });

        builder.AddTimeout(options.PerAttemptTimeout);
    }
}
