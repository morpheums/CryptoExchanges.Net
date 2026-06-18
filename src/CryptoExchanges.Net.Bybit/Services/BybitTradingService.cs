using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Core.Interfaces;
using DeltaMapper;

namespace CryptoExchanges.Net.Bybit.Services;

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
        return await FetchOrderAsync(wireSymbol, orderId, ct, orderLinkId: request.ClientOrderId).ConfigureAwait(false);
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
        return await FetchOrderAsync(wireSymbol, canceledId, ct, orderLinkId: clientOrderId).ConfigureAwait(false);
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
        // The IExchangeClient default (500) exceeds Bybit V5's per-call max (50); clamp rather than
        // throw so the common default-parameter call path succeeds. A value < 1 still fails validation.
        var effectiveLimit = Math.Min(limit, BybitRequestValidation.MaxHistoryLimit);
        BybitRequestValidation.ValidateHistoryWindow(effectiveLimit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = effectiveLimit.ToString()
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
    private async Task<Order> FetchOrderAsync(
        string wireSymbol, string orderId, CancellationToken ct, string? orderLinkId = null)
    {
        // Some V5 ACKs (notably cancel-by-linkId) omit orderId; fall back to querying by orderLinkId
        // so the re-fetch resolves the same order instead of sending an empty orderId to Bybit.
        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = wireSymbol
        };
        if (!string.IsNullOrEmpty(orderId))
            parameters["orderId"] = orderId;
        else if (!string.IsNullOrEmpty(orderLinkId))
            parameters["orderLinkId"] = orderLinkId;

        var realtime = await http.GetAsync<BybitResponse<BybitListResult<BybitOrder>>>("/v5/order/realtime", parameters, true, ct).ConfigureAwait(false);
        var match = realtime.Result?.List.FirstOrDefault();
        if (match is not null)
            return modelMapper.Map<BybitOrder, Order>(match);

        var history = await http.GetAsync<BybitResponse<BybitListResult<BybitOrder>>>("/v5/order/history", parameters, true, ct).ConfigureAwait(false);
        match = history.Result?.List.FirstOrDefault();
        if (match is not null)
            return modelMapper.Map<BybitOrder, Order>(match);

        // Neither endpoint surfaced the order; return a minimal record carrying whichever identifier
        // we have (never empty) so callers still get an id to poll later rather than a null/throw.
        var fallbackId = !string.IsNullOrEmpty(orderId) ? orderId : (orderLinkId ?? string.Empty);
        return new Order(mapper.FromWire(wireSymbol), fallbackId);
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
