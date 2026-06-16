using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Binance;

/// <summary>
/// Maps <see cref="Symbol"/>s to/from Binance's concatenated wire format (e.g. "BTCUSDT").
/// <see cref="FromWire"/> resolves exactly against a table populated from exchangeInfo
/// (see <see cref="Update"/>); a small known-quote suffix list is the cold-cache fallback.
/// Thread-safe: the lookup table is swapped atomically.
/// </summary>
public sealed class BinanceSymbolMapper : ISymbolMapper
{
    private static readonly string[] FallbackQuotes =
    [
        "FDUSD", "USDT", "USDC", "TUSD", "BUSD", "DAI",
        "USD", "EUR", "GBP", "TRY", "BTC", "ETH", "BNB"
    ];

    private volatile Dictionary<string, Symbol> _wireToSymbol =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public string ToWire(Symbol symbol) => symbol.Base.Ticker + symbol.Quote.Ticker;

    /// <inheritdoc />
    public Symbol FromComponents(string baseAsset, string quoteAsset)
        => new(Asset.Of(baseAsset), Asset.Of(quoteAsset));

    /// <summary>Refreshes the wire-&gt;Symbol lookup table from exchangeInfo symbols.</summary>
    /// <param name="symbols">The exchangeInfo symbols to index.</param>
    public void Update(IEnumerable<SymbolInfo> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        var map = new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in symbols)
            map[ToWire(info.Symbol)] = info.Symbol;
        _wireToSymbol = map;
    }

    /// <inheritdoc />
    public Symbol FromWire(string wireSymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wireSymbol);

        if (_wireToSymbol.TryGetValue(wireSymbol, out var hit))
            return hit;

        foreach (var quote in FallbackQuotes)
        {
            if (wireSymbol.Length <= quote.Length)
                continue;
            if (!wireSymbol.EndsWith(quote, StringComparison.OrdinalIgnoreCase))
                continue;

            var baseTicker = wireSymbol[..^quote.Length];
            if (Asset.TryOf(baseTicker, out var baseAsset) && Asset.TryOf(quote, out var quoteAsset))
                return new Symbol(baseAsset, quoteAsset);
        }

        throw new FormatException(
            $"Cannot resolve Binance symbol '{wireSymbol}' (not in exchangeInfo and no known quote suffix matched).");
    }
}
