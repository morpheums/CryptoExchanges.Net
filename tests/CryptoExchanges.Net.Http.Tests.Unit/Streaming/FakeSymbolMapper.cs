using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Http.Tests.Unit.Streaming;

/// <summary>
/// Trivial <see cref="ISymbolMapper"/> for tests: formats <see cref="Symbol"/> as
/// <c>"WIRE:{symbol}"</c> so routing-key tests can predict the wire string without
/// exchange-specific logic.
/// </summary>
internal sealed class FakeSymbolMapper : ISymbolMapper
{
    /// <summary>Prefix prepended to every wire symbol to distinguish it from the canonical form.</summary>
    public const string WirePrefix = "WIRE:";

    /// <inheritdoc/>
    public string ToWire(Symbol symbol) => ToWireStatic(symbol);

    /// <summary>Static helper so test code can predict the wire symbol without constructing a mapper.</summary>
    public static string ToWireStatic(Symbol symbol) => $"{WirePrefix}{symbol}";

    /// <inheritdoc/>
    public Symbol FromWire(string wireSymbol)
        => throw new NotImplementedException("not needed in tests");

    /// <inheritdoc/>
    public Symbol FromComponents(string baseAsset, string quoteAsset)
        => throw new NotImplementedException("not needed in tests");

    /// <inheritdoc/>
    public void UpdateSymbols(IEnumerable<SymbolInfo> symbols) { }
}
