using System.Security.Cryptography;
using System.Text;

namespace CryptoExchanges.Net.Kraken.Auth;

/// <summary>
/// Computes Kraken's HMAC-SHA512 REST signature. The algorithm differs from the SHA-256/UTF-8
/// shared primitive and must be implemented inline with BCL cryptography:
/// <list type="number">
///   <item>sha256_hash = SHA-256( UTF-8(nonce + body) )</item>
///   <item>prehash = UTF-8(path) ‖ sha256_hash  (byte concatenation)</item>
///   <item>API-Sign = Base64( HMAC-SHA-512( Base64Decode(apiSecret), prehash ) )</item>
/// </list>
/// The API secret is Base64-encoded (not a raw UTF-8 string) — the secret bytes come from
/// <see cref="Convert.FromBase64String"/>, NOT <c>Encoding.UTF8.GetBytes</c>.
/// </summary>
internal sealed class KrakenSignatureService
{
    private readonly byte[] _secretBytes;

    /// <param name="apiSecret">The Kraken API secret (Base64-encoded). Must be non-null, non-empty, and valid Base64.</param>
    /// <exception cref="ArgumentException"><paramref name="apiSecret"/> is null or whitespace.</exception>
    /// <exception cref="FormatException"><paramref name="apiSecret"/> is not valid Base64.</exception>
    internal KrakenSignatureService(string apiSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiSecret);
        _secretBytes = Convert.FromBase64String(apiSecret);
    }

    /// <summary>
    /// Computes <c>API-Sign</c> for a private Kraken endpoint call.
    /// </summary>
    /// <param name="path">The request path (e.g. <c>/0/private/AddOrder</c>).</param>
    /// <param name="nonce">The nonce value — must equal the <c>nonce=</c> field already embedded in <paramref name="body"/>.</param>
    /// <param name="body">The full URL-encoded POST body including <c>nonce=&lt;value&gt;</c>.</param>
    /// <returns>The Base64-encoded HMAC-SHA-512 signature for the <c>API-Sign</c> header.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="body"/> is null.</exception>
    internal string ComputeSignature(string path, long nonce, string body)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(body);

        // sha256_hash = SHA-256( UTF-8(nonce + body) )
        var message = nonce.ToString(System.Globalization.CultureInfo.InvariantCulture) + body;
        var sha256Hash = SHA256.HashData(Encoding.UTF8.GetBytes(message));

        // prehash = UTF-8(path) ‖ sha256_hash  (byte concat)
        var pathBytes = Encoding.UTF8.GetBytes(path);
        var prehash = new byte[pathBytes.Length + sha256Hash.Length];
        pathBytes.CopyTo(prehash, 0);
        sha256Hash.CopyTo(prehash, pathBytes.Length);

        // API-Sign = Base64( HMAC-SHA-512( base64-decoded-secret, prehash ) )
        var hmac = HMACSHA512.HashData(_secretBytes, prehash);
        return Convert.ToBase64String(hmac);
    }

    /// <summary>
    /// Mints a strictly-increasing nonce. Each value is <c>max(lastNonce + 1, utcMillis)</c>, so it
    /// tracks the UTC millisecond clock yet never repeats or regresses — even on a cold start or when
    /// many calls land within the same millisecond. A compare-and-swap loop guarantees monotonicity
    /// under concurrent callers (the published value is exactly what this caller observed and reserved).
    /// </summary>
    internal static long MintNonce()
    {
        while (true)
        {
            var last = Interlocked.Read(ref s_lastNonce);
            var candidate = Math.Max(last + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (Interlocked.CompareExchange(ref s_lastNonce, candidate, last) == last)
                return candidate;
        }
    }

    private static long s_lastNonce;
}
