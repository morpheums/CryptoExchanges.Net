using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Kucoin.Auth;

/// <summary>
/// Extends <see cref="ISignatureService"/> with the KuCoin passphrase-v2 signing capability.
/// Implementations compute HMAC-SHA256 signatures rendered as base64, and additionally sign the
/// API passphrase itself (as required by the <c>KC-API-PASSPHRASE</c> header in version-2 mode).
/// </summary>
internal interface IKucoinSignatureService : ISignatureService
{
    /// <summary>
    /// Signs <paramref name="passphrase"/> with HMAC-SHA256 using the secret key and returns the
    /// result base64-encoded, as required for the <c>KC-API-PASSPHRASE</c> header in the KuCoin
    /// KC-API passphrase-v2 authentication scheme.
    /// </summary>
    /// <param name="passphrase">The raw API passphrase. Must be non-null, non-empty, and non-whitespace.</param>
    /// <returns>The base64-encoded HMAC-SHA256 of the passphrase.</returns>
    /// <exception cref="ArgumentException"><paramref name="passphrase"/> is null, empty, or whitespace.</exception>
    string SignPassphrase(string passphrase);
}
