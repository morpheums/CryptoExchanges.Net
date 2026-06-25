using System.Net.Http.Headers;
using System.Text;
using System.Web;
using CryptoExchanges.Net.Kraken.Auth;

namespace CryptoExchanges.Net.Kraken.Resilience;

/// <summary>
/// For signed Kraken requests, (re)computes a fresh nonce + HMAC-SHA-512 signature ON EACH ATTEMPT
/// and sets the two credential headers: <c>API-Key</c> and <c>API-Sign</c>. Sits below the retry
/// strategy so a retried request is re-signed with a new nonce (Kraken rejects replayed nonces).
/// Unsigned (public) requests pass through untouched.
/// </summary>
/// <param name="apiKey">The Kraken API key set on the <c>API-Key</c> header for signed requests.</param>
/// <param name="signatureService">Computes the Base64 HMAC-SHA-512 signature over the Kraken prehash.</param>
internal sealed class KrakenSigningHandler(string apiKey, KrakenSignatureService signatureService)
    : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (KrakenSigningRequest.IsSigned(request))
            await ResignAsync(request, cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task ResignAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                "Kraken signed request requires an API key (API-Key); none was configured.");

        var path = request.RequestUri!.AbsolutePath;

        // Read and re-assemble the form body, replacing (or injecting) the nonce field.
        // A fresh nonce per attempt ensures Kraken never rejects a retried request.
        var existingBody = request.Content is not null
            ? await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
            : string.Empty;

        var nonce = KrakenSignatureService.MintNonce();
        var body = InjectNonce(existingBody, nonce);

        request.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

        var signature = signatureService.ComputeSignature(path, nonce, body);

        // Strip prior headers so a retried request re-signs cleanly with exactly one set.
        request.Headers.Remove("API-Key");
        request.Headers.Remove("API-Sign");
        request.Headers.Add("API-Key", apiKey);
        request.Headers.Add("API-Sign", signature);
    }

    /// <summary>
    /// Replaces an existing <c>nonce=</c> field in the URL-encoded body, or prepends one if absent.
    /// The nonce value in the body must match the nonce used to build the prehash exactly.
    /// </summary>
    private static string InjectNonce(string body, long nonce)
    {
        var nonceValue = nonce.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(body))
            return "nonce=" + nonceValue;

        // Parse and replace to handle existing nonce= from a prior attempt.
        var pairs = HttpUtility.ParseQueryString(body);
        pairs["nonce"] = nonceValue;

        // Rebuild the body from the parsed collection in deterministic order (nonce first).
        var sb = new StringBuilder();
        sb.Append("nonce=").Append(nonceValue);
        foreach (string key in pairs)
        {
            if (key == "nonce") continue;
            sb.Append('&').Append(Uri.EscapeDataString(key)).Append('=')
              .Append(Uri.EscapeDataString(pairs[key] ?? string.Empty));
        }
        return sb.ToString();
    }
}
