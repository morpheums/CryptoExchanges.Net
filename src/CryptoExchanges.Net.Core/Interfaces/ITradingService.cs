using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Core.Interfaces;

/// <summary>Provides access to order placement and management endpoints.</summary>
public interface ITradingService
{
    /// <summary>Places a new order.</summary>
    Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default);

    /// <summary>Cancels an order by exchange-assigned order ID.</summary>
    Task<Order> CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default);

    /// <summary>Cancels an order by client-assigned order ID.</summary>
    Task<Order> CancelOrderByClientIdAsync(Symbol symbol, string clientOrderId, CancellationToken ct = default);

    /// <summary>Cancels all open orders for the specified symbol.</summary>
    Task<IReadOnlyList<Order>> CancelAllOrdersAsync(Symbol symbol, CancellationToken ct = default);

    /// <summary>Retrieves a specific order by exchange-assigned order ID.</summary>
    Task<Order> GetOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default);

    /// <summary>Retrieves all currently open orders, optionally filtered by symbol.</summary>
    Task<IReadOnlyList<Order>> GetOpenOrdersAsync(Symbol? symbol = null, CancellationToken ct = default);

    /// <summary>Retrieves order history for the specified symbol.</summary>
    /// <param name="symbol">The trading pair symbol.</param>
    /// <param name="limit">Maximum number of orders to retrieve.</param>
    /// <param name="startTime">Optional start time filter.</param>
    /// <param name="endTime">Optional end time filter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<Order>> GetOrderHistoryAsync(
        Symbol symbol,
        int limit = 500,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default);
}
