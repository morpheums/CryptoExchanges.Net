using System.Text.Json.Serialization;
using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Core.Interfaces;
using DeltaMapper;

namespace CryptoExchanges.Net.Bybit.Services;

// ---------------------------------------------------------------------------
//  Bybit V5 trading DTOs
// ---------------------------------------------------------------------------

/// <summary>The thin acknowledgement Bybit V5 returns from create/cancel: ids only, not a full order.</summary>
internal sealed record BybitOrderAck
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("orderLinkId")]
    public string OrderLinkId { get; init; } = string.Empty;
}

/// <summary>A full Bybit V5 order record as returned by <c>/v5/order/realtime</c> and <c>/v5/order/history</c>.</summary>
internal sealed record BybitOrder
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("orderLinkId")]
    public string OrderLinkId { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("qty")]
    public string Qty { get; init; } = "0";

    [JsonPropertyName("cumExecQty")]
    public string CumExecQty { get; init; } = "0";

    /// <summary>Cumulative executed value in the quote asset.</summary>
    [JsonPropertyName("cumExecValue")]
    public string CumExecValue { get; init; } = "0";

    [JsonPropertyName("side")]
    public string Side { get; init; } = "Buy";

    [JsonPropertyName("orderType")]
    public string OrderType { get; init; } = "Limit";

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; init; } = "New";

    [JsonPropertyName("timeInForce")]
    public string TimeInForce { get; init; } = "GTC";

    [JsonPropertyName("triggerPrice")]
    public string TriggerPrice { get; init; } = "0";

    /// <summary>Order creation time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("createdTime")]
    public string CreatedTime { get; init; } = "0";

    /// <summary>Last update time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("updatedTime")]
    public string UpdatedTime { get; init; } = "0";
}

// ---------------------------------------------------------------------------
//  BybitTradingService
// ---------------------------------------------------------------------------

/// <summary>
/// Bybit implementation of <see cref="ITradingService"/> against the V5 spot REST API.
/// </summary>
/// <remarks>
/// Bybit V5 create/cancel endpoints return only the order id, not the full order, so
/// <see cref="PlaceOrderAsync"/> and the cancel methods re-fetch the order via
/// <c>/v5/order/realtime</c> (falling back to <c>/v5/order/history</c>) to honour the
/// <see cref="ITradingService"/> contract of returning a fully populated <see cref="Order"/>.
/// </remarks>
internal sealed class BybitTradingService(IBybitHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : ITradingService
{
    private const string SpotCategory = "spot";

    /// <inheritdoc />
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        request.Validate();

        var wireSymbol = mapper.ToWire(request.Symbol);
        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = wireSymbol,
            ["side"] = MapOrderSide(request.Side),
            ["orderType"] = MapOrderType(request.Type)
        };

        if (request.Quantity.HasValue)
            parameters["qty"] = request.Quantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        else if (request.QuoteOrderQuantity.HasValue)
            // Bybit spot market-buy by quote amount: qty carries the quote value with marketUnit=quoteCoin.
            parameters["qty"] = request.QuoteOrderQuantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.QuoteOrderQuantity.HasValue && request.Type == OrderType.Market)
            parameters["marketUnit"] = "quoteCoin";

        if (request.Price.HasValue)
            parameters["price"] = request.Price.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.StopPrice.HasValue)
            parameters["triggerPrice"] = request.StopPrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.TimeInForce.HasValue)
            parameters["timeInForce"] = MapTimeInForce(request.TimeInForce.Value);

        if (request.ClientOrderId is not null)
            parameters["orderLinkId"] = request.ClientOrderId;

        var response = await http.PostAsync<BybitResponse<BybitOrderAck>>("/v5/order/create", parameters, true, ct).ConfigureAwait(false);
        var orderId = response.Result?.OrderId ?? string.Empty;
        return await FetchOrderAsync(wireSymbol, orderId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
    {
        var wireSymbol = mapper.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = wireSymbol,
            ["orderId"] = orderId
        };

        var response = await http.PostAsync<BybitResponse<BybitOrderAck>>("/v5/order/cancel", parameters, true, ct).ConfigureAwait(false);
        var canceledId = response.Result?.OrderId ?? orderId;
        return await FetchOrderAsync(wireSymbol, canceledId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderByClientIdAsync(Symbol symbol, string clientOrderId, CancellationToken ct = default)
    {
        var wireSymbol = mapper.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = wireSymbol,
            ["orderLinkId"] = clientOrderId
        };

        var response = await http.PostAsync<BybitResponse<BybitOrderAck>>("/v5/order/cancel", parameters, true, ct).ConfigureAwait(false);
        var canceledId = response.Result?.OrderId ?? string.Empty;
        return await FetchOrderAsync(wireSymbol, canceledId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> CancelAllOrdersAsync(Symbol symbol, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = mapper.ToWire(symbol)
        };

        // cancel-all returns only ids; re-fetch the full picture via order history so the canceled
        // orders are returned with their full state.
        var response = await http.PostAsync<BybitResponse<BybitListResult<BybitOrderAck>>>("/v5/order/cancel-all", parameters, true, ct).ConfigureAwait(false);
        var acks = response.Result?.List ?? [];
        if (acks.Count == 0)
            return [];

        var history = await GetOrderHistoryAsync(symbol, BybitRequestValidation.MaxHistoryLimit, ct: ct).ConfigureAwait(false);
        var canceledIds = acks.Select(a => a.OrderId).ToHashSet(StringComparer.Ordinal);
        return history.Where(o => canceledIds.Contains(o.OrderId)).ToList();
    }

    /// <inheritdoc />
    public async Task<Order> GetOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
        => await FetchOrderAsync(mapper.ToWire(symbol), orderId, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(Symbol? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string> { ["category"] = SpotCategory };
        if (symbol.HasValue)
            parameters["symbol"] = mapper.ToWire(symbol.Value);

        var response = await http.GetAsync<BybitResponse<BybitListResult<BybitOrder>>>("/v5/order/realtime", parameters, true, ct).ConfigureAwait(false);
        var orders = response.Result?.List ?? [];
        return modelMapper.Map<BybitOrder, Order>(orders);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOrderHistoryAsync(
        Symbol symbol,
        int limit = 500,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        BybitRequestValidation.ValidateHistoryWindow(limit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = limit.ToString()
        };

        if (startTime.HasValue)
            parameters["startTime"] = startTime.Value.ToUnixTimeMilliseconds().ToString();
        if (endTime.HasValue)
            parameters["endTime"] = endTime.Value.ToUnixTimeMilliseconds().ToString();

        var response = await http.GetAsync<BybitResponse<BybitListResult<BybitOrder>>>("/v5/order/history", parameters, true, ct).ConfigureAwait(false);
        var orders = response.Result?.List ?? [];
        return modelMapper.Map<BybitOrder, Order>(orders);
    }

    // ── Order re-fetch (V5 create/cancel return ids only) ──

    /// <summary>
    /// Resolves a full <see cref="Order"/> for a Bybit order id. Bybit's create/cancel responses
    /// carry only the id, so we query <c>/v5/order/realtime</c> (open/recently-closed orders) first
    /// and fall back to <c>/v5/order/history</c> for orders that have already left the realtime set.
    /// </summary>
    private async Task<Order> FetchOrderAsync(string wireSymbol, string orderId, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = wireSymbol,
            ["orderId"] = orderId
        };

        var realtime = await http.GetAsync<BybitResponse<BybitListResult<BybitOrder>>>("/v5/order/realtime", parameters, true, ct).ConfigureAwait(false);
        var match = realtime.Result?.List.FirstOrDefault();
        if (match is not null)
            return modelMapper.Map<BybitOrder, Order>(match);

        var history = await http.GetAsync<BybitResponse<BybitListResult<BybitOrder>>>("/v5/order/history", parameters, true, ct).ConfigureAwait(false);
        match = history.Result?.List.FirstOrDefault();
        if (match is not null)
            return modelMapper.Map<BybitOrder, Order>(match);

        // Neither endpoint surfaced the order; return a minimal record so callers still get the id
        // they need (e.g. to poll later) rather than a null/throw on an otherwise-successful action.
        return new Order(mapper.FromWire(wireSymbol), orderId);
    }

    // ── Request-direction mapping helpers ──

    private static string MapOrderSide(OrderSide side) => side switch
    {
        OrderSide.Buy => "Buy",
        OrderSide.Sell => "Sell",
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, $"Unsupported order side: {side}")
    };

    private static string MapOrderType(OrderType type) => type switch
    {
        // Bybit V5 spot exposes only Limit/Market base types; stop/take-profit behaviour is
        // conveyed by the triggerPrice field rather than a distinct order type, so the
        // limit-/market-shaped variants collapse onto their base type here.
        OrderType.Limit or OrderType.StopLossLimit or OrderType.TakeProfitLimit or OrderType.LimitMaker => "Limit",
        OrderType.Market or OrderType.StopLoss or OrderType.TakeProfit => "Market",
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
