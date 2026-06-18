using CryptoExchanges.Net.Okx.Auth;

namespace CryptoExchanges.Net.Okx.Resilience;

/// <summary>
/// For signed OKX requests, (re)computes the ISO-8601 timestamp + base64 HMAC signature ON EACH
/// ATTEMPT and sets the four OKX credential headers: <c>OK-ACCESS-KEY</c>, <c>OK-ACCESS-SIGN</c>,
/// <c>OK-ACCESS-TIMESTAMP</c>, and <c>OK-ACCESS-PASSPHRASE</c>. Sits below the retry strategy so a
/// retried, delayed request is re-signed with a fresh timestamp (avoids OKX timestamp-expiry
/// rejections). Unlike Bybit, OKX's <c>OK-ACCESS-KEY</c> is only meaningful for private/signed calls,
/// so unsigned (public) requests pass through untouched with no auth headers added.
/// </summary>
/// <param name="apiKey">The OKX API key set on the <c>OK-ACCESS-KEY</c> header for signed requests.</param>
/// <param name="passphrase">The OKX API passphrase set on the <c>OK-ACCESS-PASSPHRASE</c> header for signed requests.</param>
/// <param name="signatureService">Computes the base64 HMAC-SHA256 signature over the OKX prehash string.</param>
/// <param name="timeOffset">Returns the current server-time offset in milliseconds, applied to each timestamp.</param>
internal sealed class OkxSigningHandler(
    string apiKey, string passphrase, OkxSignatureService signatureService, Func<long> timeOffset)
    : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (OkxSigningRequest.IsSigned(request))
            await ResignAsync(request, cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task ResignAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // A signed OKX request needs all three credentials: key, secret (held by the signature
        // service), and passphrase. Guard here even though the composer's secret-gated finalizer
        // should normally prevent constructing a signing handler without full credentials.
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                "OKX signed request requires an API key (OK-ACCESS-KEY); none was configured.");
        if (string.IsNullOrEmpty(passphrase))
            throw new InvalidOperationException(
                "OKX signed request requires a passphrase (OK-ACCESS-PASSPHRASE); none was configured.");

        // Fresh timestamp per attempt: UtcNow shifted by the server-time offset, then ISO-8601 UTC ms.
        var instant = DateTimeOffset.UtcNow.AddMilliseconds(timeOffset());
        var timestamp = OkxSignatureService.FormatTimestamp(instant);

        // Sign the ACTUAL outgoing path+query. Using RequestUri.PathAndQuery (rather than
        // reconstructing it) guarantees byte-for-byte consistency with whatever OkxHttpClient built,
        // so the signed prehash matches exactly what OKX receives.
        var requestPath = request.RequestUri!.PathAndQuery;
        var method = request.Method.Method;

        var body = string.Empty;
        if ((request.Method == HttpMethod.Post || request.Method == HttpMethod.Put)
            && request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }

        var prehash = OkxSignatureService.BuildPrehash(timestamp, method, requestPath, body);
        var signature = signatureService.Sign(prehash);

        // Strip any signing headers from a prior attempt so a retried request re-signs cleanly
        // (exactly one set of OK-ACCESS-* headers with a fresh timestamp/signature).
        request.Headers.Remove("OK-ACCESS-KEY");
        request.Headers.Remove("OK-ACCESS-SIGN");
        request.Headers.Remove("OK-ACCESS-TIMESTAMP");
        request.Headers.Remove("OK-ACCESS-PASSPHRASE");
        request.Headers.Add("OK-ACCESS-KEY", apiKey);
        request.Headers.Add("OK-ACCESS-SIGN", signature);
        request.Headers.Add("OK-ACCESS-TIMESTAMP", timestamp);
        request.Headers.Add("OK-ACCESS-PASSPHRASE", passphrase);
    }
}
