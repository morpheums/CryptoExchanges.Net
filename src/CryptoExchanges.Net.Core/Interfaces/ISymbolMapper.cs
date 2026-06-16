using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Core.Interfaces;

/// <summary>
/// Translates between the venue-neutral <see cref="Symbol"/> and an exchange's
/// wire representation (e.g. Binance "BTCUSDT", Coinbase "BTC-USD"). Implemented
/// per exchange; Core has no knowledge of any venue's format.
/// </summary>
public interface ISymbolMapper
{
    /// <summary>Formats a <see cref="Symbol"/> as the exchange's wire string.</summary>
    string ToWire(Symbol symbol);

    /// <summary>Resolves an exchange wire string back into a <see cref="Symbol"/>.</summary>
    /// <exception cref="FormatException">The wire string cannot be resolved.</exception>
    Symbol FromWire(string wireSymbol);

    /// <summary>Builds a <see cref="Symbol"/> from explicit base/quote ticker strings.</summary>
    Symbol FromComponents(string baseAsset, string quoteAsset);
}
