using CryptoExchanges.Net.Core;

namespace CryptoExchanges.Net.Kucoin.Internal;

/// <summary>
/// Bespoke KuCoin <see cref="ISymbolMapper"/> adapter. Delegates all resolution work to the
/// shared <see cref="SymbolMapper"/> (KuCoin's hyphen-delimited, upper-case wire format is handled
/// by <see cref="KucoinSymbolFormat"/>). Exposes <see cref="IsSupported"/> to reflect whether a
/// symbol is in the registered spot symbol table.
/// </summary>
/// <remarks>
/// <see cref="FromWire"/> propagates the inner <see cref="FormatException"/> directly, matching the
/// <see cref="ISymbolMapper"/> XML-doc contract and the pattern established by the shared
/// <see cref="SymbolMapper"/> and the other exchange implementations (OKX, Binance, Bybit).
/// </remarks>
internal sealed class KucoinSymbolMapper : ISymbolMapper
{
    private readonly SymbolMapper _inner;

    /// <summary>Creates a mapper backed by the KuCoin wire symbol format.</summary>
    public KucoinSymbolMapper()
    {
        _inner = new SymbolMapper(KucoinSymbolFormat.Instance);
    }

    /// <inheritdoc />
    public string ToWire(Symbol symbol) => _inner.ToWire(symbol);

    /// <inheritdoc />
    /// <exception cref="System.FormatException">The wire string cannot be resolved to a known symbol.</exception>
    public Symbol FromWire(string wireSymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wireSymbol);
        // Propagate FormatException directly — matches ISymbolMapper contract and sibling exchanges.
        return _inner.FromWire(wireSymbol);
    }

    /// <inheritdoc />
    public Symbol FromComponents(string baseAsset, string quoteAsset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(quoteAsset);
        return _inner.FromComponents(baseAsset, quoteAsset);
    }

    /// <inheritdoc />
    public void UpdateSymbols(IEnumerable<SymbolInfo> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        _inner.UpdateSymbols(symbols);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="symbol"/> is in the registered spot
    /// symbol table (i.e., <see cref="UpdateSymbols"/> has been called and the symbol was present),
    /// or can be resolved via the cold-cache delimiter fallback.
    /// Returns <see langword="false"/> when the symbol cannot be resolved at all.
    /// </summary>
    public bool IsSupported(Symbol symbol)
    {
        var wire = _inner.ToWire(symbol);
        try
        {
            var resolved = _inner.FromWire(wire);
            return resolved == symbol;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
