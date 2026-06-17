using System.Net;
using CryptoExchanges.Net.Core.Exceptions;
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

/// <summary>
/// Pipeline-level (post-retry) handler that maps an exhausted/failed transient outcome to the
/// right typed exception — the per-response translator cannot, since it can't know an attempt
/// was the last. Sits OUTSIDE the retry/timeout pipeline (above it). Business 4xx are already
/// thrown by the inner ErrorTranslationHandler and pass through here untouched.
/// </summary>
public sealed class TransientExhaustionHandler : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutRejectedException ex)
        {
            throw new ExchangeConnectivityException("Request timed out after retries.", IsMutation(request), ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ExchangeConnectivityException("Network failure after retries.", IsMutation(request), ex);
        }

        if (response.IsSuccessStatusCode)
            return response;

        var status = response.StatusCode;
        if (status == HttpStatusCode.TooManyRequests || (int)status == 418)
        {
            var retryAfter = RetryAfterReader.GetDelay(response);
            var body = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            response.Dispose();
            throw new RateLimitExceededException("Rate limit exceeded.", retryAfter, rawBody: body);
        }
        if ((int)status >= 500 || status == HttpStatusCode.RequestTimeout)
        {
            response.Dispose();
            throw new ExchangeConnectivityException(
                $"Exchange unavailable (HTTP {(int)status}) after retries.", IsMutation(request));
        }
        return response;
    }

    private static bool IsMutation(HttpRequestMessage r) => r.Method != HttpMethod.Get;
}
