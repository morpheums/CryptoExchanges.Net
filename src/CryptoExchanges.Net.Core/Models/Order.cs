namespace CryptoExchanges.Net.Core.Models;

/// <summary>An order placed or tracked on the exchange.</summary>
public sealed record Order(
    Symbol Symbol,
    string OrderId,
    string? ClientOrderId = null,
    decimal Price = 0,
    decimal OriginalQuantity = 0,
    decimal ExecutedQuantity = 0,
    Enums.OrderSide Side = Enums.OrderSide.Buy,
    Enums.OrderType Type = Enums.OrderType.Limit,
    Enums.OrderStatus Status = Enums.OrderStatus.New,
    Enums.TimeInForce TimeInForce = Enums.TimeInForce.Gtc,
    decimal? StopPrice = null,
    decimal? IcebergQuantity = null,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? UpdatedAt = null)
{
    /// <summary>Cumulative quote asset quantity filled as of the last update.</summary>
    public decimal CumulativeQuoteQuantity { get; init; }
}
