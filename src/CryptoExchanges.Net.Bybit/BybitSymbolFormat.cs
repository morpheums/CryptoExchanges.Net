using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Bybit;

/// <summary>Bybit's wire-symbol format: upper-case concatenation (e.g. BTCUSDT), no delimiter.</summary>
internal static class BybitSymbolFormat
{
    /// <summary>The shared Bybit <see cref="SymbolFormat"/>.</summary>
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
