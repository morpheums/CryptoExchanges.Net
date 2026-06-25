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
            ["ordertype"] = MapOrderType(request.Type, request.TimeInForce)
        };

        if (request.Quantity.HasValue)
            parameters["volume"] = request.Quantity.Value.ToString(CultureInfo.InvariantCulture);
        else if (request.QuoteOrderQuantity.HasValue)
            parameters["volume"] = request.QuoteOrderQuantity.Value.ToString(CultureInfo.InvariantCulture);

        if (request.Price.HasValue && request.Type != OrderType.Market)
            parameters["price"] = request.Price.Value.ToString(CultureInfo.InvariantCulture);

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
        var closed = await http.PostAsync<ResponseDto<ClosedOrdersEnvelopeDto>>(
            "/0/private/ClosedOrders", closedParams, signed: true, ct: ct).ConfigureAwait(false);
        var (txId, dto) = closed.Result?.Closed.FirstOrDefault() ?? default;
        if (dto is null)
            return new Order(symbol, clientOrderId);
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
        var response = await http.PostAsync<ResponseDto<OpenOrdersEnvelopeDto>>(
            "/0/private/OpenOrders", signed: true, ct: ct).ConfigureAwait(false);

        var orders = response.Result?.Open ?? [];
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

        var response = await http.PostAsync<ResponseDto<ClosedOrdersEnvelopeDto>>(
            "/0/private/ClosedOrders", parameters, signed: true, ct: ct).ConfigureAwait(false);

        var wireSymbol = symbolMapper.ToWire(symbol);
        var orders = response.Result?.Closed ?? [];
        return orders
            .Where(kv => string.Equals(kv.Value.Descr.Pair, wireSymbol, StringComparison.OrdinalIgnoreCase))
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

    private static string MapOrderType(OrderType type, TimeInForce? tif) => type switch
    {
        OrderType.Market or OrderType.StopLoss or OrderType.TakeProfit => "market",
        OrderType.LimitMaker => "limit",
        OrderType.Limit or OrderType.StopLossLimit or OrderType.TakeProfitLimit => tif switch
        {
            TimeInForce.Ioc => "limit",
            TimeInForce.Fok => "limit",
            _ => "limit"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unsupported order type: {type}")
    };
}
