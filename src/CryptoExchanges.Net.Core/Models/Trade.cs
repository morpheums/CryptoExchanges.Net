namespace CryptoExchanges.Net.Core.Models;

/// <summary>A public trade executed on the exchange.</summary>
public sealed record Trade(
    Symbol Symbol,
    string? Id = null,
    decimal Price = 0,
    decimal Quantity = 0,
    DateTimeOffset? Timestamp = null,
    bool IsBuyerMaker = false,
    string? OrderId = null);
