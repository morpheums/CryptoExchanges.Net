using System.Globalization;
using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Kucoin.Auth;

/// <summary>
/// Creates HMAC-SHA256 signatures for KuCoin KC-API passphrase-v2 requests, rendered as base64
/// for the <c>KC-API-SIGN</c> header. Also signs the passphrase itself (KC-API passphrase-v2 scheme)
/// for the <c>KC-API-PASSPHRASE</c> header. Signs a prehash of
/// <c>timestamp + METHOD + requestPath + body</c>, identical in structure to OKX, but with a Unix
/// epoch <strong>millisecond</strong> timestamp (not ISO-8601) and an HMAC-signed passphrase.
/// </summary>
internal sealed class KucoinSignatureService(string secretKey) : IKucoinSignatureService
{
    private readonly string _secretKey = InitializeSecretKey(secretKey);

    /// <inheritdoc />
    public string Sign(string prehash) =>
        HmacSignature.Compute(_secretKey, prehash, SignatureEncoding.Base64);

    /// <inheritdoc />
    public string SignPassphrase(string passphrase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        return HmacSignature.Compute(_secretKey, passphrase, SignatureEncoding.Base64);
    }

    /// <summary>
    /// Builds the canonical KuCoin prehash string: <c>timestamp + METHOD + requestPath + body</c>.
    /// The format is identical to OKX except the timestamp must be Unix epoch milliseconds (see
    /// <see cref="FormatTimestamp"/>), not ISO-8601.
    /// </summary>
    /// <param name="timestamp">
    /// The request timestamp as Unix epoch <strong>milliseconds</strong> (string, e.g.
    /// <c>"1750000000000"</c>). Use <see cref="FormatTimestamp"/> to produce this form.
    /// </param>
    /// <param name="method">The HTTP verb; upper-cased before assembly (e.g. <c>GET</c>, <c>POST</c>, <c>DELETE</c>).</param>
    /// <param name="requestPath">
    /// The request path including the leading <c>/</c> and, for GET requests, the query string
    /// (e.g. <c>/api/v1/market/allTickers</c>).
    /// </param>
    /// <param name="body">The raw JSON body for POST requests, or an empty string when there is no body.</param>
    /// <returns>The canonical prehash string, with no signature appended.</returns>
    /// <exception cref="ArgumentException"><paramref name="timestamp"/>, <paramref name="method"/>, or <paramref name="requestPath"/> is null/empty/whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> is null.</exception>
    public static string BuildPrehash(string timestamp, string method, string requestPath, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timestamp);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestPath);
        ArgumentNullException.ThrowIfNull(body);
        return $"{timestamp}{method.ToUpperInvariant()}{requestPath}{body}";
    }

    /// <summary>
    /// Formats a <see cref="DateTimeOffset"/> as the Unix epoch milliseconds string KuCoin expects
    /// (e.g. <c>"1750000000000"</c>). This differs from OKX, which uses ISO-8601 UTC with
    /// milliseconds and a trailing <c>Z</c>.
    /// </summary>
    /// <param name="timestamp">The instant to format.</param>
    /// <returns>The Unix epoch milliseconds as a string.</returns>
    public static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

    private static string InitializeSecretKey(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        return secretKey;
    }
}
