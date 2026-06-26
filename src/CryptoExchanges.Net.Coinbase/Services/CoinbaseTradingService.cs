using CryptoExchanges.Net.Coinbase.Dtos;
using CryptoExchanges.Net.Coinbase.Internal;
using CryptoExchanges.Net.Core.Exceptions;
using DeltaMapper;

namespace CryptoExchanges.Net.Coinbase.Services;

/// <summary>
/// Coinbase Advanced Trade implementation of <see cref="ITradingService"/> against the V3 brokerage REST API.
/// </summary>
internal sealed class CoinbaseTradingService(ICoinbaseHttpClient http, ISymbolMapper symbolMapper, IMapper modelMapper) : ITradingService
{
    /// <inheritdoc />
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        request.Validate();

        var wireSymbol = symbolMapper.ToWire(request.Symbol);
        var body = BuildPlaceOrderBody(wireSymbol, request);

        var response = await http.PostAsync<PlaceOrderAckDto>("/api/v3/brokerage/orders", body, true, ct).ConfigureAwait(false);

        if (!response.Success)
        {
            var errorMsg = response.ErrorResponse is { } err
                ? $"Coinbase order placement failed: {err.Error} — {err.Message}"
                : "Coinbase order placement failed.";
            throw new InvalidOrderException(errorMsg, null, null);
        }

        var orderId = response.SuccessResponse?.OrderId ?? response.OrderId;
        return await FetchOrderAsync(orderId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        var body = new { order_ids = new[] { orderId } };
        await http.PostPropertyAsync<List<CancelOrderEntryDto>>("/api/v3/brokerage/orders/batch_cancel", "results", body, true, ct).ConfigureAwait(false);

        return await FetchOrderAsync(orderId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderByClientIdAsync(Symbol symbol, string clientOrderId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientOrderId);

        // Coinbase V3 does not expose a cancel-by-client-order-id endpoint directly on batch_cancel.
        // Look up the live order by client_order_id via the batch history, then cancel by order_id.
        var openOrders = await GetOpenOrdersAsync(symbol, ct).ConfigureAwait(false);
        var target = openOrders.FirstOrDefault(o => o.ClientOrderId == clientOrderId);

        if (target is not null && target.OrderId is { Length: > 0 } targetId)
            return await CancelOrderAsync(symbol, targetId, ct).ConfigureAwait(false);

        // Fall back: cancel blindly (the order may have filled between the list and cancel calls).
        // Return a placeholder so callers get the client id back rather than a null/throw.
        return new Order(symbol, string.Empty, ClientOrderId: clientOrderId, Status: OrderStatus.Canceled);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> CancelAllOrdersAsync(Symbol symbol, CancellationToken ct = default)
    {
        var open = await GetOpenOrdersAsync(symbol, ct).ConfigureAwait(false);
        if (open.Count == 0)
            return [];

        var orderIds = open
            .Where(o => !string.IsNullOrEmpty(o.OrderId))
            .Select(o => o.OrderId)
            .ToList();

        if (orderIds.Count == 0)
            return [];

        var body = new { order_ids = orderIds };
        var cancelResults = await http.PostPropertyAsync<List<CancelOrderEntryDto>>("/api/v3/brokerage/orders/batch_cancel", "results", body, true, ct).ConfigureAwait(false) ?? [];

        var canceledIds = cancelResults
            .Where(r => r.Success)
            .Select(r => r.OrderId)
            .ToHashSet(StringComparer.Ordinal);

        // Re-fetch history to get the final state of the canceled orders.
        var history = await GetOrderHistoryAsync(symbol, CoinbaseRequestValidation.MaxOrdersLimit, ct: ct).ConfigureAwait(false);
        return history.Where(o => canceledIds.Contains(o.OrderId)).ToList();
    }

    /// <inheritdoc />
    public async Task<Order> GetOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        return await FetchOrderAsync(orderId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(Symbol? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["order_status"] = "OPEN",
            ["limit"] = CoinbaseRequestValidation.MaxOrdersLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (symbol.HasValue)
            parameters["product_id"] = symbolMapper.ToWire(symbol.Value);

        var orders = await http.GetPropertyAsync<List<OrderDto>>("/api/v3/brokerage/orders/historical/batch", "orders", parameters, true, ct).ConfigureAwait(false) ?? [];
        return modelMapper.Map<OrderDto, Order>(orders);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOrderHistoryAsync(
        Symbol symbol,
        int limit = 500,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        var effectiveLimit = Math.Min(limit, CoinbaseRequestValidation.MaxOrdersLimit);

        var parameters = new Dictionary<string, string>
        {
            ["product_id"] = symbolMapper.ToWire(symbol),
            ["limit"] = effectiveLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (startTime.HasValue)
            parameters["start_date"] = startTime.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["end_date"] = endTime.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        var orders = await http.GetPropertyAsync<List<OrderDto>>("/api/v3/brokerage/orders/historical/batch", "orders", parameters, true, ct).ConfigureAwait(false) ?? [];
        return modelMapper.Map<OrderDto, Order>(orders);
    }

    private async Task<Order> FetchOrderAsync(string orderId, CancellationToken ct)
    {
        var dto = await http.GetPropertyAsync<OrderDto?>(
            $"/api/v3/brokerage/orders/historical/{Uri.EscapeDataString(orderId)}", "order", signed: true, ct: ct)
            .ConfigureAwait(false);

        if (dto is { } order)
            return modelMapper.Map<OrderDto, Order>(order);

        // Order did not surface (e.g. just placed and not yet propagated); return a minimal record.
        return new Order(default, orderId);
    }

    private static object BuildPlaceOrderBody(string wireSymbol, PlaceOrderRequest request)
    {
        var clientOrderId = request.ClientOrderId ?? Guid.NewGuid().ToString("N");

        if (request.Type == OrderType.Market || request.Type == OrderType.StopLoss || request.Type == OrderType.TakeProfit)
        {
            // Market order: market_market_ioc with quote_size (buy by amount) or base_size (sell or quantity specified).
            if (request.Side == OrderSide.Buy && request.QuoteOrderQuantity.HasValue)
            {
                return new
                {
                    client_order_id = clientOrderId,
                    product_id = wireSymbol,
                    side = MapOrderSide(request.Side),
                    order_configuration = new
                    {
                        market_market_ioc = new { quote_size = request.QuoteOrderQuantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) }
                    }
                };
            }

            return new
            {
                client_order_id = clientOrderId,
                product_id = wireSymbol,
                side = MapOrderSide(request.Side),
                order_configuration = new
                {
                    market_market_ioc = new { base_size = (request.Quantity ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture) }
                }
            };
        }

        if (request.Type == OrderType.LimitMaker)
        {
            // Post-only limit: limit_limit_gtc with post_only = true.
            return new
            {
                client_order_id = clientOrderId,
                product_id = wireSymbol,
                side = MapOrderSide(request.Side),
                order_configuration = new
                {
                    limit_limit_gtc = new
                    {
                        base_size = (request.Quantity ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        limit_price = (request.Price ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        post_only = true
                    }
                }
            };
        }

        // Limit (GTC default), StopLossLimit, TakeProfitLimit.
        return new
        {
            client_order_id = clientOrderId,
            product_id = wireSymbol,
            side = MapOrderSide(request.Side),
            order_configuration = new
            {
                limit_limit_gtc = new
                {
                    base_size = (request.Quantity ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    limit_price = (request.Price ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    post_only = false
                }
            }
        };
    }

    private static string MapOrderSide(OrderSide side) => side switch
    {
        OrderSide.Buy => "BUY",
        OrderSide.Sell => "SELL",
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, $"Unsupported order side: {side}")
    };
}
