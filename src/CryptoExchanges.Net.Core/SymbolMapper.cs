using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Core;

/// <summary>
/// Generic, config-driven <see cref="ISymbolMapper"/>. One implementation serves every exchange,
/// parameterized by a <see cref="SymbolFormat"/>. <see cref="FromWire"/> resolves primarily against
/// a wire→Symbol table warmed by <see cref="UpdateSymbols"/>, with a config-driven cold-cache fallback.
/// Thread-safe: the lookup table is swapped atomically.
/// </summary>
public sealed class SymbolMapper : ISymbolMapper
{
    private readonly SymbolFormat _format;
    // Defensive copies of the format's collections: SymbolFormat exposes them as IReadOnly* views that
    // may be backed by a mutable instance the caller still holds. Copying at construction keeps the
    // documented thread-safe contract — post-construction mutation of the source can't affect mapping.
    private readonly Dictionary<string, string> _canonicalToWire;
    private readonly Dictionary<string, string> _wireToCanonical;
    private readonly string[] _fallbackQuoteAssets;
    private volatile Dictionary<string, Symbol> _wireToSymbol = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a mapper for the given exchange wire format.</summary>
    public SymbolMapper(SymbolFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        _format = format;
        _canonicalToWire = new(StringComparer.OrdinalIgnoreCase);
        _wireToCanonical = new(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in format.AssetAliases)
        {
            _canonicalToWire[kvp.Key] = kvp.Value;
            if (!_wireToCanonical.TryAdd(kvp.Value, kvp.Key))
                throw new ArgumentException(
                    $"Ambiguous alias: wire ticker '{kvp.Value}' is mapped from multiple canonical tickers.",
                    nameof(format));
        }
        _fallbackQuoteAssets = [.. format.FallbackQuoteAssets];
    }

    /// <inheritdoc />
    public string ToWire(Symbol symbol)
    {
        var pair = Alias(symbol.Base.Ticker) + _format.Delimiter + Alias(symbol.Quote.Ticker);
#pragma warning disable CA1308 // Intentional: producing a lowercase wire string per SymbolCasing.Lower, not normalizing for comparison.
        var cased = _format.Casing == SymbolCasing.Lower ? pair.ToLowerInvariant() : pair.ToUpperInvariant();
#pragma warning restore CA1308
        return _format.Prefix + cased;
    }

    /// <inheritdoc />
    public Symbol FromComponents(string baseAsset, string quoteAsset)
        => new(Asset.Of(Unalias(baseAsset)), Asset.Of(Unalias(quoteAsset)));

    /// <inheritdoc />
    public void UpdateSymbols(IEnumerable<SymbolInfo> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        var map = new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in symbols)
            map[ToWire(info.Symbol)] = info.Symbol;
        _wireToSymbol = map;
    }

    /// <summary>
    /// Resolves an exchange wire string back into a <see cref="Symbol"/>. The warm table populated by
    /// <see cref="UpdateSymbols"/> is the authoritative path. The cold-cache fallback uses the FIRST
    /// delimiter occurrence (for delimited formats) or a known-quote suffix match against
    /// <see cref="SymbolFormat.FallbackQuoteAssets"/> (for delimiter-less formats); delimiter-less formats
    /// with aliases (e.g. Kraken) require a warm table for <c>FromWire</c> to resolve correctly.
    /// </summary>
    /// <exception cref="FormatException">The wire string cannot be resolved.</exception>
    public Symbol FromWire(string wireSymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wireSymbol);

        if (_wireToSymbol.TryGetValue(wireSymbol, out var hit))
            return hit;

        var body = wireSymbol;
        if (_format.Prefix.Length > 0 && body.StartsWith(_format.Prefix, StringComparison.Ordinal))
            body = body[_format.Prefix.Length..];

        if (_format.Delimiter.Length > 0)
        {
            var idx = body.IndexOf(_format.Delimiter, StringComparison.Ordinal);
            if (idx > 0 && idx < body.Length - _format.Delimiter.Length)
            {
                var b = body[..idx];
                var q = body[(idx + _format.Delimiter.Length)..];
                if (Asset.TryOf(Unalias(b), out var ba) && Asset.TryOf(Unalias(q), out var qa))
                    return new Symbol(ba, qa);
            }
        }
        else
        {
            foreach (var quote in _fallbackQuoteAssets)
            {
                if (body.Length <= quote.Length) continue;
                if (!body.EndsWith(quote, StringComparison.OrdinalIgnoreCase)) continue;
                var baseTicker = body[..^quote.Length];
                if (Asset.TryOf(Unalias(baseTicker), out var ba) && Asset.TryOf(Unalias(quote), out var qa))
                    return new Symbol(ba, qa);
            }
        }

        throw new FormatException(
            $"Cannot resolve wire symbol '{wireSymbol}' (not in the symbol table and no format rule matched).");
    }

    private string Alias(string canonical)
        => _canonicalToWire.TryGetValue(canonical, out var wire) ? wire : canonical;

    private string Unalias(string wire)
        => _wireToCanonical.TryGetValue(wire, out var canonical) ? canonical : wire;
}
