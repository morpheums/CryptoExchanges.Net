using CryptoExchanges.Net.Coinbase.Dtos;
using CryptoExchanges.Net.Coinbase.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Coinbase.Services;

/// <summary>
/// Coinbase Advanced Trade implementation of <see cref="IMarketDataService"/> against the V3 brokerage REST API.
/// </summary>
internal sealed class CoinbaseMarketDataService(ICoinbaseHttpClient http, ISymbolMapper symbolMapper, IMapper modelMapper) : IMarketDataService
{
    // Lazily-fetched, cached snapshot of the supported symbol set; runs at most once under concurrent first calls.
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
            var wire = symbolMapper.ToWire(symbol.Value);
            var single = await http.GetAsync<TickerDto>($"/api/v3/brokerage/products/{Uri.EscapeDataString(wire)}", signed: false, ct: ct).ConfigureAwait(false);
            return [modelMapper.Map<TickerDto, Ticker>(single)];
        }

        var response = await http.GetAsync<TickerListResponseDto>("/api/v3/brokerage/products", signed: false, ct: ct).ConfigureAwait(false);
        // The full universe can include pairs that don't resolve; skip those rather than failing the whole batch.
        return response.Products.SelectMany(TryMapTicker).ToList();
    }

    /// <inheritdoc />
    public async Task<OrderBook> GetOrderBookAsync(Symbol symbol, int depth = 100, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["product_id"] = symbolMapper.ToWire(symbol),
            ["limit"] = Math.Clamp(depth, 1, 1000).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        var response = await http.GetAsync<OrderBookResponseDto>("/api/v3/brokerage/product_book", parameters, false, ct).ConfigureAwait(false);
        var book = response.Pricebook;

        var bids = book.Bids.Select(b => new OrderBookEntry(CoinbaseValueParsers.ParseDecimal(b.Price), CoinbaseValueParsers.ParseDecimal(b.Size))).ToList();
        var asks = book.Asks.Select(a => new OrderBookEntry(CoinbaseValueParsers.ParseDecimal(a.Price), CoinbaseValueParsers.ParseDecimal(a.Size))).ToList();

        var timestamp = CoinbaseValueParsers.ParseRfc3339ToTimestamp(book.Time);
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
        // Coinbase caps candles at 350/call; clamp so the IExchangeClient default (500) succeeds.
        var clampedLimit = Math.Min(limit, CoinbaseRequestValidation.MaxCandleLimit);
        CoinbaseRequestValidation.ValidateCandleWindow(clampedLimit, startTime, endTime);

        var wire = symbolMapper.ToWire(symbol);
        var parameters = new Dictionary<string, string>
        {
            ["granularity"] = MapKlineInterval(interval),
            ["limit"] = clampedLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (startTime.HasValue)
            parameters["start"] = startTime.Value.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (endTime.HasValue)
            parameters["end"] = endTime.Value.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        var response = await http.GetAsync<CandlesResponseDto>(
            $"/api/v3/brokerage/products/{Uri.EscapeDataString(wire)}/candles", parameters, false, ct)
            .ConfigureAwait(false);

        return response.Candles.Select(c =>
        {
            var mapped = modelMapper.Map<CandlestickDto, Candlestick>(c);
            // DeltaMapper leaves Interval and TradingSymbol unset (no source field); populate them here.
            return mapped with { Interval = interval, TradingSymbol = symbol };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<decimal> GetPriceAsync(Symbol symbol, CancellationToken ct = default)
    {
        var wire = symbolMapper.ToWire(symbol);
        var product = await http.GetAsync<TickerDto>($"/api/v3/brokerage/products/{Uri.EscapeDataString(wire)}", signed: false, ct: ct).ConfigureAwait(false);
        return CoinbaseValueParsers.ParseDecimal(product.Price);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Trade>> GetRecentTradesAsync(Symbol symbol, int limit = 500, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["product_id"] = symbolMapper.ToWire(symbol),
            ["limit"] = Math.Clamp(limit, 1, CoinbaseRequestValidation.MaxTradesLimit).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        var response = await http.GetAsync<TradesResponseDto>("/api/v3/brokerage/market/market-trades", parameters, false, ct).ConfigureAwait(false);

        // Trade.Symbol is the caller's typed argument; no wire-string resolution needed here.
        return response.Trades.Select(t => new Trade(
            symbol,
            t.TradeId,
            CoinbaseValueParsers.ParseDecimal(t.Price),
            CoinbaseValueParsers.ParseDecimal(t.Size),
            CoinbaseValueParsers.ParseRfc3339ToTimestamp(t.Time) ?? DateTimeOffset.MinValue,
            t.Side == "SELL" // Coinbase SELL taker means the buyer was the maker
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync<ProductsResponseDto>("/api/v3/brokerage/products", signed: false, ct: ct).ConfigureAwait(false);

        // Products can include entries whose base/quote are not representable assets; skip those.
        var representable = response.Products
            .Where(s => Asset.TryOf(s.BaseCurrencyId, out _) && Asset.TryOf(s.QuoteCurrencyId, out _));
        var symbols = modelMapper.Map<SymbolInfoDto, SymbolInfo>(representable);

        symbolMapper.UpdateSymbols(symbols);

        return new ExchangeInfo("Coinbase", symbols, []);
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

    /// <summary>Maps a ticker, yielding nothing when its wire symbol cannot be resolved.</summary>
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
        KlineInterval.OneMinute => "ONE_MINUTE",
        KlineInterval.FiveMinutes => "FIVE_MINUTE",
        KlineInterval.FifteenMinutes => "FIFTEEN_MINUTE",
        KlineInterval.ThirtyMinutes => "THIRTY_MINUTE",
        KlineInterval.OneHour => "ONE_HOUR",
        KlineInterval.TwoHours => "TWO_HOUR",
        KlineInterval.SixHours => "SIX_HOUR",
        KlineInterval.OneDay => "ONE_DAY",
        // Coinbase V3 does not expose 3m, 4h, 12h, 3d, 1w, 1M granularities.
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, $"Unsupported kline interval for Coinbase: {interval}")
    };
}
