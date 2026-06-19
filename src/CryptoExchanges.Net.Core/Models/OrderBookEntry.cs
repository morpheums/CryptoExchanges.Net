namespace CryptoExchanges.Net.Core.Models;

/// <summary>A single price level in an order book (bid or ask).</summary>
public readonly record struct OrderBookEntry(
    decimal Price,
    decimal Quantity);
