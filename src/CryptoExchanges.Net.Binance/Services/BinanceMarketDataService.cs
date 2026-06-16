using System.Text.Json.Serialization;

namespace CryptoExchanges.Net.Binance.Services;

// ---------------------------------------------------------------------------
//  Binance-specific DTOs (internal)
// ---------------------------------------------------------------------------

internal sealed record BinanceTickerResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("lastPrice")]
    public string LastPrice { get; init; } = "0";

    [JsonPropertyName("openPrice")]
    public string OpenPrice { get; init; } = "0";

    [JsonPropertyName("highPrice")]
    public string HighPrice { get; init; } = "0";

    [JsonPropertyName("lowPrice")]
    public string LowPrice { get; init; } = "0";

    [JsonPropertyName("volume")]
    public string Volume { get; init; } = "0";

    [JsonPropertyName("quoteVolume")]
    public string QuoteVolume { get; init; } = "0";

    [JsonPropertyName("priceChange")]
    public string PriceChange { get; init; } = "0";

    [JsonPropertyName("priceChangePercent")]
    public string PriceChangePercent { get; init; } = "0";

    [JsonPropertyName("closeTime")]
    public long CloseTime { get; init; }
}

internal sealed record BinanceOrderBookResponse
{
    [JsonPropertyName("lastUpdateId")]
    public long LastUpdateId { get; init; }

    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = [];

    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = [];
}

// BinanceKlineResponse removed — kline parsing uses raw JsonDocument.

internal sealed record BinancePriceResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";
}

internal sealed record BinanceTradeResponse
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("price")]
    public string Price { get; init; } = "0";

    [JsonPropertyName("qty")]
    public string Qty { get; init; } = "0";

    [JsonPropertyName("quoteQty")]
    public string QuoteQty { get; init; } = "0";

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("isBuyerMaker")]
    public bool IsBuyerMaker { get; init; }
}

internal sealed record BinanceExchangeInfoResponse
{
    [JsonPropertyName("symbols")]
    public List<BinanceSymbolInfo> Symbols { get; init; } = [];

    [JsonPropertyName("rateLimits")]
    public List<BinanceRateLimit> RateLimits { get; init; } = [];
}

internal sealed record BinanceSymbolInfo
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("baseAsset")]
    public string BaseAsset { get; init; } = string.Empty;

    [JsonPropertyName("quoteAsset")]
    public string QuoteAsset { get; init; } = string.Empty;

    [JsonPropertyName("orderTypes")]
    public List<string> OrderTypes { get; init; } = [];

    [JsonPropertyName("filters")]
    public List<JsonElement> Filters { get; init; } = [];
}

internal sealed record BinanceRateLimit
{
    [JsonPropertyName("rateLimitType")]
    public string RateLimitType { get; init; } = string.Empty;

    [JsonPropertyName("interval")]
    public string Interval { get; init; } = string.Empty;

    [JsonPropertyName("limit")]
    public int Limit { get; init; }
}

// ---------------------------------------------------------------------------
//  BinanceMarketDataService
// ---------------------------------------------------------------------------

/// <summary>
/// Binance implementation of <see cref="IMarketDataService"/>.
/// </summary>
internal sealed class BinanceMarketDataService(BinanceHttpClient http, ISymbolMapper mapper) : IMarketDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Ticker>> GetTickersAsync(Symbol? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>();
        if (symbol.HasValue)
            parameters["symbol"] = mapper.ToWire(symbol.Value);
        else
            parameters["type"] = "FULL";

        List<BinanceTickerResponse>? results;

        if (symbol.HasValue)
        {
            // Single symbol returns an object, not an array. The caller asked for a specific
            // symbol, so an unresolvable wire string is a genuine error — let MapTicker throw.
            var single = await http.GetAsync<BinanceTickerResponse>("/api/v3/ticker/24hr", parameters, false, ct).ConfigureAwait(false);
            results = [single];
            return results.Select(MapTicker).ToList();
        }

        results = await http.GetAsync<List<BinanceTickerResponse>>("/api/v3/ticker/24hr", parameters, false, ct).ConfigureAwait(false);

        // The full universe includes obscure/delisted pairs that may not resolve against a
        // cold cache or any known quote suffix; skip those rather than failing the whole batch.
        return results.SelectMany(TryMapTicker).ToList();
    }

    /// <inheritdoc />
    public async Task<OrderBook> GetOrderBookAsync(Symbol symbol, int depth = 100, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = depth.ToString()
        };

        var response = await http.GetAsync<BinanceOrderBookResponse>("/api/v3/depth", parameters, false, ct).ConfigureAwait(false);

        var bids = response.Bids.Select(b => new OrderBookEntry(ParseDecimal(b[0]), ParseDecimal(b[1]))).ToList();
        var asks = response.Asks.Select(a => new OrderBookEntry(ParseDecimal(a[0]), ParseDecimal(a[1]))).ToList();

        return new OrderBook(symbol, bids, asks, null, response.LastUpdateId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Candlestick>> GetCandlesticksAsync(
        Symbol symbol,
        KlineInterval interval,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        int limit = 500,
        CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["interval"] = MapKlineInterval(interval),
            ["limit"] = limit.ToString(),
            ["timeZone"] = "0" // UTC
        };

        if (startTime.HasValue)
            parameters["startTime"] = startTime.Value.ToUnixTimeMilliseconds().ToString();
        if (endTime.HasValue)
            parameters["endTime"] = endTime.Value.ToUnixTimeMilliseconds().ToString();

        // Binance returns klines as an array of arrays via JSON
        var json = await http.GetStringAsync("/api/v3/klines", parameters, false, ct).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(json);
        var candles = new List<Candlestick>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var arr = element.EnumerateArray().Select(e => e).ToList();
            if (arr.Count < 9)
                continue;
            candles.Add(new Candlestick(
                OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(arr[0].GetInt64()),
                CloseTime: DateTimeOffset.FromUnixTimeMilliseconds(arr[6].GetInt64()),
                Open: ParseDecimal(arr[1].GetString()!),
                High: ParseDecimal(arr[2].GetString()!),
                Low: ParseDecimal(arr[3].GetString()!),
                Close: ParseDecimal(arr[4].GetString()!),
                Volume: ParseDecimal(arr[5].GetString()!),
                QuoteVolume: ParseDecimal(arr[7].GetString()!),
                TradeCount: arr[8].GetInt32(),
                Interval: interval,
                TradingSymbol: symbol
            ));
        }

        return candles;
    }

    /// <inheritdoc />
    public async Task<decimal> GetPriceAsync(Symbol symbol, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol)
        };

        var response = await http.GetAsync<BinancePriceResponse>("/api/v3/ticker/price", parameters, false, ct).ConfigureAwait(false);
        return ParseDecimal(response.Price);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Trade>> GetRecentTradesAsync(Symbol symbol, int limit = 500, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = limit.ToString()
        };

        var results = await http.GetAsync<List<BinanceTradeResponse>>("/api/v3/trades", parameters, false, ct).ConfigureAwait(false);

        return results.Select(t => new Trade(
            symbol,
            t.Id.ToString(),
            ParseDecimal(t.Price),
            ParseDecimal(t.Qty),
            DateTimeOffset.FromUnixTimeMilliseconds(t.Time),
            t.IsBuyerMaker
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync<BinanceExchangeInfoResponse>("/api/v3/exchangeInfo", null, false, ct).ConfigureAwait(false);

        // Binance exchangeInfo can include non-standard entries whose base/quote are not
        // representable assets (e.g. non-ASCII test symbols); skip those rather than throw.
        var symbols = response.Symbols
            .Where(s => Asset.TryOf(s.BaseAsset, out _) && Asset.TryOf(s.QuoteAsset, out _))
            .Select(s =>
            {
                var types = s.OrderTypes.Select(ParseOrderType).ToArray();
                return new SymbolInfo(
                    Symbol: mapper.FromComponents(s.BaseAsset, s.QuoteAsset),
                    AllowedOrderTypes: types
                );
            }).ToList();

        // Populate the mapper's wire->Symbol lookup table from the freshly fetched symbols.
        if (mapper is BinanceSymbolMapper binanceMapper)
            binanceMapper.Update(symbols);

        var rateLimits = response.RateLimits.Select(r =>
            new RateLimit(MapRateLimitType(r.RateLimitType), MapRateLimitInterval(r.Interval), r.Limit)
        ).ToList();

        return new ExchangeInfo("Binance", symbols, rateLimits);
    }

    // ── Mapping helpers ──

    /// <summary>
    /// Maps a ticker, yielding nothing when its wire symbol cannot be resolved. Used for the
    /// full-universe response where unknown/delisted pairs must not abort the whole batch.
    /// </summary>
    private IEnumerable<Ticker> TryMapTicker(BinanceTickerResponse t)
    {
        Ticker ticker;
        try
        {
            ticker = MapTicker(t);
        }
        catch (FormatException)
        {
            yield break;
        }

        yield return ticker;
    }

    private Ticker MapTicker(BinanceTickerResponse t)
    {
        var sym = mapper.FromWire(t.Symbol);
        return new Ticker(
            sym,
            ParseDecimal(t.LastPrice),
            ParseDecimal(t.OpenPrice),
            ParseDecimal(t.HighPrice),
            ParseDecimal(t.LowPrice),
            ParseDecimal(t.Volume),
            ParseDecimal(t.QuoteVolume),
            ParseDecimal(t.PriceChange),
            ParseDecimal(t.PriceChangePercent),
            t.CloseTime > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(t.CloseTime) : null
        );
    }

    private static string MapKlineInterval(KlineInterval interval) => interval switch
    {
        KlineInterval.OneMinute => "1m",
        KlineInterval.ThreeMinutes => "3m",
        KlineInterval.FiveMinutes => "5m",
        KlineInterval.FifteenMinutes => "15m",
        KlineInterval.ThirtyMinutes => "30m",
        KlineInterval.OneHour => "1h",
        KlineInterval.TwoHours => "2h",
        KlineInterval.FourHours => "4h",
        KlineInterval.SixHours => "6h",
        KlineInterval.EightHours => "8h",
        KlineInterval.TwelveHours => "12h",
        KlineInterval.OneDay => "1d",
        KlineInterval.ThreeDays => "3d",
        KlineInterval.OneWeek => "1w",
        KlineInterval.OneMonth => "1M",
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, $"Unsupported kline interval: {interval}")
    };

    private static OrderType ParseOrderType(string type) => type switch
    {
        "LIMIT" => OrderType.Limit,
        "MARKET" => OrderType.Market,
        "STOP_LOSS" => OrderType.StopLoss,
        "STOP_LOSS_LIMIT" => OrderType.StopLossLimit,
        "TAKE_PROFIT" => OrderType.TakeProfit,
        "TAKE_PROFIT_LIMIT" => OrderType.TakeProfitLimit,
        "LIMIT_MAKER" => OrderType.LimitMaker,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown order type: {type}")
    };

    private static RateLimitType MapRateLimitType(string type) => type switch
    {
        "REQUEST_WEIGHT" => RateLimitType.RequestWeight,
        "ORDERS" => RateLimitType.Orders,
        "RAW_REQUESTS" => RateLimitType.RawRequests,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown rate limit type: {type}")
    };

    private static RateLimitInterval MapRateLimitInterval(string interval) => interval switch
    {
        "SECOND" => RateLimitInterval.Second,
        "MINUTE" => RateLimitInterval.Minute,
        "DAY" => RateLimitInterval.Day,
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, $"Unknown rate limit interval: {interval}")
    };

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0m;
        return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
