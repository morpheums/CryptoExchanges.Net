using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Kucoin;

/// <summary>KuCoin's wire-symbol format: upper-case, hyphen-delimited (e.g. BTC-USDT).</summary>
internal static class KucoinSymbolFormat
{
    /// <summary>The shared KuCoin <see cref="SymbolFormat"/>.</summary>
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
