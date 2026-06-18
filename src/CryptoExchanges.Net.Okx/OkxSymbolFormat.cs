using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Okx;

/// <summary>OKX's wire-symbol (instrument-id) format: upper-case, hyphen-delimited (e.g. BTC-USDT).</summary>
internal static class OkxSymbolFormat
{
    /// <summary>The shared OKX <see cref="SymbolFormat"/>.</summary>
    public static readonly SymbolFormat Instance = new()
    {
        Delimiter = "-",
        Casing = SymbolCasing.Upper,
        FallbackQuoteAssets =
        [
            "USDT", "USDC", "USDE", "DAI",
            "USD", "EUR", "BTC", "ETH"
        ]
    };
}
