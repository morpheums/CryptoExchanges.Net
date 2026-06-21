using CryptoExchanges.Net.Core;

namespace CryptoExchanges.Net.Kucoin.Internal;

/// <summary>
/// Bespoke KuCoin <see cref="ISymbolMapper"/> adapter backed by <see cref="KucoinSymbolFormat"/>
/// (hyphen-delimited, upper-case). Delegates to <see cref="SymbolMapper"/>; exposes <see cref="IsSupported"/>.
/// </summary>
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
    /// Returns <see langword="true"/> when <paramref name="symbol"/> can be resolved via the registered table
    /// or the cold-cache delimiter fallback; <see langword="false"/> otherwise.
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
