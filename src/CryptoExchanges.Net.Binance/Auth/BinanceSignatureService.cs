using System.Security.Cryptography;
using System.Text;
using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Binance.Auth;

/// <summary>
/// Creates HMAC-SHA256 signatures for Binance API requests.
/// </summary>
internal sealed class BinanceSignatureService(string secretKey) : ISignatureService
{
    private readonly byte[] _secretKeyBytes = InitializeSecretKey(secretKey);

    /// <inheritdoc />
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
        var separator = string.IsNullOrEmpty(queryString) ? string.Empty : "&";
        return $"{queryString}{separator}signature={signature}";
    }

    private static byte[] InitializeSecretKey(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        return Encoding.UTF8.GetBytes(secretKey);
    }
}
