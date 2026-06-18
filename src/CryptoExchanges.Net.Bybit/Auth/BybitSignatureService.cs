using System.Security.Cryptography;
using System.Text;

namespace CryptoExchanges.Net.Bybit.Auth;

/// <summary>
/// Creates HMAC-SHA256 signatures for Bybit API requests.
/// </summary>
/// <remarks>
/// Unlike Binance, Bybit does not append the signature to the query string. The signature is
/// returned to the caller so the request handler can place it in the <c>X-BAPI-SIGN</c> header.
/// </remarks>
internal sealed class BybitSignatureService(string secretKey)
{
    private readonly byte[] _secretKeyBytes = InitializeSecretKey(secretKey);

    /// <summary>
    /// Signs a Bybit sign-string using HMAC-SHA256 and returns the hex-encoded signature.
    /// </summary>
    /// <param name="signString">The canonical sign-string to sign.</param>
    /// <returns>The hex-encoded HMAC-SHA256 signature.</returns>
    public string Sign(string signString)
    {
        var signBytes = Encoding.UTF8.GetBytes(signString);
        var hash = HMACSHA256.HashData(_secretKeyBytes, signBytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Builds the canonical sign-string for a Bybit GET request: <c>timestamp + apiKey + recvWindow + queryString</c>.
    /// </summary>
    /// <param name="timestamp">The request timestamp in milliseconds.</param>
    /// <param name="apiKey">The Bybit API key.</param>
    /// <param name="recvWindow">The receive window in milliseconds.</param>
    /// <param name="queryString">The query string (without leading '?').</param>
    /// <returns>The canonical GET sign-string, with no signature appended.</returns>
    public static string BuildGetSignString(string timestamp, string apiKey, string recvWindow, string queryString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timestamp);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(recvWindow);
        ArgumentNullException.ThrowIfNull(queryString);
        return $"{timestamp}{apiKey}{recvWindow}{queryString}";
    }

    /// <summary>
    /// Builds the canonical sign-string for a Bybit POST request: <c>timestamp + apiKey + recvWindow + jsonBody</c>.
    /// </summary>
    /// <param name="timestamp">The request timestamp in milliseconds.</param>
    /// <param name="apiKey">The Bybit API key.</param>
    /// <param name="recvWindow">The receive window in milliseconds.</param>
    /// <param name="jsonBody">The raw JSON request body.</param>
    /// <returns>The canonical POST sign-string, with no signature appended.</returns>
    public static string BuildPostSignString(string timestamp, string apiKey, string recvWindow, string jsonBody)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timestamp);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(recvWindow);
        ArgumentNullException.ThrowIfNull(jsonBody);
        return $"{timestamp}{apiKey}{recvWindow}{jsonBody}";
    }

    private static byte[] InitializeSecretKey(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        return Encoding.UTF8.GetBytes(secretKey);
    }
}
