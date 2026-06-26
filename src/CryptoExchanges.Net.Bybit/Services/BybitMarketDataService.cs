using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Core.Interfaces;
using DeltaMapper;

namespace CryptoExchanges.Net.Bybit.Services;

/// <summary>
/// Bybit implementation of <see cref="IMarketDataService"/> against the V5 spot REST API.
/// </summary>
internal sealed class BybitMarketDataService(IBybitHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : IMarketDataService
{
    private const string SpotCategory = "spot";

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
        var parameters = new Dictionary<string, string> { ["category"] = SpotCategory };
        if (symbol.HasValue)
            parameters["symbol"] = mapper.ToWire(symbol.Value);

        var response = await http.GetAsync<ResponseDto<ListDto<TickerDto>>>("/v5/market/tickers", parameters, false, ct).ConfigureAwait(false);
        var list = response.Result?.List ?? [];

        if (symbol.HasValue)
        {
            // The caller asked for a specific symbol, so an unresolvable wire string is a genuine
            // error — let the mapper throw rather than silently dropping it.
            return list.Select(modelMapper.Map<TickerDto, Ticker>).ToList();
        }

        // The full universe includes obscure/transitional pairs that may not resolve against a
        // cold cache or any known quote suffix; skip those rather than failing the whole batch.
        // Callers needing a complete set should first warm the wire-lookup cache via GetExchangeInfoAsync.
        return list.SelectMany(TryMapTicker).ToList();
    }

    /// <inheritdoc />
    public async Task<OrderBook> GetOrderBookAsync(Symbol symbol, int depth = 100, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = depth.ToString()
        };

        var response = await http.GetAsync<ResponseDto<OrderBookDto>>("/v5/market/orderbook", parameters, false, ct).ConfigureAwait(false);
        var result = response.Result ?? new OrderBookDto();

        var bids = result.Bids.Select(b => new OrderBookEntry(BybitValueParsers.ParseDecimal(b[0]), BybitValueParsers.ParseDecimal(b[1]))).ToList();
        var asks = result.Asks.Select(a => new OrderBookEntry(BybitValueParsers.ParseDecimal(a[0]), BybitValueParsers.ParseDecimal(a[1]))).ToList();

        var timestamp = result.Timestamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(result.Timestamp) : (DateTimeOffset?)null;
        return new OrderBook(symbol, bids, asks, timestamp, result.UpdateId);
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
            ["category"] = SpotCategory,
            ["symbol"] = mapper.ToWire(symbol),
            ["interval"] = MapKlineInterval(interval),
            ["limit"] = limit.ToString()
        };

        if (startTime.HasValue)
            parameters["start"] = startTime.Value.ToUnixTimeMilliseconds().ToString();
        if (endTime.HasValue)
            parameters["end"] = endTime.Value.ToUnixTimeMilliseconds().ToString();

        // Bybit returns klines as result.list of string arrays:
        // [startTime, open, high, low, close, volume, turnover], newest-first.
        var response = await http.GetAsync<ResponseDto<ListDto<List<string>>>>("/v5/market/kline", parameters, false, ct).ConfigureAwait(false);
        var rows = response.Result?.List ?? [];

        var candles = new List<Candlestick>();
        foreach (var arr in rows)
        {
            if (arr.Count < 7)
                continue;
            candles.Add(new Candlestick(
                OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(arr[0], System.Globalization.CultureInfo.InvariantCulture)),
                CloseTime: null,
                Open: BybitValueParsers.ParseDecimal(arr[1]),
                High: BybitValueParsers.ParseDecimal(arr[2]),
                Low: BybitValueParsers.ParseDecimal(arr[3]),
                Close: BybitValueParsers.ParseDecimal(arr[4]),
                Volume: BybitValueParsers.ParseDecimal(arr[5]),
                QuoteVolume: BybitValueParsers.ParseDecimal(arr[6]),
                TradeCount: null,
                Interval: interval,
                TradingSymbol: symbol
            ));
        }

        return candles;
    }

    /// <inheritdoc />
    public async Task<decimal> GetPriceAsync(Symbol symbol, CancellationToken ct = default)
    {
        // Bybit V5 has no dedicated last-price endpoint; the spot ticker carries lastPrice.
        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = mapper.ToWire(symbol)
        };

        var response = await http.GetAsync<ResponseDto<ListDto<TickerDto>>>("/v5/market/tickers", parameters, false, ct).ConfigureAwait(false);
        var ticker = response.Result?.List.FirstOrDefault();
        return ticker is null ? 0m : BybitValueParsers.ParseDecimal(ticker.LastPrice);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Trade>> GetRecentTradesAsync(Symbol symbol, int limit = 500, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["category"] = SpotCategory,
            ["symbol"] = mapper.ToWire(symbol),
            ["limit"] = limit.ToString()
        };

        var response = await http.GetAsync<ResponseDto<ListDto<TradeDto>>>("/v5/market/recent-trade", parameters, false, ct).ConfigureAwait(false);
        var trades = response.Result?.List ?? [];

        // Trade.Symbol is the caller's typed argument (already held), not resolved from the wire
        // string, so a cold mapper cache can never make this throw. IsBuyerMaker = taker sold.
        return trades.Select(t => new Trade(
            symbol,
            t.ExecId,
            BybitValueParsers.ParseDecimal(t.Price),
            BybitValueParsers.ParseDecimal(t.Size),
            DateTimeOffset.FromUnixTimeMilliseconds(t.Time),
            t.Side == "Sell"
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string> { ["category"] = SpotCategory };
        var response = await http.GetAsync<ResponseDto<ListDto<SymbolInfoDto>>>("/v5/market/instruments-info", parameters, false, ct).ConfigureAwait(false);
        var instruments = response.Result?.List ?? [];

        // Bybit instruments can include entries whose base/quote are not representable assets;
        // skip those rather than throw.
        var representable = instruments
            .Where(s => Asset.TryOf(s.BaseCoin, out _) && Asset.TryOf(s.QuoteCoin, out _));
        var symbols = modelMapper.Map<SymbolInfoDto, SymbolInfo>(representable);

        // Populate the mapper's wire->Symbol lookup table from the freshly fetched symbols.
        mapper.UpdateSymbols(symbols);

        // Bybit V5 does not return per-endpoint rate-limit rules in instruments-info; the SDK's
        // ReactiveRateLimitGate handles rate limits from response headers at runtime instead.
        return new ExchangeInfo("Bybit", symbols, []);
    }

    /// <inheritdoc />
    public async Task<bool> IsSupportedAsync(Symbol symbol, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var supported = await EnsureSupportedSymbols().Value.WaitAsync(ct).ConfigureAwait(false);
        return supported.ContainsKey(symbol);
    }

    /// <inheritdoc />
    public async Task<Symbol?> ResolveSymbolAsync(Symbol symbol, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var supported = await EnsureSupportedSymbols().Value.WaitAsync(ct).ConfigureAwait(false);
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
        KlineInterval.OneMinute => "1",
        KlineInterval.ThreeMinutes => "3",
        KlineInterval.FiveMinutes => "5",
        KlineInterval.FifteenMinutes => "15",
        KlineInterval.ThirtyMinutes => "30",
        KlineInterval.OneHour => "60",
        KlineInterval.TwoHours => "120",
        KlineInterval.FourHours => "240",
        KlineInterval.SixHours => "360",
        KlineInterval.TwelveHours => "720",
        KlineInterval.OneDay => "D",
        KlineInterval.OneWeek => "W",
        KlineInterval.OneMonth => "M",
        // Bybit V5 spot klines do not expose 8h or 3d intervals.
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, $"Unsupported kline interval for Bybit: {interval}")
    };
}
