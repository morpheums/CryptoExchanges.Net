using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Coinbase;

/// <summary>Coinbase Advanced Trade wire-symbol format: upper-case, hyphen-delimited (e.g. BTC-USD).</summary>
internal static class CoinbaseSymbolFormat
{
    /// <summary>The shared Coinbase <see cref="SymbolFormat"/>.</summary>
    public static readonly SymbolFormat Instance = new()
    {
        Delimiter = "-",
        Casing = SymbolCasing.Upper,
        FallbackQuoteAssets =
        [
            "USDT", "USD", "EUR", "GBP", "BTC", "ETH"
        ]
    };
}
