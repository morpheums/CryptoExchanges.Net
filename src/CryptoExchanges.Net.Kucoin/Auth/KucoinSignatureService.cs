using System.Globalization;
using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Kucoin.Auth;

/// <summary>
/// Computes KuCoin KC-API passphrase-v2 HMAC-SHA256 signatures (base64) over the prehash
/// <c>timestamp + METHOD + requestPath + body</c> and signs the passphrase separately.
/// Timestamp is Unix epoch <strong>milliseconds</strong> (not ISO-8601).
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

    /// <summary>Builds the canonical KuCoin prehash string: <c>timestamp + METHOD + requestPath + body</c>.</summary>
    /// <param name="timestamp">Unix epoch milliseconds string (e.g. <c>"1750000000000"</c>).</param>
    /// <param name="method">HTTP verb, upper-cased (e.g. <c>GET</c>, <c>POST</c>).</param>
    /// <param name="requestPath">Path including leading <c>/</c> and any query string.</param>
    /// <param name="body">Raw JSON body, or empty string when there is no body.</param>
    /// <returns>The canonical prehash string.</returns>
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

    /// <summary>Formats a <see cref="DateTimeOffset"/> as the Unix epoch milliseconds string KuCoin expects.</summary>
    /// <param name="timestamp">The instant to format.</param>
    /// <returns>Unix epoch milliseconds as a decimal string.</returns>
    public static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

    private static string InitializeSecretKey(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        return secretKey;
    }
}
