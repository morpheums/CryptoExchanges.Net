using System.Security.Cryptography;
using System.Text;

namespace CryptoExchanges.Net.Binance.Auth;

/// <summary>
/// Creates HMAC-SHA256 signatures for Binance API requests.
/// </summary>
public sealed class BinanceSignatureService(string secretKey)
{
    private readonly byte[] _secretKeyBytes = InitializeSecretKey(secretKey);

    /// <summary>
    /// Signs a query string using HMAC-SHA256 and returns the hex-encoded signature.
    /// </summary>
    /// <param name="queryString">The query string to sign (without leading '?').</param>
    /// <returns>The hex-encoded HMAC-SHA256 signature.</returns>
    public string Sign(string queryString)
    {
        var queryBytes = Encoding.UTF8.GetBytes(queryString);
        var hash = HMACSHA256.HashData(_secretKeyBytes, queryBytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Builds a query string with the signature appended.
    /// </summary>
    /// <param name="queryString">The unsigned query string (without leading '?').</param>
    /// <returns>The query string with signature parameter appended.</returns>
    public string BuildSignedQuery(string queryString)
    {
        var signature = Sign(queryString);
        return $"{queryString}&signature={signature}";
    }

    private static byte[] InitializeSecretKey(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        return Encoding.UTF8.GetBytes(secretKey);
    }
}
