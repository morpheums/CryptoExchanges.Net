using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Binance;

/// <summary>Binance's wire-symbol format: upper-case concatenation (e.g. BTCUSDT), no delimiter.</summary>
internal static class BinanceSymbolFormat
{
    /// <summary>The shared Binance <see cref="SymbolFormat"/>.</summary>
    public static readonly SymbolFormat Instance = new()
    {
        Delimiter = "",
        Casing = SymbolCasing.Upper,
        FallbackQuoteAssets =
        [
            "FDUSD", "USDT", "USDC", "TUSD", "BUSD", "DAI",
            "USD", "EUR", "GBP", "TRY", "BTC", "ETH", "BNB"
        ]
    };
}
