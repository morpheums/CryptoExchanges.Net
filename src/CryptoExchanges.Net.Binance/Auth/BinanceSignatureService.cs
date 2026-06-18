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

    private static byte[] InitializeSecretKey(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        return Encoding.UTF8.GetBytes(secretKey);
    }
}
