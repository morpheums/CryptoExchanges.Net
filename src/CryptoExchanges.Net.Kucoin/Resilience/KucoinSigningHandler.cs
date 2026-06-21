using CryptoExchanges.Net.Core.Auth;
using CryptoExchanges.Net.Kucoin.Auth;

namespace CryptoExchanges.Net.Kucoin.Resilience;

/// <summary>
/// Re-signs KuCoin requests on every attempt: sets the five <c>KC-API-*</c> credential headers
/// with a fresh Unix-ms timestamp. Unsigned requests pass through untouched.
/// </summary>
/// <param name="apiKey">KuCoin API key for the <c>KC-API-KEY</c> header.</param>
/// <param name="passphrase">Raw passphrase; HMAC-signed before being set on <c>KC-API-PASSPHRASE</c>.</param>
/// <param name="signatureService">Computes the HMAC-SHA256 signature and signs the passphrase.</param>
/// <param name="timeOffset">Returns the server-time offset in milliseconds applied to each timestamp.</param>
internal sealed class KucoinSigningHandler(
    string apiKey, string passphrase, IKucoinSignatureService signatureService, Func<long> timeOffset)
    : DelegatingHandler
{
    private const string KeyVersion = "2";
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
        request.Headers.Add("KC-API-KEY-VERSION", KeyVersion);
    }
}
