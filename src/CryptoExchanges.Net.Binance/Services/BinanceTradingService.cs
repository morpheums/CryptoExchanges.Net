using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoExchanges.Net.Binance.Internal;
using CryptoExchanges.Net.Core.Interfaces;
using DeltaMapper;

namespace CryptoExchanges.Net.Binance.Services;

/// <summary>
/// Binance implementation of <see cref="ITradingService"/>.
/// </summary>
internal sealed class BinanceTradingService(IBinanceHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : ITradingService
{
    /// <inheritdoc />
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        request.Validate();

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(request.Symbol),
            ["side"] = MapOrderSide(request.Side),
            ["type"] = MapOrderType(request.Type)
        };

        if (request.Quantity.HasValue)
            parameters["quantity"] = request.Quantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.QuoteOrderQuantity.HasValue)
            parameters["quoteOrderQty"] = request.QuoteOrderQuantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.Price.HasValue)
            parameters["price"] = request.Price.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.StopPrice.HasValue)
            parameters["stopPrice"] = request.StopPrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.TimeInForce.HasValue)
            parameters["timeInForce"] = MapTimeInForce(request.TimeInForce.Value);

        if (request.ClientOrderId is not null)
            parameters["newClientOrderId"] = request.ClientOrderId;

        if (request.IcebergQuantity.HasValue)
            parameters["icebergQty"] = request.IcebergQuantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var response = await http.PostAsync<OrderDto>("/api/v3/order", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<OrderDto, Order>(response);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["orderId"] = orderId
        };

        var response = await http.DeleteAsync<OrderDto>("/api/v3/order", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<OrderDto, Order>(response);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderByClientIdAsync(Symbol symbol, string clientOrderId, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["origClientOrderId"] = clientOrderId
        };

        var response = await http.DeleteAsync<OrderDto>("/api/v3/order", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<OrderDto, Order>(response);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> CancelAllOrdersAsync(Symbol symbol, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol)
        };

        // Binance returns a top-level JSON array whose items are either plain order
        // objects or order-list wrappers (e.g. OCO) carrying nested orderReports.
        var payload = await http.DeleteAsync<JsonElement[]>("/api/v3/openOrders", parameters, true, ct).ConfigureAwait(false);
        var orders = payload.SelectMany(ExtractCanceledOrders).ToList();
        return modelMapper.Map<OrderDto, Order>(orders);
    }

    /// <summary>
    /// Yields the canceled order(s) from a single cancel-all response item, unwrapping
    /// order-list entries (e.g. OCO) that carry their orders under <c>orderReports</c>.
    /// </summary>
    private static IEnumerable<OrderDto> ExtractCanceledOrders(JsonElement item)
    {
        if (item.TryGetProperty("orderReports", out var reports) && reports.ValueKind == JsonValueKind.Array)
            return reports.Deserialize<List<OrderDto>>() ?? [];

        var order = item.Deserialize<OrderDto>();
        return order is null ? [] : [order];
    }

    /// <inheritdoc />
    public async Task<Order> GetOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["orderId"] = orderId
        };

        var response = await http.GetAsync<OrderDto>("/api/v3/order", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<OrderDto, Order>(response);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(Symbol? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>();

        if (symbol.HasValue)
            parameters["symbol"] = mapper.ToWire(symbol.Value);

        var results = await http.GetAsync<List<OrderDto>>("/api/v3/openOrders", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<OrderDto, Order>(results);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOrderHistoryAsync(
        Symbol symbol,
        int limit = 500,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        BinanceRequestValidation.ValidateHistoryWindow(limit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = limit.ToString()
        };

        if (startTime.HasValue)
            parameters["startTime"] = startTime.Value.ToUnixTimeMilliseconds().ToString();
        if (endTime.HasValue)
            parameters["endTime"] = endTime.Value.ToUnixTimeMilliseconds().ToString();

        var results = await http.GetAsync<List<OrderDto>>("/api/v3/allOrders", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<OrderDto, Order>(results);
    }

    private static string MapOrderSide(OrderSide side) => side switch
    {
        OrderSide.Buy => "BUY",
        OrderSide.Sell => "SELL",
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, $"Unsupported order side: {side}")
    };

    private static string MapOrderType(OrderType type) => type switch
    {
        OrderType.Limit => "LIMIT",
        OrderType.Market => "MARKET",
        OrderType.StopLoss => "STOP_LOSS",
        OrderType.StopLossLimit => "STOP_LOSS_LIMIT",
        OrderType.TakeProfit => "TAKE_PROFIT",
        OrderType.TakeProfitLimit => "TAKE_PROFIT_LIMIT",
        OrderType.LimitMaker => "LIMIT_MAKER",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unsupported order type: {type}")
    };

    private static string MapTimeInForce(TimeInForce tif) => tif switch
    {
        TimeInForce.Gtc => "GTC",
        TimeInForce.Ioc => "IOC",
        TimeInForce.Fok => "FOK",
        _ => throw new ArgumentOutOfRangeException(nameof(tif), tif, $"Unsupported TimeInForce: {tif}")
    };

}
