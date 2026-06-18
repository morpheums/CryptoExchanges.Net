namespace CryptoExchanges.Net.Core.Models;

/// <summary>Detailed trading rules for a single symbol.</summary>
public sealed record SymbolInfo(
    Symbol Symbol,
    IReadOnlyList<Enums.OrderType> AllowedOrderTypes,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    decimal? TickSize = null,
    decimal? MinQuantity = null,
    decimal? MaxQuantity = null,
    decimal? StepSize = null,
    decimal? MinNotional = null);
