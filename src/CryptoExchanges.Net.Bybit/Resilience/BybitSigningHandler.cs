using System.Globalization;
using CryptoExchanges.Net.Bybit.Auth;
using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Bybit.Resilience;

/// <summary>
/// Adds the Bybit API-key header to every request, and for signed requests (re)computes the
/// timestamp + HMAC signature ON EACH ATTEMPT. Sits below the retry strategy so a retried,
/// delayed request is re-signed with a fresh timestamp (avoids recvWindow rejections).
/// Unlike Binance, the signature is carried in headers (<c>X-BAPI-SIGN</c>) rather than the query.
/// Supports query-signed (GET/DELETE) and body-signed (POST JSON) requests.
/// </summary>
/// <param name="apiKey">The Bybit API key set on the <c>X-BAPI-API-KEY</c> header.</param>
/// <param name="signatureService">Computes the HMAC-SHA256 signature over the canonical sign-string.</param>
/// <param name="recvWindow">The receive-window value (pre-formatted, invariant) for the <c>X-BAPI-RECV-WINDOW</c> header.</param>
/// <param name="timeOffset">Returns the current server-time offset in milliseconds, applied to each timestamp.</param>
internal sealed class BybitSigningHandler(
    string apiKey, ISignatureService signatureService, string recvWindow, Func<long> timeOffset)
    : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Remove("X-BAPI-API-KEY");
            request.Headers.Add("X-BAPI-API-KEY", apiKey);
        }

        if (BybitSigningRequest.IsSigned(request))
            await ResignAsync(request, cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task ResignAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var timestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeOffset())
            .ToString(CultureInfo.InvariantCulture);

        string signString;
        if (request.Method == HttpMethod.Post && request.Content is not null)
        {
            var jsonBody = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            signString = BybitSignatureService.BuildPostSignString(timestamp, apiKey, recvWindow, jsonBody);
        }
        else
        {
            var query = request.RequestUri?.Query.TrimStart('?') ?? string.Empty;
            signString = BybitSignatureService.BuildGetSignString(timestamp, apiKey, recvWindow, query);
        }

        var signature = signatureService.Sign(signString);

        // Strip any signing headers from a prior attempt so a retried request re-signs cleanly
        // (single, not doubled, X-BAPI-* headers with a fresh timestamp/signature).
        request.Headers.Remove("X-BAPI-TIMESTAMP");
        request.Headers.Remove("X-BAPI-RECV-WINDOW");
        request.Headers.Remove("X-BAPI-SIGN");
        request.Headers.Add("X-BAPI-TIMESTAMP", timestamp);
        request.Headers.Add("X-BAPI-RECV-WINDOW", recvWindow);
        request.Headers.Add("X-BAPI-SIGN", signature);
    }
}
