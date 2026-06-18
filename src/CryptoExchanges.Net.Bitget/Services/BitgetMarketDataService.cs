using CryptoExchanges.Net.Bitget.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Bitget.Services;

/// <summary>
/// Bitget implementation of <see cref="IMarketDataService"/> against the V2 spot REST API.
/// </summary>
internal sealed class BitgetMarketDataService(IBitgetHttpClient http, ISymbolMapper mapper, IMapper modelMapper) : IMarketDataService
{
    // Lazily-fetched, cached snapshot of the supported symbol set, used only by the opt-in
    // IsSupportedAsync / ResolveSymbolAsync validation methods (mirrors the OKX posture). The
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
            // Single-symbol path: /api/v2/spot/market/tickers?symbol=... . The caller asked for a
            // specific symbol, so an unresolvable wire string is a genuine error — let the mapper throw.
            var single = new Dictionary<string, string> { ["symbol"] = mapper.ToWire(symbol.Value) };
            var oneResp = await http.GetAsync<ResponseDto<TickerDto>>("/api/v2/spot/market/tickers", single, false, ct).ConfigureAwait(false);
            return oneResp.Data.Select(modelMapper.Map<TickerDto, Ticker>).ToList();
        }

        var response = await http.GetAsync<ResponseDto<TickerDto>>("/api/v2/spot/market/tickers", null, false, ct).ConfigureAwait(false);

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
            ["symbol"] = mapper.ToWire(symbol),
            // Bitget caps the orderbook limit at 150; clamp rather than fail the common default call path.
            ["limit"] = Math.Clamp(depth, 1, 150).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        var response = await http.GetAsync<ResponseDto<OrderBookDto>>("/api/v2/spot/market/orderbook", parameters, false, ct).ConfigureAwait(false);
        var book = response.Data.FirstOrDefault() ?? new OrderBookDto();

        // Bitget book levels are [price, size]; only price+size are used. Skip short/malformed rows
        // (fewer than 2 fields) rather than index out of range — mirrors the candle parser's guard.
        var bids = book.Bids.Where(b => b.Count >= 2).Select(b => new OrderBookEntry(BitgetValueParsers.ParseDecimal(b[0]), BitgetValueParsers.ParseDecimal(b[1]))).ToList();
        var asks = book.Asks.Where(a => a.Count >= 2).Select(a => new OrderBookEntry(BitgetValueParsers.ParseDecimal(a[0]), BitgetValueParsers.ParseDecimal(a[1]))).ToList();

        var ts = BitgetValueParsers.ParseMs(book.Ts);
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
            ["symbol"] = mapper.ToWire(symbol),
            ["granularity"] = MapKlineInterval(interval),
            // Bitget V2 candles cap limit at 1000; clamp so the IExchangeClient default (500) succeeds.
            ["limit"] = Math.Clamp(limit, 1, 1000).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        // Bitget uses startTime/endTime (epoch-ms) bounds.
        if (startTime.HasValue)
            parameters["startTime"] = startTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["endTime"] = endTime.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Bitget returns candles as a data array of string arrays:
        // [ts, open, high, low, close, baseVolume, quoteVolume, usdtVolume].
        var response = await http.GetAsync<ResponseDto<List<string>>>("/api/v2/spot/market/candles", parameters, false, ct).ConfigureAwait(false);

        var candles = new List<Candlestick>();
        foreach (var arr in response.Data)
        {
            if (arr.Count < 7)
                continue;
            candles.Add(new Candlestick(
                OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(BitgetValueParsers.ParseMs(arr[0])),
                CloseTime: null,
                Open: BitgetValueParsers.ParseDecimal(arr[1]),
                High: BitgetValueParsers.ParseDecimal(arr[2]),
                Low: BitgetValueParsers.ParseDecimal(arr[3]),
                Close: BitgetValueParsers.ParseDecimal(arr[4]),
                Volume: BitgetValueParsers.ParseDecimal(arr[5]),
                QuoteVolume: BitgetValueParsers.ParseDecimal(arr[6]),
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
        var parameters = new Dictionary<string, string> { ["symbol"] = mapper.ToWire(symbol) };
        var response = await http.GetAsync<ResponseDto<TickerDto>>("/api/v2/spot/market/tickers", parameters, false, ct).ConfigureAwait(false);
        var ticker = response.Data.FirstOrDefault();
        return ticker is null ? 0m : BitgetValueParsers.ParseDecimal(ticker.LastPr);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Trade>> GetRecentTradesAsync(Symbol symbol, int limit = 500, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = mapper.ToWire(symbol),
            // Bitget fills endpoint caps limit at 500; clamp so the default succeeds.
            ["limit"] = Math.Clamp(limit, 1, 500).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        var response = await http.GetAsync<ResponseDto<TradeDto>>("/api/v2/spot/market/fills", parameters, false, ct).ConfigureAwait(false);

        // Trade.Symbol is the caller's typed argument (already held), not resolved from the wire
        // string, so a cold mapper cache can never make this throw. IsBuyerMaker = taker sold.
        return response.Data.Select(t => new Trade(
            symbol,
            t.TradeId,
            BitgetValueParsers.ParseDecimal(t.Price),
            BitgetValueParsers.ParseDecimal(t.Size),
            DateTimeOffset.FromUnixTimeMilliseconds(BitgetValueParsers.ParseMs(t.Ts)),
            t.Side == "sell"
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync<ResponseDto<SymbolDto>>("/api/v2/spot/public/symbols", null, false, ct).ConfigureAwait(false);

        // Bitget symbols can include entries whose base/quote are not representable assets; skip
        // those rather than throw.
        var representable = response.Data
            .Where(s => Asset.TryOf(s.BaseCoin, out _) && Asset.TryOf(s.QuoteCoin, out _));
        var symbols = modelMapper.Map<SymbolDto, SymbolInfo>(representable);

        // Populate the mapper's wire->Symbol lookup table from the freshly fetched symbols.
        mapper.UpdateSymbols(symbols);

        // Bitget does not return per-endpoint rate-limit rules in symbols; the SDK's
        // ReactiveRateLimitGate handles rate limits from response headers at runtime instead.
        return new ExchangeInfo("Bitget", symbols, []);
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

    /// <summary>Maps the domain kline interval onto Bitget's <c>granularity</c> token (lower-case h/d/w).</summary>
    private static string MapKlineInterval(KlineInterval interval) => interval switch
    {
        KlineInterval.OneMinute => "1min",
        KlineInterval.ThreeMinutes => "3min",
        KlineInterval.FiveMinutes => "5min",
        KlineInterval.FifteenMinutes => "15min",
        KlineInterval.ThirtyMinutes => "30min",
        KlineInterval.OneHour => "1h",
        KlineInterval.FourHours => "4h",
        KlineInterval.SixHours => "6h",
        KlineInterval.TwelveHours => "12h",
        KlineInterval.OneDay => "1day",
        KlineInterval.ThreeDays => "3day",
        KlineInterval.OneWeek => "1week",
        KlineInterval.OneMonth => "1M",
        // Bitget V2 spot candles do not expose 2h / 8h bars.
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, $"Unsupported kline interval for Bitget: {interval}")
    };
}
