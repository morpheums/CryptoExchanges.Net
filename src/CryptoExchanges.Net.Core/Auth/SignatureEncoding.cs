namespace CryptoExchanges.Net.Core.Auth;

/// <summary>
/// The output form for an HMAC-SHA256 signature. Exchanges differ only in how the same hash is
/// rendered: Binance and Bybit use lowercase hex; OKX (and Bitget) use base64.
/// </summary>
public enum SignatureEncoding
{
    /// <summary>Lowercase hexadecimal (<see cref="Convert.ToHexStringLower(byte[])"/>).</summary>
    Hex,

    /// <summary>Standard base64 (<see cref="Convert.ToBase64String(byte[])"/>).</summary>
    Base64,
}
