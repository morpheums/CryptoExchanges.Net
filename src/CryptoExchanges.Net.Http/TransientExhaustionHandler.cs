using System.Net;
using CryptoExchanges.Net.Core.Exceptions;
using Polly.Timeout;

namespace CryptoExchanges.Net.Http;

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
