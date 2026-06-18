using CryptoExchanges.Net.Okx.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Okx.Services;

// ---------------------------------------------------------------------------
//  OkxMarketDataService
// ---------------------------------------------------------------------------

/// <summary>
/// OKX implementation of <see cref="IMarketDataService"/> against the V5 spot REST API.
/// </summary>
internal sealed class OkxMarketDataService(IOkxHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : IMarketDataService
{
    // Lazily-fetched, cached snapshot of the supported symbol set, used only by the opt-in
    // IsSupportedAsync / ResolveSymbolAsync validation methods (mirrors the Bybit posture). The
    // Lazy<Task<>> guarantees the underlying GetExchangeInfoAsync call runs at most once even under
    // concurrent first calls; nothing else touches this, so other methods never force a fetch.
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
            // Single-symbol path: /api/v5/market/ticker?instId=... . The caller asked for a specific
            // symbol, so an unresolvable wire string is a genuine error — let the mapper throw.
            var single = new Dictionary<string, string> { ["instId"] = mapper.ToWire(symbol.Value) };
            var oneResp = await http.GetAsync<OkxResponse<OkxTicker>>("/api/v5/market/ticker", single, false, ct).ConfigureAwait(false);
            return oneResp.Data.Select(modelMapper.Map<OkxTicker, Ticker>).ToList();
        }

        var parameters = new Dictionary<string, string> { ["instType"] = OkxRequestValidation.SpotInstType };
        var response = await http.GetAsync<OkxResponse<OkxTicker>>("/api/v5/market/tickers", parameters, false, ct).ConfigureAwait(false);

        // The full universe includes obscure/transitional pairs that may not resolve against a cold
        // cache or any known quote suffix; skip those rather than failing the whole batch. Callers
        // needing a complete set should first warm the wire-lookup cache via GetExchangeInfoAsync.
        return response.Data.SelectMany(TryMapTicker).ToList();
    }

    /// <inheritdoc />
    public async Task<OrderBook> GetOrderBookAsync(Symbol symbol, int depth = 100, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["instId"] = mapper.ToWire(symbol),
            // OKX caps the books depth at 400; clamp rather than fail the common default call path.
            ["sz"] = Math.Clamp(depth, 1, 400).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        var response = await http.GetAsync<OkxResponse<OkxOrderBook>>("/api/v5/market/books", parameters, false, ct).ConfigureAwait(false);
        var book = response.Data.FirstOrDefault() ?? new OkxOrderBook();

        // OKX book levels are [price, size, deprecatedField, numOrders]; only price+size are used.
        var bids = book.Bids.Select(b => new OrderBookEntry(OkxValueParsers.ParseDecimal(b[0]), OkxValueParsers.ParseDecimal(b[1]))).ToList();
        var asks = book.Asks.Select(a => new OrderBookEntry(OkxValueParsers.ParseDecimal(a[0]), OkxValueParsers.ParseDecimal(a[1]))).ToList();

        var ts = OkxValueParsers.ParseMs(book.Ts);
        var timestamp = ts > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ts) : (DateTimeOffset?)null;
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
            ["instId"] = mapper.ToWire(symbol),
            ["bar"] = MapKlineInterval(interval),
            // OKX V5 candles cap limit at 100; clamp so the IExchangeClient default (500) succeeds.
            ["limit"] = Math.Min(limit, OkxRequestValidation.MaxHistoryLimit).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        // OKX uses 'before'/'after' (exclusive) cursors keyed by ts; expose start/end as those bounds.
        if (startTime.HasValue)
            parameters["after"] = startTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["before"] = endTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        // OKX returns candles as a data array of string arrays:
        // [ts, open, high, low, close, vol, volCcy, volCcyQuote, confirm], newest-first.
        var response = await http.GetAsync<OkxResponse<List<string>>>("/api/v5/market/candles", parameters, false, ct).ConfigureAwait(false);

        var candles = new List<Candlestick>();
        foreach (var arr in response.Data)
        {
            if (arr.Count < 7)
                continue;
            candles.Add(new Candlestick(
                OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(OkxValueParsers.ParseMs(arr[0])),
                CloseTime: null,
                Open: OkxValueParsers.ParseDecimal(arr[1]),
                High: OkxValueParsers.ParseDecimal(arr[2]),
                Low: OkxValueParsers.ParseDecimal(arr[3]),
                Close: OkxValueParsers.ParseDecimal(arr[4]),
                Volume: OkxValueParsers.ParseDecimal(arr[5]),
                QuoteVolume: OkxValueParsers.ParseDecimal(arr[6]),
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
        var parameters = new Dictionary<string, string> { ["instId"] = mapper.ToWire(symbol) };
        var response = await http.GetAsync<OkxResponse<OkxTicker>>("/api/v5/market/ticker", parameters, false, ct).ConfigureAwait(false);
        var ticker = response.Data.FirstOrDefault();
        return ticker is null ? 0m : OkxValueParsers.ParseDecimal(ticker.Last);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Trade>> GetRecentTradesAsync(Symbol symbol, int limit = 500, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["instId"] = mapper.ToWire(symbol),
            // OKX trades endpoint caps limit at 500; clamp so the default succeeds.
            ["limit"] = Math.Clamp(limit, 1, 500).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        var response = await http.GetAsync<OkxResponse<OkxTrade>>("/api/v5/market/trades", parameters, false, ct).ConfigureAwait(false);

        // Trade.Symbol is the caller's typed argument (already held), not resolved from the wire
        // string, so a cold mapper cache can never make this throw. IsBuyerMaker = taker sold.
        return response.Data.Select(t => new Trade(
            symbol,
            t.TradeId,
            OkxValueParsers.ParseDecimal(t.Px),
            OkxValueParsers.ParseDecimal(t.Sz),
            DateTimeOffset.FromUnixTimeMilliseconds(OkxValueParsers.ParseMs(t.Ts)),
            t.Side == "sell"
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string> { ["instType"] = OkxRequestValidation.SpotInstType };
        var response = await http.GetAsync<OkxResponse<OkxInstrument>>("/api/v5/public/instruments", parameters, false, ct).ConfigureAwait(false);

        // OKX instruments can include entries whose base/quote are not representable assets; skip
        // those rather than throw.
        var representable = response.Data
            .Where(s => Asset.TryOf(s.BaseCcy, out _) && Asset.TryOf(s.QuoteCcy, out _));
        var symbols = modelMapper.Map<OkxInstrument, SymbolInfo>(representable);

        // Populate the mapper's wire->Symbol lookup table from the freshly fetched symbols.
        mapper.UpdateSymbols(symbols);

        // OKX does not return per-endpoint rate-limit rules in instruments; the SDK's
        // ReactiveRateLimitGate handles rate limits from response headers at runtime instead.
        return new ExchangeInfo("OKX", symbols, []);
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

    // ── Mapping helpers ──

    /// <summary>
    /// Maps a ticker, yielding nothing when its wire symbol cannot be resolved. Used for the
    /// full-universe response where unknown/delisted pairs must not abort the whole batch.
    /// </summary>
    private IEnumerable<Ticker> TryMapTicker(OkxTicker t)
    {
        Ticker ticker;
        try
        {
            ticker = modelMapper.Map<OkxTicker, Ticker>(t);
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
        KlineInterval.OneHour => "1H",
        KlineInterval.TwoHours => "2H",
        KlineInterval.FourHours => "4H",
        // OKX expresses the >= 6h / day / week / month bars in UTC (the 'Hutc'/day suffix forms).
        KlineInterval.SixHours => "6Hutc",
        KlineInterval.TwelveHours => "12Hutc",
        KlineInterval.OneDay => "1Dutc",
        KlineInterval.ThreeDays => "3Dutc",
        KlineInterval.OneWeek => "1Wutc",
        KlineInterval.OneMonth => "1Mutc",
        // OKX V5 spot candles do not expose an 8h bar.
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, $"Unsupported kline interval for OKX: {interval}")
    };
}
