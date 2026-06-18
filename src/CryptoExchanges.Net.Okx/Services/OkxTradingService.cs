using CryptoExchanges.Net.Okx.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Okx.Services;

// ---------------------------------------------------------------------------
//  OKX V5 trading DTOs
// ---------------------------------------------------------------------------

/// <summary>
/// The per-order acknowledgement OKX V5 returns from place/cancel: ids + a per-order status code.
/// OKX nests a per-order outcome (<c>sCode</c>/<c>sMsg</c>) inside the data array even when the
/// top-level <c>code</c> is "0"; a non-zero <c>sCode</c> never reaches the services because the
/// resilience pipeline's error translator converts it into a typed exception.
/// </summary>
internal sealed record OkxOrderAck
{
    [JsonPropertyName("ordId")]
    public string OrdId { get; init; } = string.Empty;

    [JsonPropertyName("clOrdId")]
    public string ClOrdId { get; init; } = string.Empty;

    /// <summary>Per-order result code ("0" = success).</summary>
    [JsonPropertyName("sCode")]
    public string SCode { get; init; } = "0";

    [JsonPropertyName("sMsg")]
    public string SMsg { get; init; } = string.Empty;
}

/// <summary>A full OKX V5 order record as returned by <c>/api/v5/trade/order</c> and the order lists.</summary>
internal sealed record OkxOrder
{
    [JsonPropertyName("instId")]
    public string InstId { get; init; } = string.Empty;

    [JsonPropertyName("ordId")]
    public string OrdId { get; init; } = string.Empty;

    [JsonPropertyName("clOrdId")]
    public string ClOrdId { get; init; } = string.Empty;

    [JsonPropertyName("px")]
    public string Px { get; init; } = "0";

    /// <summary>Original order size (in base currency for spot limit; quote for market-buy by quote).</summary>
    [JsonPropertyName("sz")]
    public string Sz { get; init; } = "0";

    /// <summary>Accumulated filled size in the base currency.</summary>
    [JsonPropertyName("accFillSz")]
    public string AccFillSz { get; init; } = "0";

    /// <summary>Average fill price; OKX emits "" when there are no fills yet.</summary>
    [JsonPropertyName("avgPx")]
    public string AvgPx { get; init; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; init; } = "buy";

    [JsonPropertyName("ordType")]
    public string OrdType { get; init; } = "limit";

    [JsonPropertyName("state")]
    public string State { get; init; } = "live";

    /// <summary>Order creation time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("cTime")]
    public string CTime { get; init; } = "0";

    /// <summary>Last update time in unix milliseconds (string-encoded).</summary>
    [JsonPropertyName("uTime")]
    public string UTime { get; init; } = "0";
}

// ---------------------------------------------------------------------------
//  OkxTradingService
// ---------------------------------------------------------------------------

/// <summary>
/// OKX implementation of <see cref="ITradingService"/> against the V5 spot REST API.
/// </summary>
/// <remarks>
/// OKX V5 place/cancel endpoints return only the order id (inside a per-order ack), not the full
/// order, so <see cref="PlaceOrderAsync"/> and the cancel methods re-fetch the order via
/// <c>/api/v5/trade/order</c> to honour the <see cref="ITradingService"/> contract of returning a
/// fully populated <see cref="Order"/>.
/// <para>
/// POST bodies (place/cancel) are flat string-keyed JSON objects (instId, tdMode, side, ordType,
/// sz, px, clOrdId) — all OKX spot order fields are scalar, so the existing
/// <c>IOkxHttpClient.PostAsync(Dictionary&lt;string,string&gt;)</c> serializes the exact wire body
/// the signer reads back; no nested/array overload is required.
/// </para>
/// </remarks>
internal sealed class OkxTradingService(IOkxHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : ITradingService
{
    /// <summary>Spot uses the cash trade mode (no margin).</summary>
    private const string CashTradeMode = "cash";

    /// <inheritdoc />
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        request.Validate();

        var wireSymbol = mapper.ToWire(request.Symbol);
        var ordType = MapOrderType(request.Type, request.TimeInForce);
        var parameters = new Dictionary<string, string>
        {
            ["instId"] = wireSymbol,
            ["tdMode"] = CashTradeMode,
            ["side"] = MapOrderSide(request.Side),
            ["ordType"] = ordType
        };

        if (request.Quantity.HasValue)
            parameters["sz"] = request.Quantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        else if (request.QuoteOrderQuantity.HasValue)
        {
            // OKX spot market-buy by quote amount: sz carries the quote value with tgtCcy=quote_ccy.
            parameters["sz"] = request.QuoteOrderQuantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (request.Type == OrderType.Market)
                parameters["tgtCcy"] = "quote_ccy";
        }

        // Market orders carry no price; OKX rejects a px on a market order.
        if (request.Price.HasValue && request.Type != OrderType.Market)
            parameters["px"] = request.Price.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.ClientOrderId is not null)
            parameters["clOrdId"] = request.ClientOrderId;

        var response = await http.PostAsync<OkxResponse<OkxOrderAck>>("/api/v5/trade/order", parameters, true, ct).ConfigureAwait(false);
        var ack = response.Data.FirstOrDefault();
        return await FetchOrderAsync(wireSymbol, ack?.OrdId ?? string.Empty, ct, clientOrderId: request.ClientOrderId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
    {
        var wireSymbol = mapper.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["instId"] = wireSymbol,
            ["ordId"] = orderId
        };

        var response = await http.PostAsync<OkxResponse<OkxOrderAck>>("/api/v5/trade/cancel-order", parameters, true, ct).ConfigureAwait(false);
        var canceledId = response.Data.FirstOrDefault()?.OrdId ?? orderId;
        return await FetchOrderAsync(wireSymbol, canceledId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Order> CancelOrderByClientIdAsync(Symbol symbol, string clientOrderId, CancellationToken ct = default)
    {
        var wireSymbol = mapper.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["instId"] = wireSymbol,
            ["clOrdId"] = clientOrderId
        };

        var response = await http.PostAsync<OkxResponse<OkxOrderAck>>("/api/v5/trade/cancel-order", parameters, true, ct).ConfigureAwait(false);
        var canceledId = response.Data.FirstOrDefault()?.OrdId ?? string.Empty;
        return await FetchOrderAsync(wireSymbol, canceledId, ct, clientOrderId: clientOrderId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> CancelAllOrdersAsync(Symbol symbol, CancellationToken ct = default)
    {
        // OKX has no symbol-scoped cancel-all; enumerate the open orders for the symbol and cancel
        // each via cancel-batch-orders, then re-fetch the affected orders' full state via history.
        var open = await GetOpenOrdersAsync(symbol, ct).ConfigureAwait(false);
        if (open.Count == 0)
            return [];

        var wireSymbol = mapper.ToWire(symbol);
        // cancel-batch-orders takes a JSON ARRAY of {instId, ordId} objects. All fields are scalar
        // strings, but the body is an array, so build it through the typed object-body overload.
        var cancels = open
            .Where(o => !string.IsNullOrEmpty(o.OrderId))
            .Select(o => new Dictionary<string, string> { ["instId"] = wireSymbol, ["ordId"] = o.OrderId })
            .ToList();
        if (cancels.Count == 0)
            return [];

        var response = await http.PostAsync<OkxResponse<OkxOrderAck>>("/api/v5/trade/cancel-batch-orders", cancels, true, ct).ConfigureAwait(false);
        var canceledIds = response.Data.Select(a => a.OrdId).Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
        if (canceledIds.Count == 0)
            return [];

        var history = await GetOrderHistoryAsync(symbol, OkxRequestValidation.MaxHistoryLimit, ct: ct).ConfigureAwait(false);
        return history.Where(o => canceledIds.Contains(o.OrderId)).ToList();
    }

    /// <inheritdoc />
    public async Task<Order> GetOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)
        => await FetchOrderAsync(mapper.ToWire(symbol), orderId, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(Symbol? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string> { ["instType"] = OkxRequestValidation.SpotInstType };
        if (symbol.HasValue)
            parameters["instId"] = mapper.ToWire(symbol.Value);

        var response = await http.GetAsync<OkxResponse<OkxOrder>>("/api/v5/trade/orders-pending", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<OkxOrder, Order>(response.Data);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOrderHistoryAsync(
        Symbol symbol,
        int limit = 500,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        // The IExchangeClient default (500) exceeds OKX V5's per-call max (100); clamp rather than
        // throw so the common default-parameter call path succeeds. A value < 1 still fails validation.
        var effectiveLimit = Math.Min(limit, OkxRequestValidation.MaxHistoryLimit);
        OkxRequestValidation.ValidateHistoryWindow(effectiveLimit, startTime, endTime);

        var parameters = new Dictionary<string, string>
        {
            ["instType"] = OkxRequestValidation.SpotInstType,
            ["instId"] = mapper.ToWire(symbol),
            ["limit"] = effectiveLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        // OKX paginates history with 'before'/'after' (exclusive) cursors keyed by ts.
        if (startTime.HasValue)
            parameters["after"] = startTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["before"] = endTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        var response = await http.GetAsync<OkxResponse<OkxOrder>>("/api/v5/trade/orders-history", parameters, true, ct).ConfigureAwait(false);
        return modelMapper.Map<OkxOrder, Order>(response.Data);
    }

    // ── Order re-fetch (V5 place/cancel return ids only) ──

    /// <summary>
    /// Resolves a full <see cref="Order"/> for an OKX order id. OKX place/cancel responses carry only
    /// the id, so we query <c>/api/v5/trade/order</c> (by ordId, or clOrdId when the ack omits ordId).
    /// </summary>
    private async Task<Order> FetchOrderAsync(
        string wireSymbol, string orderId, CancellationToken ct, string? clientOrderId = null)
    {
        // Some acks (notably cancel-by-clOrdId) omit ordId; fall back to querying by clOrdId so the
        // re-fetch resolves the same order instead of sending an empty ordId to OKX.
        var parameters = new Dictionary<string, string> { ["instId"] = wireSymbol };
        if (!string.IsNullOrEmpty(orderId))
            parameters["ordId"] = orderId;
        else if (!string.IsNullOrEmpty(clientOrderId))
            parameters["clOrdId"] = clientOrderId;

        var response = await http.GetAsync<OkxResponse<OkxOrder>>("/api/v5/trade/order", parameters, true, ct).ConfigureAwait(false);
        var match = response.Data.FirstOrDefault();
        if (match is not null)
            return modelMapper.Map<OkxOrder, Order>(match);

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
    /// Maps the domain order type (plus optional time-in-force) onto OKX's <c>ordType</c>, which folds
    /// TIF semantics in: a limit order's TIF (<c>ioc</c>/<c>fok</c>/post-only) IS the ordType, while a
    /// plain limit is <c>limit</c> (GTC). OKX V5 spot has no distinct stop/take-profit ordType (algo
    /// orders use a separate API), so those limit-/market-shaped variants collapse onto their base type.
    /// </summary>
    private static string MapOrderType(OrderType type, TimeInForce? tif) => type switch
    {
        OrderType.Market or OrderType.StopLoss or OrderType.TakeProfit => "market",
        OrderType.LimitMaker => "post_only",
        OrderType.Limit or OrderType.StopLossLimit or OrderType.TakeProfitLimit => tif switch
        {
            TimeInForce.Ioc => "ioc",
            TimeInForce.Fok => "fok",
            // Gtc (or unspecified) is a resting limit order.
            _ => "limit"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unsupported order type: {type}")
    };
}
