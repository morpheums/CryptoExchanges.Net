using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoExchanges.Net.Binance.Internal;
using CryptoExchanges.Net.Core.Interfaces;
using DeltaMapper;

namespace CryptoExchanges.Net.Binance.Services;

// ---------------------------------------------------------------------------
//  Binance Trading DTOs
// ---------------------------------------------------------------------------

internal sealed record BinanceOrderResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("clientOrderId")]
    public string ClientOrderId { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("origQty")]
    public string OrigQty { get; init; } = "0";

    [JsonPropertyName("executedQty")]
    public string ExecutedQty { get; init; } = "0";

    /// <summary>Binance 2026: cumulative quote quantity.</summary>
    [JsonPropertyName("cummulativeQuoteQty")]
    public string CumulativeQuoteQty { get; init; } = "0";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "NEW";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "LIMIT";

    [JsonPropertyName("side")]
    public string Side { get; init; } = "BUY";

    [JsonPropertyName("stopPrice")]
    public string StopPrice { get; init; } = "0";

    [JsonPropertyName("timeInForce")]
    public string TimeInForce { get; init; } = "GTC";

    [JsonPropertyName("icebergQty")]
    public string IcebergQty { get; init; } = "0";

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("updateTime")]
    public long UpdateTime { get; init; }

    /// <summary>Binance 2026: whether the order is still working.</summary>
    [JsonPropertyName("isWorking")]
    public bool IsWorking { get; init; }

    /// <summary>Binance 2026: working time.</summary>
    [JsonPropertyName("workingTime")]
    public long WorkingTime { get; init; }

    /// <summary>Binance 2026: original quote order quantity.</summary>
    [JsonPropertyName("origQuoteOrderQty")]
    public string OrigQuoteOrderQty { get; init; } = "0";

    /// <summary>Binance 2026: self-trade prevention mode.</summary>
    [JsonPropertyName("selfTradePreventionMode")]
    public string SelfTradePreventionMode { get; init; } = "NONE";
}

// ---------------------------------------------------------------------------
//  BinanceTradingService
// ---------------------------------------------------------------------------

/// <summary>
/// Binance implementation of <see cref="ITradingService"/>.
/// </summary>
internal sealed class BinanceTradingService(BinanceHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : ITradingService
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

        var response = await http.PostAsync<BinanceOrderResponse>("/api/v3/order", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<BinanceOrderResponse, Order>(response);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["orderId"] = orderId
        };

        var response = await http.DeleteAsync<BinanceOrderResponse>("/api/v3/order", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<BinanceOrderResponse, Order>(response);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderByClientIdAsync(Symbol symbol, string clientOrderId, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["origClientOrderId"] = clientOrderId
        };

        var response = await http.DeleteAsync<BinanceOrderResponse>("/api/v3/order", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<BinanceOrderResponse, Order>(response);
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
        return modelMapper.Map<BinanceOrderResponse, Order>(orders);
    }

    /// <summary>
    /// Yields the canceled order(s) from a single cancel-all response item, unwrapping
    /// order-list entries (e.g. OCO) that carry their orders under <c>orderReports</c>.
    /// </summary>
    private static IEnumerable<BinanceOrderResponse> ExtractCanceledOrders(JsonElement item)
    {
        if (item.TryGetProperty("orderReports", out var reports) && reports.ValueKind == JsonValueKind.Array)
            return reports.Deserialize<List<BinanceOrderResponse>>() ?? [];

        var order = item.Deserialize<BinanceOrderResponse>();
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

        var response = await http.GetAsync<BinanceOrderResponse>("/api/v3/order", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<BinanceOrderResponse, Order>(response);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(Symbol? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>();

        if (symbol.HasValue)
            parameters["symbol"] = mapper.ToWire(symbol.Value);

        var results = await http.GetAsync<List<BinanceOrderResponse>>("/api/v3/openOrders", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<BinanceOrderResponse, Order>(results);
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

        var results = await http.GetAsync<List<BinanceOrderResponse>>("/api/v3/allOrders", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<BinanceOrderResponse, Order>(results);
    }

    // ── Request-direction mapping helpers ──

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
