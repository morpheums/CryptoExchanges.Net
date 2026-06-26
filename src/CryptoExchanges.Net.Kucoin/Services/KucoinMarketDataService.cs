using CryptoExchanges.Net.Kucoin.Dtos;
using CryptoExchanges.Net.Kucoin.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Kucoin.Services;

/// <summary>
/// KuCoin implementation of <see cref="IMarketDataService"/> against the V1/V2 spot REST API.
/// </summary>
internal sealed class KucoinMarketDataService(IKucoinHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : IMarketDataService
{
    // Lazily-fetched, cached snapshot of the supported symbol set, used only by the opt-in
    // IsSupportedAsync / ResolveSymbolAsync validation methods. The Lazy<Task<>> guarantees the
    // underlying GetExchangeInfoAsync call runs at most once even under concurrent first calls.
    private Lazy<Task<IReadOnlyDictionary<Symbol, Symbol>>>? _supportedSymbols;
    private readonly object _supportedSymbolsGate = new();

    private Lazy<Task<IReadOnlyDictionary<Symbol, Symbol>>> EnsureSupportedSymbols()
    {
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
        if (symbol.HasValue)
        {
            var single = new Dictionary<string, string> { ["symbol"] = mapper.ToWire(symbol.Value) };
            var oneResp = await http.GetAsync<ResponseDto<TickerDto>>("/api/v1/market/stats", single, false, ct).ConfigureAwait(false);
            if (oneResp.Data is null)
                return [];
            return [modelMapper.Map<TickerDto, Ticker>(oneResp.Data)];
        }

        var response = await http.GetAsync<ResponseDto<AllTickersDto>>("/api/v1/market/allTickers", null, false, ct).ConfigureAwait(false);
        var items = response.Data?.Ticker ?? [];

        // The full universe includes obscure/transitional pairs that may not resolve; skip those
        // rather than failing the whole batch.
        return items.SelectMany(TryMapTicker).ToList();
    }

    /// <inheritdoc />
    public async Task<OrderBook> GetOrderBookAsync(Symbol symbol, int depth = 100, CancellationToken ct = default)
    {
        // KuCoin V1 public order book: level2_20 (top 20) or level2_100 (top 100).
        // Depth >= 100 uses level2_100; anything smaller uses level2_20.
        var endpoint = depth >= 100 ? "/api/v1/market/orderbook/level2_100" : "/api/v1/market/orderbook/level2_20";
        var parameters = new Dictionary<string, string> { ["symbol"] = mapper.ToWire(symbol) };

        var response = await http.GetAsync<ResponseDto<OrderBookDto>>(endpoint, parameters, false, ct).ConfigureAwait(false);
        var book = response.Data ?? new OrderBookDto();

        var bids = book.Bids.Select(b => new OrderBookEntry(KucoinValueParsers.ParseDecimal(b[0]), KucoinValueParsers.ParseDecimal(b[1]))).ToList();
        var asks = book.Asks.Select(a => new OrderBookEntry(KucoinValueParsers.ParseDecimal(a[0]), KucoinValueParsers.ParseDecimal(a[1]))).ToList();

        var timestamp = book.Time > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(book.Time) : (DateTimeOffset?)null;
        return new OrderBook(symbol, bids, asks, timestamp);
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
            ["type"] = MapKlineInterval(interval)
        };

        // KuCoin candles use unix seconds for start/end (not ms).
        if (startTime.HasValue)
            parameters["startAt"] = startTime.Value.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["endAt"] = endTime.Value.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        // KuCoin returns candlesticks as a data array of string arrays:
        // [startTime(s), open, close, high, low, volume, quoteVolume], newest-first.
        var response = await http.GetAsync<ResponseDto<List<List<string>>>>("/api/v1/market/candles", parameters, false, ct).ConfigureAwait(false);
        var rows = response.Data ?? [];

        var candles = new List<Candlestick>();
        foreach (var arr in rows)
        {
            if (arr.Count < 7)
                continue;
            // KuCoin candle timestamp is unix SECONDS (not ms); multiply by 1000.
            var openTimeMs = KucoinValueParsers.ParseMs(arr[0]) * 1000L;
            candles.Add(new Candlestick(
                OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs),
                CloseTime: null,
                Open: KucoinValueParsers.ParseDecimal(arr[1]),
                High: KucoinValueParsers.ParseDecimal(arr[3]),
                Low: KucoinValueParsers.ParseDecimal(arr[4]),
                Close: KucoinValueParsers.ParseDecimal(arr[2]),
                Volume: KucoinValueParsers.ParseDecimal(arr[5]),
                QuoteVolume: KucoinValueParsers.ParseDecimal(arr[6]),
                TradeCount: null,
                Interval: interval,
                TradingSymbol: symbol
            ));
        }

        // KuCoin returns newest-first; reverse for chronological order and apply limit.
        candles.Reverse();
        return candles.Count > limit ? candles.GetRange(candles.Count - limit, limit) : candles;
    }

    /// <inheritdoc />
    public async Task<decimal> GetPriceAsync(Symbol symbol, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string> { ["symbol"] = mapper.ToWire(symbol) };
        var response = await http.GetAsync<ResponseDto<TickerDto>>("/api/v1/market/stats", parameters, false, ct).ConfigureAwait(false);
        return response.Data is null ? 0m : KucoinValueParsers.ParseDecimal(response.Data.Last);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Trade>> GetRecentTradesAsync(Symbol symbol, int limit = 500, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string> { ["symbol"] = mapper.ToWire(symbol) };
        var response = await http.GetAsync<ResponseDto<List<TradeDto>>>("/api/v1/market/histories", parameters, false, ct).ConfigureAwait(false);
        var trades = response.Data ?? [];

        // Trade timestamp from KuCoin histories is in nanoseconds (string).
        return trades
            .Take(Math.Max(1, limit))
            .Select(t => new Trade(
                symbol,
                t.Sequence,
                KucoinValueParsers.ParseDecimal(t.Price),
                KucoinValueParsers.ParseDecimal(t.Size),
                DateTimeOffset.FromUnixTimeMilliseconds(KucoinValueParsers.ParseNsToMs(t.Time)),
                t.Side == "sell"
            ))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync<ResponseDto<List<SymbolInfoDto>>>("/api/v2/symbols", null, false, ct).ConfigureAwait(false);
        var items = response.Data ?? [];

        var representable = items
            .Where(s => Asset.TryOf(s.BaseCurrency, out _) && Asset.TryOf(s.QuoteCurrency, out _));
        var symbols = modelMapper.Map<SymbolInfoDto, SymbolInfo>(representable);

        // Populate the mapper's wire→Symbol lookup table from the freshly fetched symbols.
        mapper.UpdateSymbols(symbols);

        return new ExchangeInfo("KuCoin", symbols, []);
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
        KlineInterval.OneMinute => "1min",
        KlineInterval.ThreeMinutes => "3min",
        KlineInterval.FiveMinutes => "5min",
        KlineInterval.FifteenMinutes => "15min",
        KlineInterval.ThirtyMinutes => "30min",
        KlineInterval.OneHour => "1hour",
        KlineInterval.TwoHours => "2hour",
        KlineInterval.FourHours => "4hour",
        KlineInterval.SixHours => "6hour",
        KlineInterval.EightHours => "8hour",
        KlineInterval.TwelveHours => "12hour",
        KlineInterval.OneDay => "1day",
        KlineInterval.ThreeDays => "3day",
        KlineInterval.OneWeek => "1week",
        KlineInterval.OneMonth => "1month",
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, $"Unsupported kline interval for KuCoin: {interval}")
    };
}
