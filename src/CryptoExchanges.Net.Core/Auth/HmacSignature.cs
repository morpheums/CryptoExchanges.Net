using System.Security.Cryptography;
using System.Text;

namespace CryptoExchanges.Net.Core.Auth;

/// <summary>
/// Computes HMAC-SHA256 signatures shared across exchanges. This is the single HMAC primitive that
/// per-exchange signature services build on; they select an output <see cref="SignatureEncoding"/>
/// without re-implementing the hash.
/// </summary>
public static class HmacSignature
{
    /// <summary>
    /// Signs <paramref name="payload"/> with <paramref name="secret"/> using HMAC-SHA256 and renders
    /// the result in the requested <paramref name="encoding"/>. The <see cref="SignatureEncoding.Hex"/>
    /// output is byte-for-byte identical to Binance's and Bybit's lowercase-hex signatures for the same
    /// secret and payload.
    /// </summary>
    /// <param name="secret">The UTF-8 HMAC secret. Must be non-null, non-empty, and non-whitespace.</param>
    /// <param name="payload">The UTF-8 string to sign (e.g. a query string or canonical sign-string). Must be non-null, non-empty, and non-whitespace.</param>
    /// <param name="encoding">The signature output form.</param>
    /// <returns>The encoded HMAC-SHA256 signature.</returns>
    /// <exception cref="ArgumentException"><paramref name="secret"/> or <paramref name="payload"/> is null/empty/whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="encoding"/> is not a defined value.</exception>
    public static string Compute(string secret, string payload, SignatureEncoding encoding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(secretBytes, payloadBytes);

        return encoding switch
        {
            SignatureEncoding.Hex => Convert.ToHexStringLower(hash),
            SignatureEncoding.Base64 => Convert.ToBase64String(hash),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unknown signature encoding."),
        };
    }
}
