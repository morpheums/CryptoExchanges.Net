using System.Text.Json;
using CryptoExchanges.Net.Kraken.Dtos.Streaming;
using CryptoExchanges.Net.Http.Streaming;
using DeltaMapper;

namespace CryptoExchanges.Net.Kraken.Streaming;

/// <summary>
/// Builds the <see cref="StreamDecoderRegistry"/> for the Kraken WS v2 public WebSocket streams.
/// Each closure unwraps the <c>{"channel":...,"data":[...]}</c> envelope, deserializes the first
/// data-array element as the leaf DTO, and maps to <c>Core.Models</c>. Symbol is sourced from
/// the data element's <c>symbol</c> field (slash/wsname form) rather than the envelope.
/// </summary>
internal static class KrakenStreamDecoders
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = false
    };

    /// <summary>
    /// Builds a fully-populated <see cref="StreamDecoderRegistry"/> with decoders for all four
    /// stream kinds, capturing the provided mapper and symbol mapper as closures.
    /// </summary>
    /// <param name="mapper">The keyed DeltaMapper instance from <c>AddKrakenStreams</c>.</param>
    /// <param name="symbolMapper">The bespoke keyed symbol mapper from <c>AddKrakenStreams</c>.</param>
    /// <returns>A registry ready for injection into the engine.</returns>
    public static StreamDecoderRegistry Build(IMapper mapper, ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(symbolMapper);

        var registry = new StreamDecoderRegistry();

        registry.Register(StreamKind.Ticker, bytes =>
        {
            var dto = DeserializeFirstDataElement<StreamTickerDto>(bytes)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            return new Ticker(
                Symbol: symbol,
                LastPrice: dto.Last,
                OpenPrice: 0m,
                HighPrice: dto.High,
                LowPrice: dto.Low,
                Volume: dto.Volume,
                QuoteVolume: null,
                PriceChange: dto.Change,
                PriceChangePercent: dto.ChangePct,
                Timestamp: null);
        });

        registry.Register(StreamKind.Trade, bytes =>
        {
            // Kraken trade frames may batch multiple trades; emit the most recent (last).
            var dto = DeserializeLastDataElement<StreamTradeDto>(bytes)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            // "side" is the taker side: "sell" taker ⇒ buyer is the maker.
            var isBuyerMaker = string.Equals(dto.Side, "sell", StringComparison.Ordinal);
            var ts = DateTimeOffset.TryParse(dto.Timestamp,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : (DateTimeOffset?)null;
            return new Trade(
                Symbol: symbol,
                Id: dto.TradeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Price: dto.Price,
                Quantity: dto.Qty,
                Timestamp: ts,
                IsBuyerMaker: isBuyerMaker);
        });

        registry.Register(StreamKind.OrderBook, bytes =>
        {
            var dto = DeserializeFirstDataElement<StreamDepthDto>(bytes)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            var bids = new List<OrderBookEntry>(dto.Bids.Count);
            foreach (var b in dto.Bids)
                bids.Add(new OrderBookEntry(b.Price, b.Qty));
            var asks = new List<OrderBookEntry>(dto.Asks.Count);
            foreach (var a in dto.Asks)
                asks.Add(new OrderBookEntry(a.Price, a.Qty));
            return new OrderBook(symbol, bids, asks, null, dto.Checksum > 0 ? dto.Checksum : null);
        });

        registry.Register(StreamKind.Kline, bytes =>
        {
            var dto = DeserializeFirstDataElement<StreamKlineDto>(bytes)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            var openTime = DateTimeOffset.TryParse(dto.IntervalBegin,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTimeOffset.MinValue;
            return new Candlestick(
                OpenTime: openTime,
                CloseTime: null,
                Open: dto.Open,
                High: dto.High,
                Low: dto.Low,
                Close: dto.Close,
                Volume: dto.Volume,
                QuoteVolume: null,
                TradeCount: null,
                Interval: null);
        });

        return registry;
    }

    private static T? DeserializeFirstDataElement<T>(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("data"u8, out var data)
            || data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"Kraken push frame missing or non-array 'data'; cannot decode {typeof(T).Name}.");
        var enumerator = data.EnumerateArray();
        if (!enumerator.MoveNext())
            throw new InvalidOperationException(
                $"Kraken 'data' array is empty; cannot decode {typeof(T).Name}.");
        return enumerator.Current.Deserialize<T>(JsonOpts);
    }

    private static T? DeserializeLastDataElement<T>(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("data"u8, out var data)
            || data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"Kraken push frame missing or non-array 'data'; cannot decode {typeof(T).Name}.");
        JsonElement? last = null;
        foreach (var element in data.EnumerateArray())
            last = element;
        if (last is null)
            throw new InvalidOperationException(
                $"Kraken 'data' array is empty; cannot decode {typeof(T).Name}.");
        return last.Value.Deserialize<T>(JsonOpts);
    }
}
