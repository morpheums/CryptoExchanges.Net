using System.Globalization;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Kraken.Dtos;
using CryptoExchanges.Net.Kraken.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Kraken.Services;

/// <summary>Kraken implementation of <see cref="ITradingService"/> against the private REST API.</summary>
internal sealed class KrakenTradingService(IKrakenHttpClient http, ISymbolMapper symbolMapper, IMapper modelMapper) : ITradingService
{
    /// <inheritdoc />
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        request.Validate();

        var wireSymbol = symbolMapper.ToWire(request.Symbol);
        var parameters = new Dictionary<string, string>
        {
            ["pair"] = wireSymbol,
            ["type"] = MapOrderSide(request.Side),
            ["ordertype"] = MapOrderType(request.Type)
        };

        if (request.Quantity.HasValue)
            parameters["volume"] = request.Quantity.Value.ToString(CultureInfo.InvariantCulture);
        else if (request.QuoteOrderQuantity.HasValue)
            // Kraken AddOrder 'volume' is base-asset only; no quote-size param exists, so reject rather than mis-size.
            throw new NotSupportedException(
                "Kraken AddOrder 'volume' is denominated in the base asset; quote-denominated order "
                + "quantity (QuoteOrderQuantity) is not supported. Specify Quantity (base size) instead.");

        if (IsStopOrder(request.Type))
        {
            // Kraken carries the trigger price in 'price'; for *-limit variants the limit price goes in 'price2'.
            // Validate() guarantees StopPrice (all stop types) and Price (the -limit variants) are present.
            parameters["price"] = request.StopPrice!.Value.ToString(CultureInfo.InvariantCulture);
            if (request.Type is OrderType.StopLossLimit or OrderType.TakeProfitLimit)
                parameters["price2"] = request.Price!.Value.ToString(CultureInfo.InvariantCulture);
        }
        else if (request.Price.HasValue && request.Type != OrderType.Market)
        {
            parameters["price"] = request.Price.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.ClientOrderId is not null
            && int.TryParse(request.ClientOrderId, out var userRef))
            parameters["userref"] = userRef.ToString(CultureInfo.InvariantCulture);

        var response = await http.PostAsync<ResponseDto<OrderAckDto>>(
            "/0/private/AddOrder", parameters, signed: true, ct: ct).ConfigureAwait(false);

        var txId = response.Result?.TxId.FirstOrDefault()
            ?? throw new ExchangeApiException("AddOrder returned no transaction id.", null, null);
        return await FetchOrderAsync(txId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        var parameters = new Dictionary<string, string> { ["txid"] = orderId };
        await http.PostAsync<ResponseDto<JsonElement>>(
            "/0/private/CancelOrder", parameters, signed: true, ct: ct).ConfigureAwait(false);
        return await FetchOrderAsync(orderId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderByClientIdAsync(Symbol symbol, string clientOrderId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientOrderId);
        // Kraken uses integer userref; the client order id must be parseable as int.
        if (!int.TryParse(clientOrderId, out var userRef))
            throw new ArgumentException("Kraken client order ids must be integers (userref).", nameof(clientOrderId));

        var parameters = new Dictionary<string, string> { ["userref"] = userRef.ToString(CultureInfo.InvariantCulture) };
        await http.PostAsync<ResponseDto<JsonElement>>(
            "/0/private/CancelOrder", parameters, signed: true, ct: ct).ConfigureAwait(false);

        // Re-fetch the now-canceled order via ClosedOrders filtered by userref.
        var closedParams = new Dictionary<string, string>
        {
            ["userref"] = userRef.ToString(CultureInfo.InvariantCulture)
        };
        var closed = await http.PostResultPropertyAsync<Dictionary<string, OrderDto>>(
            "/0/private/ClosedOrders", "closed", closedParams, signed: true, ct: ct).ConfigureAwait(false) ?? [];
        // The dictionary's enumeration order is non-deterministic; if multiple closed orders share the
        // userref, pick the most recent by close time so we return a stable, correct txid.
        var (txId, dto) = closed
            .OrderByDescending(kv => kv.Value.CloseTime)
            .FirstOrDefault();
        if (dto is null)
            // The exchange txid is unknown here; carry the client id in ClientOrderId, never in OrderId.
            return new Order(symbol, string.Empty, ClientOrderId: clientOrderId, Status: OrderStatus.Canceled);
        return MapOrder(txId ?? string.Empty, dto);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> CancelAllOrdersAsync(Symbol symbol, CancellationToken ct = default)
    {
        var open = await GetOpenOrdersAsync(symbol, ct).ConfigureAwait(false);
        if (open.Count == 0)
            return [];

        var canceled = new List<Order>(open.Count);
        foreach (var order in open)
        {
            if (string.IsNullOrEmpty(order.OrderId))
                continue;
            var parameters = new Dictionary<string, string> { ["txid"] = order.OrderId };
            await http.PostAsync<ResponseDto<JsonElement>>(
                "/0/private/CancelOrder", parameters, signed: true, ct: ct).ConfigureAwait(false);
            canceled.Add(order with { Status = OrderStatus.Canceled });
        }
        return canceled;
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
        var orders = await http.PostResultPropertyAsync<Dictionary<string, OrderDto>>(
            "/0/private/OpenOrders", "open", signed: true, ct: ct).ConfigureAwait(false) ?? [];
        IEnumerable<KeyValuePair<string, OrderDto>> filtered = orders;

        if (symbol.HasValue)
        {
            var wireSymbol = symbolMapper.ToWire(symbol.Value);
            filtered = orders.Where(kv =>
                string.Equals(kv.Value.Descr.Pair, wireSymbol, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.Select(kv => MapOrder(kv.Key, kv.Value)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOrderHistoryAsync(
        Symbol symbol,
        int limit = 500,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        KrakenRequestValidation.ValidateHistoryWindow(limit, startTime, endTime);

        var parameters = new Dictionary<string, string>();
        if (startTime.HasValue)
            parameters["start"] = startTime.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["end"] = endTime.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var orders = await http.PostResultPropertyAsync<Dictionary<string, OrderDto>>(
            "/0/private/ClosedOrders", "closed", parameters, signed: true, ct: ct).ConfigureAwait(false) ?? [];

        var wireSymbol = symbolMapper.ToWire(symbol);
        // Kraken returns ClosedOrders as a dictionary with non-deterministic enumeration order;
        // sort most-recent-first by close time so Take(limit) yields the latest N, not an arbitrary subset.
        return orders
            .Where(kv => string.Equals(kv.Value.Descr.Pair, wireSymbol, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Value.CloseTime)
            .Take(limit)
            .Select(kv => MapOrder(kv.Key, kv.Value))
            .ToList();
    }

    /// <summary>Fetches the current state of an order by its transaction id via <c>QueryOrders</c>.</summary>
    private async Task<Order> FetchOrderAsync(string txId, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string> { ["txid"] = txId };
        var response = await http.PostAsync<ResponseDto<Dictionary<string, OrderDto>>>(
            "/0/private/QueryOrders", parameters, signed: true, ct: ct).ConfigureAwait(false);

        var orders = response.Result ?? [];
        var (key, dto) = orders.FirstOrDefault();
        if (dto is null)
            throw new ExchangeApiException($"Order not found: {txId}", null, null);
        return MapOrder(key ?? txId, dto);
    }

    private Order MapOrder(string txId, OrderDto dto)
    {
        var order = modelMapper.Map<OrderDto, Order>(dto);
        return order with { OrderId = txId };
    }

    private static string MapOrderSide(OrderSide side) => side switch
    {
        OrderSide.Buy => "buy",
        OrderSide.Sell => "sell",
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, $"Unsupported order side: {side}")
    };

    private static bool IsStopOrder(OrderType type) => type
        is OrderType.StopLoss or OrderType.StopLossLimit or OrderType.TakeProfit or OrderType.TakeProfitLimit;

    private static string MapOrderType(OrderType type) => type switch
    {
        OrderType.Market => "market",
        OrderType.Limit or OrderType.LimitMaker => "limit",
        OrderType.StopLoss => "stop-loss",
        OrderType.StopLossLimit => "stop-loss-limit",
        OrderType.TakeProfit => "take-profit",
        OrderType.TakeProfitLimit => "take-profit-limit",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unsupported order type: {type}")
    };
}
