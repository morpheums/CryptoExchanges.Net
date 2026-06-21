using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Kucoin.Auth;

/// <summary>
/// Extends <see cref="ISignatureService"/> with KuCoin passphrase-v2 signing (HMAC-SHA256 + base64
/// on the passphrase itself, required by <c>KC-API-PASSPHRASE</c> header in version-2 mode).
/// </summary>
internal interface IKucoinSignatureService : ISignatureService
{
    /// <summary>Signs <paramref name="passphrase"/> with HMAC-SHA256 + base64 for the <c>KC-API-PASSPHRASE</c> header.</summary>
    /// <param name="passphrase">The raw API passphrase. Must be non-null, non-empty, and non-whitespace.</param>
    /// <returns>The base64-encoded HMAC-SHA256 of the passphrase.</returns>
    /// <exception cref="ArgumentException"><paramref name="passphrase"/> is null, empty, or whitespace.</exception>
    string SignPassphrase(string passphrase);
}
