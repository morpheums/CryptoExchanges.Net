using System.Globalization;
using CryptoExchanges.Net.Kraken.Dtos;
using CryptoExchanges.Net.Kraken.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Kraken.Services;

/// <summary>Kraken implementation of <see cref="IMarketDataService"/> against the Spot REST API.</summary>
internal sealed class KrakenMarketDataService : IMarketDataService
{
    private readonly KrakenHttpClient _http;
    private readonly ISymbolMapper _symbolMapper;
    private readonly IMapper _modelMapper;

    // Lazy-populated supported-symbol set, used only by the opt-in IsSupportedAsync /
    // ResolveSymbolAsync methods (backed by a single GetExchangeInfoAsync call).
    private Lazy<Task<IReadOnlyDictionary<Symbol, Symbol>>>? _supportedSymbols;
    private readonly object _supportedSymbolsGate = new();

    public KrakenMarketDataService(KrakenHttpClient http, ISymbolMapper symbolMapper, IMapper modelMapper)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(symbolMapper);
        ArgumentNullException.ThrowIfNull(modelMapper);
        _http = http;
        _symbolMapper = symbolMapper;
        _modelMapper = modelMapper;
    }

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
            // Single-symbol path: /0/public/Ticker?pair=... — result is an object keyed by the wire pair.
            var wire = _http.ToWire(symbol.Value);
            var parameters = new Dictionary<string, string> { ["pair"] = wire };
            var response = await _http.GetAsync<ResponseDto<Dictionary<string, TickerDto>>>(
                "/0/public/Ticker", parameters, false, ct).ConfigureAwait(false);
            var result = response.Result ?? [];

            var tickers = new List<Ticker>();
            foreach (var (_, dto) in result)
            {
                var ticker = _modelMapper.Map<TickerDto, Ticker>(dto);
                // Ticker.Symbol is ignored by the profile (symbol is set by caller via the positional arrays).
                tickers.Add(ticker with { Symbol = symbol.Value });
            }

            return tickers;
        }

        // All-pairs path: /0/public/Ticker with no pair param is not supported by Kraken;
        // fall back to the instruments list to enumerate all pairs and skip unresolvable ones.
        var info = await GetExchangeInfoAsync(ct).ConfigureAwait(false);
        var allTickers = new List<Ticker>(info.Symbols.Count);
        foreach (var symbolInfo in info.Symbols)
        {
            var singleWire = _http.ToWire(symbolInfo.Symbol);
            var p = new Dictionary<string, string> { ["pair"] = singleWire };
            try
            {
                var r = await _http.GetAsync<ResponseDto<Dictionary<string, TickerDto>>>(
                    "/0/public/Ticker", p, false, ct).ConfigureAwait(false);
                var resultMap = r.Result ?? [];
                foreach (var (_, dto) in resultMap)
                    allTickers.Add(_modelMapper.Map<TickerDto, Ticker>(dto) with { Symbol = symbolInfo.Symbol });
            }
            catch (Core.Exceptions.ExchangeException)
            {
                // Skip pairs that are no longer active or cause errors.
            }
        }

        return allTickers;
    }

    /// <inheritdoc />
    public async Task<OrderBook> GetOrderBookAsync(Symbol symbol, int depth = 100, CancellationToken ct = default)
    {
        var wire = _http.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["pair"] = wire,
            // Kraken caps count at 500; clamp so the default (100) succeeds without truncation.
            ["count"] = Math.Clamp(depth, 1, 500).ToString(CultureInfo.InvariantCulture)
        };

        var response = await _http.GetAsync<ResponseDto<Dictionary<string, OrderBookDto>>>(
            "/0/public/Depth", parameters, false, ct).ConfigureAwait(false);
        var result = response.Result ?? [];

        // Kraken returns result keyed by the pair name; grab the first (and only) value.
        var book = result.Values.FirstOrDefault() ?? new OrderBookDto();

        var bids = book.Bids.Select(b => new OrderBookEntry(
            KrakenValueParsers.ParseDecimal(KrakenValueParsers.GetArrayString(b, 0)),
            KrakenValueParsers.ParseDecimal(KrakenValueParsers.GetArrayString(b, 1))
        )).ToList();

        var asks = book.Asks.Select(a => new OrderBookEntry(
            KrakenValueParsers.ParseDecimal(KrakenValueParsers.GetArrayString(a, 0)),
            KrakenValueParsers.ParseDecimal(KrakenValueParsers.GetArrayString(a, 1))
        )).ToList();

        // Kraken order book entries have a timestamp at index 2 (unix seconds as a number).
        var ts = book.Bids.Count > 0 ? KrakenValueParsers.GetArrayLong(book.Bids[0], 2) : 0L;
        var timestamp = ts > 0 ? DateTimeOffset.FromUnixTimeSeconds(ts) : (DateTimeOffset?)null;
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
        var wire = _http.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["pair"] = wire,
            // Kraken OHLC uses integer minute intervals.
            ["interval"] = MapKlineIntervalMinutes(interval).ToString(CultureInfo.InvariantCulture)
        };

        if (startTime.HasValue)
            parameters["since"] = startTime.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        // Kraken OHLC result is keyed by pair name; rows are positional arrays: [time, open, high,
        // low, close, vwap, volume, count].
        var response = await _http.GetAsync<ResponseDto<Dictionary<string, JsonElement>>>(
            "/0/public/OHLC", parameters, false, ct).ConfigureAwait(false);
        var result = response.Result ?? [];

        var candles = new List<Candlestick>();
        foreach (var (key, value) in result)
        {
            // The result also contains a "last" key (next cursor); skip non-array values.
            if (key == "last" || value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var row in value.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array)
                    continue;

                var elements = row.EnumerateArray().ToList();
                if (elements.Count < 7)
                    continue;

                var dto = new CandlestickDto
                {
                    OpenTime = elements[0].ValueKind == JsonValueKind.Number ? elements[0].GetInt64() : 0L,
                    Open     = elements[1].ValueKind == JsonValueKind.String ? elements[1].GetString() ?? "0" : elements[1].GetRawText(),
                    High     = elements[2].ValueKind == JsonValueKind.String ? elements[2].GetString() ?? "0" : elements[2].GetRawText(),
                    Low      = elements[3].ValueKind == JsonValueKind.String ? elements[3].GetString() ?? "0" : elements[3].GetRawText(),
                    Close    = elements[4].ValueKind == JsonValueKind.String ? elements[4].GetString() ?? "0" : elements[4].GetRawText(),
                    Vwap     = elements[5].ValueKind == JsonValueKind.String ? elements[5].GetString() ?? "0" : elements[5].GetRawText(),
                    Volume   = elements[6].ValueKind == JsonValueKind.String ? elements[6].GetString() ?? "0" : elements[6].GetRawText(),
                    Count    = elements.Count > 7 && elements[7].ValueKind == JsonValueKind.Number ? elements[7].GetInt32() : 0
                };

                var candle = _modelMapper.Map<CandlestickDto, Candlestick>(dto) with
                {
                    Interval = interval,
                    TradingSymbol = symbol
                };
                candles.Add(candle);

                if (candles.Count >= limit)
                    break;
            }

            if (candles.Count >= limit)
                break;
        }

        return candles;
    }

    /// <inheritdoc />
    public async Task<decimal> GetPriceAsync(Symbol symbol, CancellationToken ct = default)
    {
        var wire = _http.ToWire(symbol);
        var parameters = new Dictionary<string, string> { ["pair"] = wire };
        var response = await _http.GetAsync<ResponseDto<Dictionary<string, TickerDto>>>(
            "/0/public/Ticker", parameters, false, ct).ConfigureAwait(false);
        var result = response.Result ?? [];
        var dto = result.Values.FirstOrDefault();
        return dto is null ? 0m : KrakenValueParsers.ParseDecimal(dto.Close.Count > 0 ? dto.Close[0] : "0");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Trade>> GetRecentTradesAsync(Symbol symbol, int limit = 500, CancellationToken ct = default)
    {
        var wire = _http.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["pair"] = wire,
            ["count"] = Math.Clamp(limit, 1, KrakenRequestValidation.MaxTradesLimit).ToString(CultureInfo.InvariantCulture)
        };

        // Kraken Trades result is keyed by pair name; rows are positional arrays deserialized into TradeDto.
        var response = await _http.GetAsync<ResponseDto<Dictionary<string, JsonElement>>>(
            "/0/public/Trades", parameters, false, ct).ConfigureAwait(false);
        var result = response.Result ?? [];

        var trades = new List<Trade>();
        foreach (var (key, value) in result)
        {
            if (key == "last" || value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var row in value.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array)
                    continue;

                var elements = row.EnumerateArray().ToList();
                if (elements.Count < 5)
                    continue;

                // Positional: [price, volume, time, side, orderType, misc, tradeId]
                var price  = GetString(elements, 0);
                var volume = GetString(elements, 1);
                var time   = GetDecimal(elements, 2);
                var side   = GetString(elements, 3);
                var tradeId = elements.Count > 6 ? GetString(elements, 6) : string.Empty;

                var ms = KrakenValueParsers.ParseFractionalSecondsToMs(time);
                // Kraken trade side: 'b' = buy (taker bought), 's' = sell (taker sold).
                // IsBuyerMaker = taker side is sell (the buyer was the maker).
                var isBuyerMaker = side == "s";

                trades.Add(new Trade(
                    symbol,
                    tradeId,
                    KrakenValueParsers.ParseDecimal(price),
                    KrakenValueParsers.ParseDecimal(volume),
                    DateTimeOffset.FromUnixTimeMilliseconds(ms),
                    isBuyerMaker
                ));
            }

            break; // Only one pair key in the result.
        }

        return trades;
    }

    /// <inheritdoc />
    public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync<ResponseDto<Dictionary<string, SymbolInfoDto>>>(
            "/0/public/AssetPairs", signed: false, ct: ct).ConfigureAwait(false);
        var result = response.Result ?? [];

        // Build the warm legacy→wsname table (ADR-010-006). Legacy codes (e.g. XXBTZUSD) map to
        // wsname slash-form (e.g. XBT/USD). The table is used by ToWire for subsequent symbol lookups.
        var legacyTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var representable = new List<SymbolInfoDto>();

        foreach (var (legacyCode, dto) in result)
        {
            if (string.IsNullOrEmpty(dto.Wsname))
                continue;
            // Map legacy code → wsname (e.g. XXBTZUSD → XBT/USD).
            legacyTable[legacyCode] = dto.Wsname;

            if (Asset.TryOf(ExtractBase(dto), out _) && Asset.TryOf(ExtractQuote(dto), out _))
                representable.Add(dto);
        }

        _http.UpdateLegacyTable(legacyTable);

        var symbols = _modelMapper.Map<SymbolInfoDto, SymbolInfo>(representable);
        _symbolMapper.UpdateSymbols(symbols);

        return new ExchangeInfo("Kraken", symbols, []);
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

    // Kraken OHLC interval is an integer number of minutes.
    private static int MapKlineIntervalMinutes(KlineInterval interval) => interval switch
    {
        KlineInterval.OneMinute      => 1,
        KlineInterval.FiveMinutes    => 5,
        KlineInterval.FifteenMinutes => 15,
        KlineInterval.ThirtyMinutes  => 30,
        KlineInterval.OneHour        => 60,
        KlineInterval.FourHours      => 240,
        KlineInterval.OneDay         => 1440,
        KlineInterval.OneWeek        => 10080,
        KlineInterval.OneMonth       => 21600,
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, $"Unsupported kline interval for Kraken: {interval}")
    };

    // Kraken wsname base is the portion before '/' (e.g. "XBT" from "XBT/USD").
    private static string ExtractBase(SymbolInfoDto dto)
    {
        if (string.IsNullOrEmpty(dto.Wsname))
            return dto.Base;
        var slash = dto.Wsname.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 ? dto.Wsname[..slash] : dto.Base;
    }

    // Kraken wsname quote is the portion after '/' (e.g. "USD" from "XBT/USD").
    private static string ExtractQuote(SymbolInfoDto dto)
    {
        if (string.IsNullOrEmpty(dto.Wsname))
            return dto.Quote;
        var slash = dto.Wsname.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 ? dto.Wsname[(slash + 1)..] : dto.Quote;
    }

    private static string GetString(List<JsonElement> elements, int index)
    {
        if (index >= elements.Count)
            return string.Empty;
        var el = elements[index];
        return el.ValueKind == JsonValueKind.String ? el.GetString() ?? string.Empty : el.GetRawText();
    }

    private static decimal GetDecimal(List<JsonElement> elements, int index)
    {
        var raw = GetString(elements, index);
        if (string.IsNullOrEmpty(raw))
            return 0m;
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }
}
