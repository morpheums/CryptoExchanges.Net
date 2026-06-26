using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Kraken;

/// <summary>Kraken's wire-symbol (wsname) format: upper-case, slash-delimited (e.g. XBT/USDT).</summary>
internal static class KrakenSymbolFormat
{
    /// <summary>The shared Kraken <see cref="SymbolFormat"/>.</summary>
    public static readonly SymbolFormat Instance = new()
    {
        Delimiter = "/",
        Casing = SymbolCasing.Upper,
        // Kraken uses its own legacy tickers: canonical BTC→XBT, DOGE→XDG on the wire.
        AssetAliases = new Dictionary<string, string>
        {
            ["BTC"]  = "XBT",
            ["DOGE"] = "XDG"
        },
        FallbackQuoteAssets =
        [
            "USDT", "USD", "EUR", "GBP", "BTC", "XBT", "ETH"
        ]
    };
}
