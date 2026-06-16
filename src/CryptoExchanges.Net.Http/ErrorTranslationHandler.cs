using System.Net;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Http;

/// <summary>
/// Innermost resilience handler. On a non-success response that represents a business error
/// (4xx other than transient 408/429), reads the body and throws the exchange's typed exception.
/// Transient outcomes (5xx, 408, 429, 418) are passed through unchanged so the outer retry
/// strategy can act and the pipeline-level exhaustion mapping can run.
/// </summary>
public sealed class ErrorTranslationHandler(IExchangeErrorTranslator translator) : DelegatingHandler
{
    private static bool IsTransient(HttpStatusCode status)
        => (int)status >= 500
        || status == HttpStatusCode.RequestTimeout
        || status == HttpStatusCode.TooManyRequests
        || (int)status == 418;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode || IsTransient(response.StatusCode))
            return response;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw translator.Translate(response, body);
    }
}
