using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Exceptions;

namespace CryptoExchanges.Net.Kucoin.Internal;

/// <summary>
/// Bespoke KuCoin <see cref="ISymbolMapper"/> adapter. Delegates all resolution work to the
/// shared <see cref="SymbolMapper"/> (KuCoin's hyphen-delimited, upper-case wire format is handled
/// by <see cref="KucoinSymbolFormat"/>). Exposes <see cref="IsSupported"/> to reflect whether a
/// symbol is in the registered spot symbol table, and re-wraps cold-cache resolution failures as
/// <see cref="ExchangeApiException"/> for consistent caller error handling.
/// </summary>
internal sealed class KucoinSymbolMapper : ISymbolMapper
{
    private readonly SymbolMapper _inner;

    /// <summary>Creates a mapper backed by the KuCoin wire symbol format.</summary>
    public KucoinSymbolMapper()
    {
        _inner = new SymbolMapper(KucoinSymbolFormat.Instance);
    }

    /// <summary>
    /// Formats a <see cref="Symbol"/> as the KuCoin wire string (e.g. <c>BTC-USDT</c>).
    /// Returns the formatted wire string; never throws (any well-formed symbol can be encoded).
    /// </summary>
    public string ToWire(Symbol symbol) => _inner.ToWire(symbol);

    /// <inheritdoc />
    public Symbol FromWire(string wireSymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wireSymbol);
        try
        {
            return _inner.FromWire(wireSymbol);
        }
        catch (FormatException ex)
        {
            throw new ExchangeApiException(
                $"KuCoin wire symbol '{wireSymbol}' could not be resolved. Ensure UpdateSymbols has been called.",
                innerException: ex);
        }
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
