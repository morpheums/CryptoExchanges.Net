using CryptoExchanges.Net.Core.Auth;
using CryptoExchanges.Net.Kucoin.Auth;

namespace CryptoExchanges.Net.Kucoin.Resilience;

/// <summary>
/// For signed KuCoin requests, (re)computes the Unix-ms timestamp + base64 HMAC signature ON EACH
/// ATTEMPT and sets the five KuCoin KC-API credential headers: <c>KC-API-KEY</c>,
/// <c>KC-API-SIGN</c>, <c>KC-API-TIMESTAMP</c>, <c>KC-API-PASSPHRASE</c>, and
/// <c>KC-API-KEY-VERSION</c> (always <c>2</c>). Sits below the retry strategy so a retried,
/// delayed request is re-signed with a fresh timestamp (avoids KuCoin timestamp-expiry
/// rejections). Unlike OKX, KuCoin passphrase-v2 requires the passphrase itself to be
/// HMAC-SHA256-signed and base64-encoded; see <see cref="KucoinSignatureService.SignPassphrase"/>.
/// Unsigned (public) requests pass through untouched with no KC-API-* headers added.
/// </summary>
/// <param name="apiKey">The KuCoin API key set on the <c>KC-API-KEY</c> header for signed requests.</param>
/// <param name="passphrase">The KuCoin API passphrase; will be HMAC-signed and base64-encoded before being set on <c>KC-API-PASSPHRASE</c>.</param>
/// <param name="signatureService">Computes the base64 HMAC-SHA256 signature over the KuCoin prehash string and signs the passphrase.</param>
/// <param name="timeOffset">Returns the current server-time offset in milliseconds, applied to each timestamp.</param>
internal sealed class KucoinSigningHandler(
    string apiKey, string passphrase, KucoinSignatureService signatureService, Func<long> timeOffset)
    : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (KucoinSigningRequest.IsSigned(request))
            await ResignAsync(request, cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task ResignAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // A signed KuCoin request needs all three credentials: key, secret (held by the signature
        // service), and passphrase. Guard here even though the composer's secret-gated finalizer
        // should normally prevent constructing a signing handler without full credentials.
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                "KuCoin signed request requires an API key (KC-API-KEY); none was configured.");
        if (string.IsNullOrEmpty(passphrase))
            throw new InvalidOperationException(
                "KuCoin signed request requires a passphrase (KC-API-PASSPHRASE); none was configured.");

        // Fresh timestamp per attempt: UtcNow shifted by the server-time offset, then Unix epoch ms.
        var instant = DateTimeOffset.UtcNow.AddMilliseconds(timeOffset());
        var timestamp = KucoinSignatureService.FormatTimestamp(instant);

        // Sign the ACTUAL outgoing path+query. Using RequestUri.PathAndQuery (rather than
        // reconstructing it) guarantees byte-for-byte consistency with whatever KucoinHttpClient
        // built, so the signed prehash matches exactly what KuCoin receives.
        var requestPath = request.RequestUri!.PathAndQuery;
        var method = request.Method.Method;

        var body = string.Empty;
        if ((request.Method == HttpMethod.Post || request.Method == HttpMethod.Put)
            && request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }

        var prehash = KucoinSignatureService.BuildPrehash(timestamp, method, requestPath, body);
        var signature = signatureService.Sign(prehash);

        // KuCoin passphrase-v2: sign the passphrase itself with HMAC-SHA256 + base64.
        var signedPassphrase = signatureService.SignPassphrase(passphrase);

        // Strip any signing headers from a prior attempt so a retried request re-signs cleanly
        // (exactly one set of KC-API-* headers with a fresh timestamp/signature).
        request.Headers.Remove("KC-API-KEY");
        request.Headers.Remove("KC-API-SIGN");
        request.Headers.Remove("KC-API-TIMESTAMP");
        request.Headers.Remove("KC-API-PASSPHRASE");
        request.Headers.Remove("KC-API-KEY-VERSION");
        request.Headers.Add("KC-API-KEY", apiKey);
        request.Headers.Add("KC-API-SIGN", signature);
        request.Headers.Add("KC-API-TIMESTAMP", timestamp);
        request.Headers.Add("KC-API-PASSPHRASE", signedPassphrase);
        request.Headers.Add("KC-API-KEY-VERSION", "2");
    }
}
