using CryptoExchanges.Net.Bitget.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Bitget.Services;

// ---------------------------------------------------------------------------
//  Bitget V2 trading DTOs
// ---------------------------------------------------------------------------

/// <summary>
/// The per-order acknowledgement Bitget V2 returns from place/cancel: the ids only. A non-success
/// envelope never reaches the services because the resilience pipeline's error translator converts
/// it into a typed exception.
/// </summary>
internal sealed record BitgetOrderAck
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("clientOid")]
    public string ClientOid { get; init; } = string.Empty;
}

/// <summary>A full Bitget V2 order record as returned by <c>/api/v2/spot/trade/orderInfo</c> and the order lists.</summary>
internal sealed record BitgetOrder
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("clientOid")]
    public string ClientOid { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    /// <summary>Original order size (base currency for limit; quote for market-buy by quote).</summary>
    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    /// <summary>Accumulated filled base quantity.</summary>
    [JsonPropertyName("baseVolume")]
    public string BaseVolume { get; init; } = "0";

    /// <summary>Accumulated filled quote amount (price * filled base).</summary>
    [JsonPropertyName("quoteVolume")]
    public string QuoteVolume { get; init; } = "0";

    /// <summary>Average fill price; Bitget emits "" / "0" when there are no fills yet.</summary>
    [JsonPropertyName("priceAvg")]
    public string PriceAvg { get; init; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; init; } = "buy";

    [JsonPropertyName("orderType")]
    public string OrderType { get; init; } = "limit";

    /// <summary>Time-in-force (<c>gtc</c>/<c>ioc</c>/<c>fok</c>/<c>post_only</c>).</summary>
    [JsonPropertyName("force")]
    public string Force { get; init; } = "gtc";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "live";

    /// <summary>Order creation time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("cTime")]
    public string CTime { get; init; } = "0";

    /// <summary>Last update time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("uTime")]
    public string UTime { get; init; } = "0";
}

// ---------------------------------------------------------------------------
//  BitgetTradingService
// ---------------------------------------------------------------------------

/// <summary>
/// Bitget implementation of <see cref="ITradingService"/> against the V2 spot REST API.
/// </summary>
/// <remarks>
/// Bitget V2 place/cancel endpoints return only the order id (inside a per-order ack), not the full
/// order, so <see cref="PlaceOrderAsync"/> and the cancel methods re-fetch the order via
/// <c>/api/v2/spot/trade/orderInfo</c> to honour the <see cref="ITradingService"/> contract of
/// returning a fully populated <see cref="Order"/>.
/// <para>
/// POST bodies (place/cancel) are flat string-keyed JSON objects (symbol, side, orderType, force,
/// size, price, clientOid) — all Bitget spot single-order fields are scalar, so the existing
/// <c>IBitgetHttpClient.PostAsync(Dictionary&lt;string,string&gt;)</c> serializes the exact wire body
/// the signer reads back. The batch-cancel body is a JSON array, so it uses the typed object-body overload.
/// </para>
/// </remarks>
internal sealed class BitgetTradingService(IBitgetHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : ITradingService
{
    /// <inheritdoc />
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        request.Validate();

        var wireSymbol = mapper.ToWire(request.Symbol);
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = wireSymbol,
            ["side"] = MapOrderSide(request.Side),
            ["orderType"] = MapOrderType(request.Type),
            ["force"] = MapForce(request.Type, request.TimeInForce)
        };

        if (request.Quantity.HasValue)
            parameters["size"] = request.Quantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        else if (request.QuoteOrderQuantity.HasValue)
            // Bitget spot market-buy by quote amount: size carries the quote value.
            parameters["size"] = request.QuoteOrderQuantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Market orders carry no price; Bitget rejects a price on a market order.
        if (request.Price.HasValue && request.Type != OrderType.Market)
            parameters["price"] = request.Price.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.ClientOrderId is not null)
            parameters["clientOid"] = request.ClientOrderId;

        var response = await http.PostAsync<BitgetResponse<BitgetOrderAck>>("/api/v2/spot/trade/place-order", parameters, true, ct).ConfigureAwait(false);
        var ack = response.Data.FirstOrDefault();
        return await FetchOrderAsync(wireSymbol, ack?.OrderId ?? string.Empty, ct, clientOrderId: request.ClientOrderId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
    {
        var wireSymbol = mapper.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = wireSymbol,
            ["orderId"] = orderId
        };

        var response = await http.PostAsync<BitgetResponse<BitgetOrderAck>>("/api/v2/spot/trade/cancel-order", parameters, true, ct).ConfigureAwait(false);
        var canceledId = response.Data.FirstOrDefault()?.OrderId ?? orderId;
        return await FetchOrderAsync(wireSymbol, canceledId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderByClientIdAsync(Symbol symbol, string clientOrderId, CancellationToken ct = default)
    {
        var wireSymbol = mapper.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = wireSymbol,
            ["clientOid"] = clientOrderId
        };

        var response = await http.PostAsync<BitgetResponse<BitgetOrderAck>>("/api/v2/spot/trade/cancel-order", parameters, true, ct).ConfigureAwait(false);
        var canceledId = response.Data.FirstOrDefault()?.OrderId ?? string.Empty;
        return await FetchOrderAsync(wireSymbol, canceledId, ct, clientOrderId: clientOrderId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> CancelAllOrdersAsync(Symbol symbol, CancellationToken ct = default)
    {
        // Bitget's symbol-scoped batch cancel takes a JSON ARRAY of {symbol, orderId} objects. All
        // fields are scalar strings, but the body is an array, so build it through the typed
        // object-body overload. Enumerate the open orders first, then re-fetch the affected orders.
        var open = await GetOpenOrdersAsync(symbol, ct).ConfigureAwait(false);
        if (open.Count == 0)
            return [];

        var wireSymbol = mapper.ToWire(symbol);
        var cancels = open
            .Where(o => !string.IsNullOrEmpty(o.OrderId))
            .Select(o => new Dictionary<string, string> { ["symbol"] = wireSymbol, ["orderId"] = o.OrderId })
            .ToList();
        if (cancels.Count == 0)
            return [];

        var response = await http.PostAsync<BitgetResponse<BitgetOrderAck>>("/api/v2/spot/trade/batch-cancel-order", cancels, true, ct).ConfigureAwait(false);
        var canceledIds = response.Data.Select(a => a.OrderId).Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
        if (canceledIds.Count == 0)
            return [];

        var history = await GetOrderHistoryAsync(symbol, BitgetRequestValidation.MaxHistoryLimit, ct: ct).ConfigureAwait(false);
        return history.Where(o => canceledIds.Contains(o.OrderId)).ToList();
    }

    /// <inheritdoc />
    public async Task<Order> GetOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
        => await FetchOrderAsync(mapper.ToWire(symbol), orderId, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(Symbol? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>();
        if (symbol.HasValue)
            parameters["symbol"] = mapper.ToWire(symbol.Value);

        var response = await http.GetAsync<BitgetResponse<BitgetOrder>>("/api/v2/spot/trade/unfilled-orders", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<BitgetOrder, Order>(response.Data);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOrderHistoryAsync(
        Symbol symbol,
        int limit = 500,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        // The IExchangeClient default (500) exceeds Bitget V2's per-call max (100); clamp rather than
        // throw so the common default-parameter call path succeeds. A value < 1 still fails validation.
        var effectiveLimit = Math.Min(limit, BitgetRequestValidation.MaxHistoryLimit);
        BitgetRequestValidation.ValidateHistoryWindow(effectiveLimit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = effectiveLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (startTime.HasValue)
            parameters["startTime"] = startTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["endTime"] = endTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        var response = await http.GetAsync<BitgetResponse<BitgetOrder>>("/api/v2/spot/trade/history-orders", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<BitgetOrder, Order>(response.Data);
    }

    // ── Order re-fetch (V2 place/cancel return ids only) ──

    /// <summary>
    /// Resolves a full <see cref="Order"/> for a Bitget order id. Bitget place/cancel responses carry
    /// only the id, so we query <c>/api/v2/spot/trade/orderInfo</c> (by orderId, or clientOid when the
    /// ack omits orderId).
    /// </summary>
    private async Task<Order> FetchOrderAsync(
        string wireSymbol, string orderId, CancellationToken ct, string? clientOrderId = null)
    {
        // Some acks omit orderId; fall back to querying by clientOid so the re-fetch resolves the same
        // order instead of sending an empty orderId to Bitget.
        var parameters = new Dictionary<string, string> { ["symbol"] = wireSymbol };
        if (!string.IsNullOrEmpty(orderId))
            parameters["orderId"] = orderId;
        else if (!string.IsNullOrEmpty(clientOrderId))
            parameters["clientOid"] = clientOrderId;

        var response = await http.GetAsync<BitgetResponse<BitgetOrder>>("/api/v2/spot/trade/orderInfo", parameters, true, ct).ConfigureAwait(false);
        var match = response.Data.FirstOrDefault();
        if (match is not null)
            return modelMapper.Map<BitgetOrder, Order>(match);

        // The order did not surface; return a minimal record carrying whichever identifier we have
        // (never empty) so callers still get an id to poll later rather than a null/throw.
        var fallbackId = !string.IsNullOrEmpty(orderId) ? orderId : (clientOrderId ?? string.Empty);
        return new Order(mapper.FromWire(wireSymbol), fallbackId);
    }

    // ── Request-direction mapping helpers ──

    private static string MapOrderSide(OrderSide side) => side switch
    {
        OrderSide.Buy => "buy",
        OrderSide.Sell => "sell",
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, $"Unsupported order side: {side}")
    };

    /// <summary>
    /// Maps the domain order type onto Bitget's <c>orderType</c> (<c>limit</c>/<c>market</c>). Bitget
    /// V2 spot has no distinct stop/take-profit order type on this endpoint (algo/plan orders use a
    /// separate API), so those variants collapse onto their base type.
    /// </summary>
    private static string MapOrderType(OrderType type) => type switch
    {
        OrderType.Market or OrderType.StopLoss or OrderType.TakeProfit => "market",
        OrderType.Limit or OrderType.LimitMaker or OrderType.StopLossLimit or OrderType.TakeProfitLimit => "limit",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unsupported order type: {type}")
    };

    /// <summary>
    /// Maps the domain time-in-force onto Bitget's <c>force</c> field, which Bitget carries SEPARATELY
    /// from <c>orderType</c> (unlike OKX). A <see cref="OrderType.LimitMaker"/> is a maker-only resting
    /// order, so it maps to <c>post_only</c>; a plain limit defaults to <c>gtc</c>. Market orders are
    /// non-resting and map to <c>gtc</c> (Bitget ignores force on market orders).
    /// </summary>
    private static string MapForce(OrderType type, TimeInForce? tif)
    {
        if (type == OrderType.LimitMaker)
            return "post_only";
        return tif switch
        {
            TimeInForce.Ioc => "ioc",
            TimeInForce.Fok => "fok",
            // Gtc (or unspecified) is a resting limit order.
            _ => "gtc"
        };
    }
}
