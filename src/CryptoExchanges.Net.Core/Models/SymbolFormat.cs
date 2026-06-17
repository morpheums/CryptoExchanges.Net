using System.Collections.ObjectModel;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Core.Models;

/// <summary>
/// Describes how an exchange formats wire symbols, driving the generic <see cref="CryptoExchanges.Net.Core.SymbolMapper"/>.
/// </summary>
public sealed record SymbolFormat
{
    /// <summary>Delimiter between base and quote (e.g. "" for Binance, "-" for Coinbase, "_" for Gate).</summary>
    public string Delimiter { get; init; } = "";

    /// <summary>Casing applied to the wire symbol.</summary>
    public SymbolCasing Casing { get; init; } = SymbolCasing.Upper;

    /// <summary>Optional fixed prefix (e.g. Bitfinex "t").</summary>
    public string Prefix { get; init; } = "";

    /// <summary>Canonical-ticker → wire-ticker aliases (e.g. Kraken BTC→XBT). Reverse is computed.</summary>
    public IReadOnlyDictionary<string, string> AssetAliases { get; init; } =
        ReadOnlyDictionary<string, string>.Empty;

    /// <summary>Known quote tickers used to split a delimiter-less wire symbol when the table misses
    /// (cold-cache fallback). Ignored when <see cref="Delimiter"/> is non-empty.</summary>
    public IReadOnlyList<string> FallbackQuoteAssets { get; init; } = [];
}
