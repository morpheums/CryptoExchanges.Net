using CryptoExchanges.Net.Kucoin.Dtos;
using CryptoExchanges.Net.Kucoin.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Kucoin.Services;

/// <summary>
/// KuCoin implementation of <see cref="ITradingService"/> against the V1 spot REST API.
/// Place/cancel endpoints return only the order ID, so each mutating method re-fetches the full order.
/// </summary>
internal sealed class KucoinTradingService(IKucoinHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : ITradingService
{
    /// <inheritdoc />
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        request.Validate();

        var wireSymbol = mapper.ToWire(request.Symbol);
        var parameters = new Dictionary<string, string>
        {
            ["clientOid"] = request.ClientOrderId ?? Guid.NewGuid().ToString("N"),
            ["side"] = MapOrderSide(request.Side),
            ["symbol"] = wireSymbol,
            ["type"] = MapOrderType(request.Type)
        };

        if (request.Quantity.HasValue)
            parameters["size"] = request.Quantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        else if (request.QuoteOrderQuantity.HasValue)
            parameters["funds"] = request.QuoteOrderQuantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Market orders carry no price; KuCoin rejects a price on a market order.
        if (request.Price.HasValue && request.Type != OrderType.Market)
            parameters["price"] = request.Price.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.TimeInForce.HasValue && request.Type != OrderType.Market)
            parameters["timeInForce"] = MapTimeInForce(request.TimeInForce.Value);

        var response = await http.PostAsync<ResponseDto<OrderAckDto>>("/api/v1/orders", parameters, true, ct).ConfigureAwait(false);
        var orderId = response.Data?.OrderId ?? string.Empty;
        return await FetchOrderAsync(orderId, ct, clientOrderId: request.ClientOrderId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync<ResponseDto<CancelOrderAckDto>>($"/api/v1/orders/{orderId}", null, true, ct).ConfigureAwait(false);
        var cancelledId = response.Data?.CancelledOrderIds?.FirstOrDefault() ?? orderId;
        return await FetchOrderAsync(cancelledId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderByClientIdAsync(Symbol symbol, string clientOrderId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync<ResponseDto<CancelOrderAckDto>>($"/api/v1/order/client-order/{clientOrderId}", null, true, ct).ConfigureAwait(false);
        var cancelledId = response.Data?.CancelledOrderIds?.FirstOrDefault() ?? string.Empty;
        return await FetchOrderAsync(cancelledId, ct, clientOrderId: clientOrderId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> CancelAllOrdersAsync(Symbol symbol, CancellationToken ct = default)
    {
        var wireSymbol = mapper.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = wireSymbol,
            ["tradeType"] = "TRADE"
        };

        var response = await http.DeleteAsync<ResponseDto<CancelOrderAckDto>>("/api/v1/orders", parameters, true, ct).ConfigureAwait(false);
        var cancelledIds = response.Data?.CancelledOrderIds ?? [];
        if (cancelledIds.Count == 0)
            return [];

        // Re-fetch cancelled orders from history.
        var history = await GetOrderHistoryAsync(symbol, KucoinRequestValidation.MaxHistoryLimit, ct: ct).ConfigureAwait(false);
        var idSet = cancelledIds.ToHashSet(StringComparer.Ordinal);
        return history.Where(o => idSet.Contains(o.OrderId)).ToList();
    }

    /// <inheritdoc />
    public async Task<Order> GetOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
        => await FetchOrderAsync(orderId, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(Symbol? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string> { ["status"] = "active" };
        if (symbol.HasValue)
            parameters["symbol"] = mapper.ToWire(symbol.Value);

        var response = await http.GetAsync<ResponseDto<ListDto<OrderDto>>>("/api/v1/orders", parameters, true, ct).ConfigureAwait(false);
        var items = response.Data?.Items ?? [];
        return modelMapper.Map<OrderDto, Order>(items);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOrderHistoryAsync(
        Symbol symbol,
        int limit = 500,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        var effectiveLimit = Math.Min(limit, KucoinRequestValidation.MaxHistoryLimit);
        KucoinRequestValidation.ValidateHistoryWindow(effectiveLimit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["status"] = "done",
            ["symbol"] = mapper.ToWire(symbol),
            ["pageSize"] = effectiveLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (startTime.HasValue)
            parameters["startAt"] = startTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["endAt"] = endTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        var response = await http.GetAsync<ResponseDto<ListDto<OrderDto>>>("/api/v1/orders", parameters, true, ct).ConfigureAwait(false);
        var items = response.Data?.Items ?? [];
        return modelMapper.Map<OrderDto, Order>(items);
    }

    /// <summary>Fetches the full <see cref="Order"/> by order ID, falling back to the client-order endpoint when the ID is absent.</summary>
    private async Task<Order> FetchOrderAsync(string orderId, CancellationToken ct, string? clientOrderId = null)
    {
        if (!string.IsNullOrEmpty(orderId))
        {
            var response = await http.GetAsync<ResponseDto<OrderDto>>($"/api/v1/orders/{orderId}", null, true, ct).ConfigureAwait(false);
            if (response.Data is not null)
                return modelMapper.Map<OrderDto, Order>(response.Data);
        }
        else if (!string.IsNullOrEmpty(clientOrderId))
        {
            var response = await http.GetAsync<ResponseDto<OrderDto>>($"/api/v1/order/client-order/{clientOrderId}", null, true, ct).ConfigureAwait(false);
            if (response.Data is not null)
                return modelMapper.Map<OrderDto, Order>(response.Data);
        }

        // Could not resolve the order; return a minimal record so callers still get an id.
        var fallbackId = !string.IsNullOrEmpty(orderId) ? orderId : (clientOrderId ?? string.Empty);
        // Use a default symbol (we don't have the wire symbol here for mapper.FromWire).
        return new Order(default, fallbackId);
    }

    private static string MapOrderSide(OrderSide side) => side switch
    {
        OrderSide.Buy => "buy",
        OrderSide.Sell => "sell",
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, $"Unsupported order side: {side}")
    };

    private static string MapOrderType(OrderType type) => type switch
    {
        OrderType.Limit or OrderType.LimitMaker or OrderType.StopLossLimit or OrderType.TakeProfitLimit => "limit",
        OrderType.Market or OrderType.StopLoss or OrderType.TakeProfit => "market",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unsupported order type for KuCoin: {type}")
    };

    private static string MapTimeInForce(TimeInForce tif) => tif switch
    {
        TimeInForce.Gtc => "GTC",
        TimeInForce.Ioc => "IOC",
        TimeInForce.Fok => "FOK",
        _ => "GTC"
    };
}
