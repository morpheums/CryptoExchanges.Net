using System.Text.Json;
using CryptoExchanges.Net.Bybit.Dtos.Streaming;
using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Http.Streaming;
using DeltaMapper;

namespace CryptoExchanges.Net.Bybit.Streaming;

/// <summary>
/// Builds the <see cref="StreamDecoderRegistry"/> for the Bybit v5 public WebSocket streams —
/// each closure unwraps the data envelope, deserializes the leaf DTO, and maps to <c>Core.Models</c>.
/// </summary>
internal static class BybitStreamDecoders
{
    // Case-sensitive: Bybit v5 publicTrade uses single-character keys where case distinguishes
    // fields ("s" = symbol, "S" = taker side). Case-insensitive would cause property-name
    // collisions at runtime (same issue as Binance single-char fields).
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = false
    };

    /// <summary>
    /// Builds a fully-populated <see cref="StreamDecoderRegistry"/> with decoders for all four
    /// stream kinds, capturing the provided mapper and symbol mapper as closures.
    /// </summary>
    /// <param name="mapper">The keyed DeltaMapper instance from <c>AddBybitExchange</c>.</param>
    /// <param name="symbolMapper">The bespoke keyed symbol mapper from <c>AddBybitExchange</c>.</param>
    /// <returns>A registry ready for injection into the engine.</returns>
    public static StreamDecoderRegistry Build(IMapper mapper, ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(symbolMapper);

        var registry = new StreamDecoderRegistry();

        // Bybit v5 ticker frame: data is a single object {"symbol":...,"lastPrice":...,...}.
        registry.Register(StreamKind.Ticker, bytes =>
        {
            var dto = DeserializeData<StreamTickerDto>(bytes)!;
            return mapper.Map<StreamTickerDto, Ticker>(dto);
        });

        // Bybit v5 publicTrade frame: data is an ARRAY of trade objects; emit the first entry.
        // S == "Sell" means the taker was a seller, so the buyer was the market maker.
        registry.Register(StreamKind.Trade, bytes =>
        {
            var dto = DeserializeFirstArrayElement<StreamTradeDto>(bytes)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            return new Trade(
                Symbol: symbol,
                Id: dto.TradeId,
                Price: BybitValueParsers.ParseDecimal(dto.Price),
                Quantity: BybitValueParsers.ParseDecimal(dto.Quantity),
                Timestamp: dto.TradeTime > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(dto.TradeTime)
                    : null,
                IsBuyerMaker: string.Equals(dto.Side, "Sell", StringComparison.Ordinal));
        });

        // Bybit v5 orderbook frame: data is a single object {"s":...,"b":[...],"a":[...],"u":...,"seq":...}.
        // Both snapshot and delta frames share this shape; no local-book maintenance required.
        registry.Register(StreamKind.OrderBook, bytes =>
        {
            var dto = DeserializeData<StreamDepthDto>(bytes)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            var bids = new List<OrderBookEntry>(dto.Bids.Count);
            foreach (var b in dto.Bids)
                bids.Add(new OrderBookEntry(
                    BybitValueParsers.ParseDecimal(b[0]),
                    BybitValueParsers.ParseDecimal(b[1])));
            var asks = new List<OrderBookEntry>(dto.Asks.Count);
            foreach (var a in dto.Asks)
                asks.Add(new OrderBookEntry(
                    BybitValueParsers.ParseDecimal(a[0]),
                    BybitValueParsers.ParseDecimal(a[1])));
            return new OrderBook(symbol, bids, asks, null, dto.UpdateId);
        });

        // Bybit v5 kline frame: data is an ARRAY of kline objects; emit the first entry.
        registry.Register(StreamKind.Kline, bytes =>
        {
            var dto = DeserializeFirstArrayElement<StreamKlineDto>(bytes)!;
            var interval = MapWireInterval(dto.Interval);
            // dto.Confirm indicates a closed bar; the Candlestick model does not carry that flag.
            return new Candlestick(
                OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(dto.OpenTime),
                CloseTime: null,
                Open: BybitValueParsers.ParseDecimal(dto.Open),
                High: BybitValueParsers.ParseDecimal(dto.High),
                Low: BybitValueParsers.ParseDecimal(dto.Low),
                Close: BybitValueParsers.ParseDecimal(dto.Close),
                Volume: BybitValueParsers.ParseDecimal(dto.Volume),
                QuoteVolume: BybitValueParsers.ParseDecimal(dto.Turnover),
                TradeCount: null,
                Interval: interval);
        });

        return registry;
    }

    // Bybit v5 push frames: {"topic":..., "type":..., "ts":..., "data": <object>}
    // Unwrap "data" before deserializing the leaf DTO (FEAT-008/TASK-074 lesson).
    private static T? DeserializeData<T>(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("data"u8, out var dataProp))
            throw new InvalidOperationException(
                "Bybit v5 push frame has no 'data' element; cannot decode the payload.");
        return dataProp.Deserialize<T>(JsonOpts);
    }

    // Bybit v5 publicTrade and kline frames carry data as an ARRAY; unwrap and take first element.
    private static T? DeserializeFirstArrayElement<T>(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("data"u8, out var dataProp))
            throw new InvalidOperationException(
                "Bybit v5 push frame has no 'data' element; cannot decode the payload.");
        if (dataProp.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"Expected Bybit v5 'data' to be an array for type {typeof(T).Name}; got {dataProp.ValueKind}.");
        var enumerator = dataProp.EnumerateArray();
        if (!enumerator.MoveNext())
            throw new InvalidOperationException(
                $"Bybit v5 'data' array is empty; cannot decode {typeof(T).Name}.");
        return enumerator.Current.Deserialize<T>(JsonOpts);
    }

    private static KlineInterval? MapWireInterval(string wire) => wire switch
    {
        "1" => KlineInterval.OneMinute,
        "3" => KlineInterval.ThreeMinutes,
        "5" => KlineInterval.FiveMinutes,
        "15" => KlineInterval.FifteenMinutes,
        "30" => KlineInterval.ThirtyMinutes,
        "60" => KlineInterval.OneHour,
        "120" => KlineInterval.TwoHours,
        "240" => KlineInterval.FourHours,
        "360" => KlineInterval.SixHours,
        "720" => KlineInterval.TwelveHours,
        "D" => KlineInterval.OneDay,
        "W" => KlineInterval.OneWeek,
        "M" => KlineInterval.OneMonth,
        _ => null
    };
}
