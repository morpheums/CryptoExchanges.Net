using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Core.Interfaces;

/// <summary>
/// Represents a request to place a new order.
/// Supports market, limit, and stop orders with optional iceberg quantities.
/// </summary>
public sealed record PlaceOrderRequest
{
    /// <summary>The trading pair symbol. Required.</summary>
    public required Symbol Symbol { get; init; }

    /// <summary>Buy or Sell. Required.</summary>
    public required OrderSide Side { get; init; }

    /// <summary>Order execution type. Required.</summary>
    public required OrderType Type { get; init; }

    /// <summary>Base asset quantity.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>Quote asset order quantity (used for market orders).</summary>
    public decimal? QuoteOrderQuantity { get; init; }

    /// <summary>Limit price. Required for limit orders.</summary>
    public decimal? Price { get; init; }

    /// <summary>Stop price. Required for stop-limit and stop-loss orders.</summary>
    public decimal? StopPrice { get; init; }

    /// <summary>How long the order remains active.</summary>
    public TimeInForce? TimeInForce { get; init; }

    /// <summary>Client-supplied order ID for idempotency.</summary>
    public string? ClientOrderId { get; init; }

    /// <summary>Visible quantity for iceberg orders.</summary>
    public decimal? IcebergQuantity { get; init; }

    /// <summary>
    /// Creates a new <see cref="PlaceOrderRequest"/> with the specified parameters,
    /// validates it, and returns the request. This is the intended creation path.
    /// </summary>
    /// <param name="symbol">The trading pair symbol. Required.</param>
    /// <param name="side">Buy or Sell. Required.</param>
    /// <param name="type">Order execution type. Required.</param>
    /// <param name="quantity">Base asset quantity.</param>
    /// <param name="quoteOrderQuantity">Quote asset order quantity (for market orders).</param>
    /// <param name="price">Limit price. Required for limit orders.</param>
    /// <param name="stopPrice">Stop price. Required for stop-limit and stop-loss orders.</param>
    /// <param name="timeInForce">How long the order remains active.</param>
    /// <param name="clientOrderId">Client-supplied order ID for idempotency.</param>
    /// <param name="icebergQuantity">Visible quantity for iceberg orders.</param>
    /// <returns>A validated <see cref="PlaceOrderRequest"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static PlaceOrderRequest Create(
        Symbol symbol,
        OrderSide side,
        OrderType type,
        decimal? quantity = null,
        decimal? quoteOrderQuantity = null,
        decimal? price = null,
        decimal? stopPrice = null,
        TimeInForce? timeInForce = null,
        string? clientOrderId = null,
        decimal? icebergQuantity = null)
    {
        var request = new PlaceOrderRequest
        {
            Symbol = symbol,
            Side = side,
            Type = type,
            Quantity = quantity,
            QuoteOrderQuantity = quoteOrderQuantity,
            Price = price,
            StopPrice = stopPrice,
            TimeInForce = timeInForce,
            ClientOrderId = clientOrderId,
            IcebergQuantity = icebergQuantity
        };
        request.Validate();
        return request;
    }

    /// <summary>Validates that the request is well-formed.</summary>
    /// <exception cref="ArgumentException">Thrown when required fields are missing or inconsistent.</exception>
    public void Validate()
    {
        // Guard: symbol components
        if (Symbol.Base.IsNone || Symbol.Quote.IsNone)
            throw new ArgumentException("A valid trading symbol is required.", nameof(Symbol));

        var errors = new List<string>();

        // Quantity validation
        if (Type != OrderType.Market || QuoteOrderQuantity is null or 0)
        {
            if (Quantity is null or <= 0)
                errors.Add("Quantity is required and must be positive.");
        }

        if (Type == OrderType.Market && QuoteOrderQuantity is not null && QuoteOrderQuantity <= 0)
            errors.Add("QuoteOrderQuantity must be positive.");

        // Price validation
        if (Type is OrderType.Limit or OrderType.StopLossLimit or OrderType.TakeProfitLimit or OrderType.LimitMaker)
        {
            if (Price is null or <= 0)
                errors.Add("Price is required for limit orders and must be positive.");
        }

        // Stop price validation
        if (Type is OrderType.StopLoss or OrderType.StopLossLimit or OrderType.TakeProfit or OrderType.TakeProfitLimit)
        {
            if (StopPrice is null or <= 0)
                errors.Add("StopPrice is required for stop orders and must be positive.");
        }

        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));
    }
}
