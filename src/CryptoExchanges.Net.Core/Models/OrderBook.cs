namespace CryptoExchanges.Net.Core.Models;

/// <summary>Snapshot of the current order book for a trading pair.</summary>
public sealed record OrderBook(
    Symbol Symbol,
    IReadOnlyList<OrderBookEntry> Bids,
    IReadOnlyList<OrderBookEntry> Asks,
    DateTimeOffset? Timestamp = null,
    long? LastUpdateId = null);
