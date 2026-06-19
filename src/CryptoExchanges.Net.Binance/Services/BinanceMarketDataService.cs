using System.Text.Json.Serialization;
using CryptoExchanges.Net.Binance.Internal;
using CryptoExchanges.Net.Core.Interfaces;
using DeltaMapper;

namespace CryptoExchanges.Net.Binance.Services;

/// <summary>
/// Binance implementation of <see cref="IMarketDataService"/>.
/// </summary>
internal sealed class BinanceMarketDataService(IBinanceHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : IMarketDataService
{
    // Lazily-fetched, cached snapshot of the supported symbol set, used only by the opt-in
    // IsSupportedAsync / ResolveSymbolAsync validation methods. Lazy<Task<>> guarantees the
    // underlying GetExchangeInfoAsync call runs at most once even under concurrent first calls;
    // the resulting Task is shared. Nothing else touches this, so other methods never force a
    // fetch. Initialized lazily in EnsureSupportedSymbols() because a field initializer cannot
    // reference instance methods.
    private Lazy<Task<IReadOnlyDictionary<Symbol, Symbol>>>? _supportedSymbols;
    private readonly object _supportedSymbolsGate = new();

    private Lazy<Task<IReadOnlyDictionary<Symbol, Symbol>>> EnsureSupportedSymbols()
    {
        // Double-checked init of the Lazy itself; the Lazy then serializes the actual fetch.
        if (_supportedSymbols is not null)
            return _supportedSymbols;

        lock (_supportedSymbolsGate)
        {
            _supportedSymbols ??= new Lazy<Task<IReadOnlyDictionary<Symbol, Symbol>>>(
                async () =>
                {
                    var info = await GetExchangeInfoAsync().ConfigureAwait(false);
                    return info.Symbols.ToDictionary(s => s.Symbol, s => s.Symbol);
                },
                LazyThreadSafetyMode.ExecutionAndPublication);
            return _supportedSymbols;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Ticker>> GetTickersAsync(Symbol? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>();
        if (symbol.HasValue)
            parameters["symbol"] = mapper.ToWire(symbol.Value);
        else
            parameters["type"] = "FULL";

        List<TickerDto>? results;

        if (symbol.HasValue)
        {
            // Single symbol returns an object, not an array. The caller asked for a specific
            // symbol, so an unresolvable wire string is a genuine error — let MapTicker throw.
            var single = await http.GetAsync<TickerDto>("/api/v3/ticker/24hr", parameters, false, ct).ConfigureAwait(false);
            return [modelMapper.Map<TickerDto, Ticker>(single)];
        }

        results = await http.GetAsync<List<TickerDto>>("/api/v3/ticker/24hr", parameters, false, ct).ConfigureAwait(false);

        // The full universe includes obscure/delisted pairs that may not resolve against a
        // cold cache or any known quote suffix; skip those rather than failing the whole batch.
        // The skip is intentional for obscure/transitional symbols — callers needing a complete
        // set should first warm the wire-lookup cache via GetExchangeInfoAsync.
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

        var response = await http.GetAsync<OrderBookDto>("/api/v3/depth", parameters, false, ct).ConfigureAwait(false);

        var bids = response.Bids.Select(b => new OrderBookEntry(BinanceValueParsers.ParseDecimal(b[0]), BinanceValueParsers.ParseDecimal(b[1]))).ToList();
        var asks = response.Asks.Select(a => new OrderBookEntry(BinanceValueParsers.ParseDecimal(a[0]), BinanceValueParsers.ParseDecimal(a[1]))).ToList();

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
                Open: BinanceValueParsers.ParseDecimal(arr[1].GetString()!),
                High: BinanceValueParsers.ParseDecimal(arr[2].GetString()!),
                Low: BinanceValueParsers.ParseDecimal(arr[3].GetString()!),
                Close: BinanceValueParsers.ParseDecimal(arr[4].GetString()!),
                Volume: BinanceValueParsers.ParseDecimal(arr[5].GetString()!),
                QuoteVolume: BinanceValueParsers.ParseDecimal(arr[7].GetString()!),
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

        var response = await http.GetAsync<PriceDto>("/api/v3/ticker/price", parameters, false, ct).ConfigureAwait(false);
        return BinanceValueParsers.ParseDecimal(response.Price);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Trade>> GetRecentTradesAsync(Symbol symbol, int limit = 500, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = limit.ToString()
        };

        var results = await http.GetAsync<List<TradeDto>>("/api/v3/trades", parameters, false, ct).ConfigureAwait(false);

        return results.Select(t => new Trade(
            symbol,
            t.Id.ToString(),
            BinanceValueParsers.ParseDecimal(t.Price),
            BinanceValueParsers.ParseDecimal(t.Qty),
            DateTimeOffset.FromUnixTimeMilliseconds(t.Time),
            t.IsBuyerMaker
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync<ExchangeInfoDto>("/api/v3/exchangeInfo", null, false, ct).ConfigureAwait(false);

        // Binance exchangeInfo can include non-standard entries whose base/quote are not
        // representable assets (e.g. non-ASCII test symbols); skip those rather than throw.
        var representable = response.Symbols
            .Where(s => Asset.TryOf(s.BaseAsset, out _) && Asset.TryOf(s.QuoteAsset, out _));
        var symbols = modelMapper.Map<SymbolInfoDto, SymbolInfo>(representable);

        // Populate the mapper's wire->Symbol lookup table from the freshly fetched symbols.
        mapper.UpdateSymbols(symbols);

        var rateLimits = response.RateLimits.Select(r =>
            new RateLimit(MapRateLimitType(r.RateLimitType), MapRateLimitInterval(r.Interval), r.Limit)
        ).ToList();

        return new ExchangeInfo("Binance", symbols, rateLimits);
    }

    /// <inheritdoc />
    public async Task<bool> IsSupportedAsync(Symbol symbol, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var supported = await EnsureSupportedSymbols().Value.ConfigureAwait(false);
        return supported.ContainsKey(symbol);
    }

    /// <inheritdoc />
    public async Task<Symbol?> ResolveSymbolAsync(Symbol symbol, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var supported = await EnsureSupportedSymbols().Value.ConfigureAwait(false);
        return supported.TryGetValue(symbol, out var canonical) ? canonical : null;
    }

    /// <summary>
    /// Maps a ticker, yielding nothing when its wire symbol cannot be resolved. Used for the
    /// full-universe response where unknown/delisted pairs must not abort the whole batch.
    /// </summary>
    private IEnumerable<Ticker> TryMapTicker(TickerDto t)
    {
        Ticker ticker;
        try
        {
            ticker = modelMapper.Map<TickerDto, Ticker>(t);
        }
        catch (FormatException)
        {
            yield break;
        }

        yield return ticker;
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

}
