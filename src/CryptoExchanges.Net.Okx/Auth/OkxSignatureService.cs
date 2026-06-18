using System.Globalization;
using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Okx.Auth;

/// <summary>
/// Creates HMAC-SHA256 signatures for OKX API requests, rendered as base64.
/// </summary>
/// <remarks>
/// OKX signs a prehash string of the form <c>timestamp + METHOD + requestPath + body</c> and places
/// the resulting base64 signature in the <c>OK-ACCESS-SIGN</c> header. Unlike Binance/Bybit (lowercase
/// hex), OKX renders the same HMAC-SHA256 hash as standard base64. The signature is returned to the
/// caller rather than appended to the payload; the OKX signing handler (added in a later task) is
/// responsible for supplying the timestamp and writing the header.
/// </remarks>
internal sealed class OkxSignatureService(string secretKey)
{
    private readonly string _secretKey = InitializeSecretKey(secretKey);

    /// <summary>
    /// Signs an OKX prehash string using HMAC-SHA256 and returns the base64-encoded signature.
    /// </summary>
    /// <param name="prehash">The canonical OKX prehash string (see <see cref="BuildPrehash"/>).</param>
    /// <returns>The base64-encoded HMAC-SHA256 signature for the <c>OK-ACCESS-SIGN</c> header.</returns>
    public string Sign(string prehash) =>
        HmacSignature.Compute(_secretKey, prehash, SignatureEncoding.Base64);

    /// <summary>
    /// Builds the canonical OKX prehash string: <c>timestamp + METHOD + requestPath + body</c>.
    /// </summary>
    /// <param name="timestamp">
    /// The request timestamp in ISO-8601 UTC with milliseconds and a trailing <c>Z</c>
    /// (e.g. <c>2026-06-17T12:00:00.000Z</c>). Use <see cref="FormatTimestamp"/> to produce this form.
    /// </param>
    /// <param name="method">The HTTP verb; upper-cased before assembly (e.g. <c>GET</c>, <c>POST</c>, <c>DELETE</c>).</param>
    /// <param name="requestPath">
    /// The request path including the leading <c>/</c> and, for GET requests, the query string
    /// (e.g. <c>/api/v5/market/tickers?instType=SPOT</c>).
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
    /// Formats a <see cref="DateTimeOffset"/> as the ISO-8601 UTC timestamp OKX expects: milliseconds
    /// precision with a trailing <c>Z</c> (e.g. <c>2026-06-17T12:00:00.000Z</c>).
    /// </summary>
    /// <param name="timestamp">The instant to format.</param>
    /// <returns>The ISO-8601 UTC timestamp string.</returns>
    public static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static string InitializeSecretKey(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        return secretKey;
    }
}
