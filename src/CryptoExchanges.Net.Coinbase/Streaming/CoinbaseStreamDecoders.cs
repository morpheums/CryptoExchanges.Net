using CryptoExchanges.Net.Coinbase.Dtos.Streaming;
using CryptoExchanges.Net.Coinbase.Internal;
using CryptoExchanges.Net.Http.Streaming;
using DeltaMapper;

namespace CryptoExchanges.Net.Coinbase.Streaming;

/// <summary>
/// Builds the <see cref="StreamDecoderRegistry"/> for the Coinbase Advanced Trade public WebSocket streams.
/// Each closure unwraps the outer <c>events</c> array before deserializing the leaf DTO and mapping
/// to <c>Core.Models</c> — Coinbase wraps all push data in <c>{"channel":"...","events":[{...}]}</c>.
/// </summary>
internal static class CoinbaseStreamDecoders
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = false
    };

    /// <summary>
    /// Builds a fully-populated <see cref="StreamDecoderRegistry"/> with decoders for all four
    /// stream kinds, capturing the provided mapper and symbol mapper as closures.
    /// </summary>
    /// <param name="mapper">The keyed DeltaMapper instance from <c>AddCoinbaseStreams</c>.</param>
    /// <param name="symbolMapper">The bespoke keyed symbol mapper from <c>AddCoinbaseStreams</c>.</param>
    /// <returns>A registry ready for injection into the engine.</returns>
    public static StreamDecoderRegistry Build(IMapper mapper, ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(symbolMapper);

        var registry = new StreamDecoderRegistry();

        registry.Register(StreamKind.Ticker, bytes =>
        {
            // Real Coinbase ticker frame: events[0].tickers[0] holds the data.
            var dto = DeserializeFirstItemInNestedArray<StreamTickerDto>(bytes, "tickers"u8)!;
            var symbol = symbolMapper.FromWire(dto.ProductId);
            return new Ticker(
                Symbol: symbol,
                LastPrice: CoinbaseValueParsers.ParseDecimal(dto.Price),
                OpenPrice: null,
                HighPrice: CoinbaseValueParsers.ParseDecimal(dto.High24h),
                LowPrice: CoinbaseValueParsers.ParseDecimal(dto.Low24h),
                Volume: CoinbaseValueParsers.ParseDecimal(dto.Volume24h),
                QuoteVolume: CoinbaseValueParsers.ParseDecimal(dto.Volume24hUsd),
                PriceChange: null,
                PriceChangePercent: CoinbaseValueParsers.ParseDecimal(dto.PricePercentChg24h),
                Timestamp: CoinbaseValueParsers.ParseRfc3339ToTimestamp(dto.Time));
        });

        registry.Register(StreamKind.Trade, bytes =>
        {
            // market_trades events embed a "trades" array; take the last entry (newest trade).
            var dto = DeserializeLastTrade(bytes)!;
            return new Trade(
                Symbol: symbolMapper.FromWire(dto.ProductId),
                Id: dto.TradeId,
                Price: CoinbaseValueParsers.ParseDecimal(dto.Price),
                Quantity: CoinbaseValueParsers.ParseDecimal(dto.Size),
                Timestamp: CoinbaseValueParsers.ParseRfc3339ToTimestamp(dto.Time),
                // Coinbase "side" is the taker side: SELL taker ⇒ buyer is the maker.
                IsBuyerMaker: string.Equals(dto.Side, "SELL", StringComparison.Ordinal));
        });

        registry.Register(StreamKind.OrderBook, bytes =>
        {
            var dto = DeserializeFirstEvent<StreamDepthDto>(bytes)!;
            var symbol = symbolMapper.FromWire(dto.ProductId);
            var bids = new List<OrderBookEntry>();
            var asks = new List<OrderBookEntry>();
            foreach (var update in dto.Updates)
            {
                var entry = new OrderBookEntry(
                    CoinbaseValueParsers.ParseDecimal(update.PriceLevel),
                    CoinbaseValueParsers.ParseDecimal(update.NewQuantity));
                if (string.Equals(update.Side, "bid", StringComparison.Ordinal))
                    bids.Add(entry);
                else
                    asks.Add(entry);
            }
            return new OrderBook(symbol, bids, asks, null, null);
        });

        registry.Register(StreamKind.Kline, bytes =>
        {
            // candles events embed a "candles" array; take the first candle.
            var dto = DeserializeFirstCandle(bytes)!;
            return new Candlestick(
                OpenTime: CoinbaseValueParsers.ParseUnixSecondsToTimestamp(dto.Start) ?? DateTimeOffset.MinValue,
                CloseTime: null,
                Open: CoinbaseValueParsers.ParseDecimal(dto.Open),
                High: CoinbaseValueParsers.ParseDecimal(dto.High),
                Low: CoinbaseValueParsers.ParseDecimal(dto.Low),
                Close: CoinbaseValueParsers.ParseDecimal(dto.Close),
                Volume: CoinbaseValueParsers.ParseDecimal(dto.Volume),
                QuoteVolume: null,
                TradeCount: null,
                Interval: KlineInterval.OneMinute);
        });

        return registry;
    }

    // Unwraps the outer events array, navigates into a named nested array, and deserializes the first element as T.
    // Used for channels (e.g. ticker) where real Coinbase embeds data in events[0].<arrayName>[0].
    private static T? DeserializeFirstItemInNestedArray<T>(ReadOnlyMemory<byte> frame, ReadOnlySpan<byte> arrayPropertyName)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("events"u8, out var events) ||
            events.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"Coinbase push frame missing or non-array 'events'; cannot decode {typeof(T).Name}.");
        var evtEnum = events.EnumerateArray();
        if (!evtEnum.MoveNext())
            throw new InvalidOperationException(
                $"Coinbase 'events' array is empty; cannot decode {typeof(T).Name}.");
        var firstEvent = evtEnum.Current;
        if (!firstEvent.TryGetProperty(arrayPropertyName, out var nested) ||
            nested.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"Coinbase event missing nested array; cannot decode {typeof(T).Name}.");
        var innerEnum = nested.EnumerateArray();
        if (!innerEnum.MoveNext())
            throw new InvalidOperationException(
                $"Coinbase nested array is empty; cannot decode {typeof(T).Name}.");
        return innerEnum.Current.Deserialize<T>(JsonOpts);
    }

    // Unwraps the outer events array and deserializes the first element as T.
    private static T? DeserializeFirstEvent<T>(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("events"u8, out var events) ||
            events.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"Coinbase push frame missing or non-array 'events'; cannot decode {typeof(T).Name}.");
        var enumerator = events.EnumerateArray();
        if (!enumerator.MoveNext())
            throw new InvalidOperationException(
                $"Coinbase 'events' array is empty; cannot decode {typeof(T).Name}.");
        return enumerator.Current.Deserialize<T>(JsonOpts);
    }

    // market_trades events contain a "trades" array; take the last (newest) entry.
    private static StreamTradeDto? DeserializeLastTrade(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("events"u8, out var events) ||
            events.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Coinbase market_trades frame missing 'events' array; cannot decode StreamTradeDto.");
        var evtEnum = events.EnumerateArray();
        if (!evtEnum.MoveNext())
            throw new InvalidOperationException(
                "Coinbase market_trades 'events' array is empty; cannot decode StreamTradeDto.");
        var firstEvent = evtEnum.Current;
        if (!firstEvent.TryGetProperty("trades"u8, out var tradesArr) ||
            tradesArr.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Coinbase market_trades event missing 'trades' array; cannot decode StreamTradeDto.");
        StreamTradeDto? last = null;
        foreach (var tradeEl in tradesArr.EnumerateArray())
            last = tradeEl.Deserialize<StreamTradeDto>(JsonOpts);
        if (last is null)
            throw new InvalidOperationException(
                "Coinbase market_trades 'trades' array is empty; cannot decode StreamTradeDto.");
        return last;
    }

    // candles events contain a "candles" array; take the first entry.
    private static StreamKlineDto? DeserializeFirstCandle(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("events"u8, out var events) ||
            events.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Coinbase candles frame missing 'events' array; cannot decode StreamKlineDto.");
        var evtEnum = events.EnumerateArray();
        if (!evtEnum.MoveNext())
            throw new InvalidOperationException(
                "Coinbase candles 'events' array is empty; cannot decode StreamKlineDto.");
        var firstEvent = evtEnum.Current;
        if (!firstEvent.TryGetProperty("candles"u8, out var candlesArr) ||
            candlesArr.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Coinbase candles event missing 'candles' array; cannot decode StreamKlineDto.");
        var candleEnum = candlesArr.EnumerateArray();
        if (!candleEnum.MoveNext())
            throw new InvalidOperationException(
                "Coinbase candles 'candles' array is empty; cannot decode StreamKlineDto.");
        return candleEnum.Current.Deserialize<StreamKlineDto>(JsonOpts);
    }
}
