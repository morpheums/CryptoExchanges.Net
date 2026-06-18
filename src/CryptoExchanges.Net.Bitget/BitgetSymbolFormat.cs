using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Bitget;

/// <summary>Bitget's wire-symbol format: upper-case concatenation (e.g. BTCUSDT), no delimiter.</summary>
internal static class BitgetSymbolFormat
{
    /// <summary>The shared Bitget <see cref="SymbolFormat"/>.</summary>
    public static readonly SymbolFormat Instance = new()
    {
        Delimiter = "",
        Casing = SymbolCasing.Upper,
        FallbackQuoteAssets =
        [
            "USDT", "USDC", "USDE", "DAI",
            "USD", "EUR", "BTC", "ETH"
        ]
    };
}
