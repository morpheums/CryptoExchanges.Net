using System.Globalization;
using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Bitget.Auth;

/// <summary>
/// Creates HMAC-SHA256 signatures for Bitget API requests, rendered as base64 for the
/// <c>ACCESS-SIGN</c> header. Signs a prehash of
/// <c>timestamp + METHOD + requestPath + ('?' + queryString when present) + body</c>.
/// </summary>
internal sealed class BitgetSignatureService(string secretKey) : ISignatureService
{
    private readonly string _secretKey = InitializeSecretKey(secretKey);

    /// <inheritdoc />
    public string Sign(string payload) =>
        HmacSignature.Compute(_secretKey, payload, SignatureEncoding.Base64);

    /// <summary>
    /// Builds the canonical Bitget prehash:
    /// <c>timestamp + METHOD + requestPath + ('?' + queryString when non-empty) + body</c>.
    /// Unlike OKX, the query string is appended to the path with a literal <c>?</c> only when present;
    /// <paramref name="body"/> is empty for GET/DELETE.
    /// </summary>
    public static string BuildPrehash(string timestamp, string method, string requestPath, string queryString, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timestamp);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestPath);
        ArgumentNullException.ThrowIfNull(queryString);
        ArgumentNullException.ThrowIfNull(body);

        var query = queryString.Length > 0 ? $"?{queryString}" : string.Empty;
        return $"{timestamp}{method.ToUpperInvariant()}{requestPath}{query}{body}";
    }

    /// <summary>Formats an instant as the epoch-millisecond timestamp string Bitget expects.</summary>
    public static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

    private static string InitializeSecretKey(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        return secretKey;
    }
}
