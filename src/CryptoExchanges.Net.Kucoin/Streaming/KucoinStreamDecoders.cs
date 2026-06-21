using System.Text.Json;
using CryptoExchanges.Net.Http.Streaming;
using CryptoExchanges.Net.Kucoin.Dtos.Streaming;
using CryptoExchanges.Net.Kucoin.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Kucoin.Streaming;

/// <summary>
/// Builds the <see cref="StreamDecoderRegistry"/> for KuCoin public WebSocket streams.
/// Each closure deserializes the push-frame payload and maps it to a <c>Core.Models</c> type
/// via the keyed <see cref="IMapper"/> and <see cref="ISymbolMapper"/>.
/// </summary>
internal static class KucoinStreamDecoders
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Builds a <see cref="StreamDecoderRegistry"/> with decoders for all four stream kinds.</summary>
    /// <param name="mapper">The keyed DeltaMapper instance.</param>
    /// <param name="symbolMapper">The bespoke keyed symbol mapper.</param>
    /// <returns>A registry ready for injection into the engine.</returns>
    public static StreamDecoderRegistry Build(IMapper mapper, ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(symbolMapper);

        var registry = new StreamDecoderRegistry();

        // Ticker: snapshot channel wraps payload as data.data (double-nested).
        registry.Register(StreamKind.Ticker, bytes =>
        {
            var dto = DeserializeSnapshotData<StreamTickerDto>(bytes)!;
            return mapper.Map<StreamTickerDto, Ticker>(dto);
        });

        registry.Register(StreamKind.Trade, bytes =>
        {
            var dto = DeserializeData<StreamTradeDto>(bytes)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            // "buy" taker → seller is maker (IsBuyerMaker = false).
            var isBuyerMaker = string.Equals(dto.Side, "sell", StringComparison.OrdinalIgnoreCase);
            var tsMs = KucoinValueParsers.ParseNsToMs(dto.Time);
            return new Trade(
                Symbol: symbol,
                Id: dto.TradeId,
                Price: KucoinValueParsers.ParseDecimal(dto.Price),
                Quantity: KucoinValueParsers.ParseDecimal(dto.Size),
                Timestamp: tsMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(tsMs) : null,
                IsBuyerMaker: isBuyerMaker);
        });

        // KuCoin level2 stream delivers incremental diffs; each entry is [price, size, sequence].
        registry.Register(StreamKind.OrderBook, bytes =>
        {
            var dto = DeserializeData<StreamDepthDto>(bytes)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            var bidEntries = dto.Changes.Bids;
            var bids = new List<OrderBookEntry>(bidEntries.Count);
            foreach (var b in bidEntries)
                bids.Add(new OrderBookEntry(KucoinValueParsers.ParseDecimal(b[0]), KucoinValueParsers.ParseDecimal(b[1])));
            var askEntries = dto.Changes.Asks;
            var asks = new List<OrderBookEntry>(askEntries.Count);
            foreach (var a in askEntries)
                asks.Add(new OrderBookEntry(KucoinValueParsers.ParseDecimal(a[0]), KucoinValueParsers.ParseDecimal(a[1])));
            return new OrderBook(symbol, bids, asks, null, dto.SequenceEnd);
        });

        // KuCoin candles array: [startAt(s), open, close, high, low, vol, quoteVol]; interval not in payload.
        registry.Register(StreamKind.Kline, bytes =>
        {
            var dto = DeserializeData<StreamKlineDto>(bytes)!;
            var c = dto.Candles;
            var openTimeSec = c.Count > 0 ? KucoinValueParsers.ParseMs(c[0]) : 0L;
            var openTime = openTimeSec > 0
                ? DateTimeOffset.FromUnixTimeSeconds(openTimeSec)
                : DateTimeOffset.MinValue;
            return new Candlestick(
                OpenTime: openTime,
                // KuCoin does not send a separate close time in the streaming payload.
                CloseTime: null,
                Open: c.Count > 1 ? KucoinValueParsers.ParseDecimal(c[1]) : 0m,
                High: c.Count > 3 ? KucoinValueParsers.ParseDecimal(c[3]) : 0m,
                Low: c.Count > 4 ? KucoinValueParsers.ParseDecimal(c[4]) : 0m,
                Close: c.Count > 2 ? KucoinValueParsers.ParseDecimal(c[2]) : 0m,
                Volume: c.Count > 5 ? KucoinValueParsers.ParseDecimal(c[5]) : 0m,
                QuoteVolume: c.Count > 6 ? KucoinValueParsers.ParseDecimal(c[6]) : 0m,
                TradeCount: null,
                Interval: null);
        });

        return registry;
    }

    /// <summary>Deserializes the <c>data</c> field from a push frame, or the raw bytes when no outer wrapper is present.</summary>
    private static T? DeserializeData<T>(ReadOnlyMemory<byte> frame)
    {
        try
        {
            var reader = new Utf8JsonReader(frame.Span);
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("data"u8, out var dataProp))
                return dataProp.Deserialize<T>(JsonOpts);
        }
        catch (JsonException)
        {
            // Malformed outer wrapper: fall through and try deserializing as bare payload.
        }

        return JsonSerializer.Deserialize<T>(frame.Span, JsonOpts);
    }

    /// <summary>
    /// Deserializes the double-nested <c>data.data</c> payload for the <c>/market/snapshot</c> channel.
    /// Falls back to deserializing the raw bytes when no outer wrapper is present (unit-test fixtures).
    /// </summary>
    private static T? DeserializeSnapshotData<T>(ReadOnlyMemory<byte> frame)
    {
        try
        {
            var reader = new Utf8JsonReader(frame.Span);
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            // Full push frame: {... "data": {"sequence":..., "data": {...}}}
            if (root.TryGetProperty("data"u8, out var outerData))
            {
                if (outerData.TryGetProperty("data"u8, out var innerData))
                    return innerData.Deserialize<T>(JsonOpts);

                // Outer data present but no inner data — try deserializing outer as the payload
                // (handles bare single-level test fixtures that already represent the inner object).
                return outerData.Deserialize<T>(JsonOpts);
            }
        }
        catch (JsonException)
        {
            // Malformed outer wrapper — fall through.
        }

        // Bare inner payload (unit-test fixture with no envelope at all).
        return JsonSerializer.Deserialize<T>(frame.Span, JsonOpts);
    }
}
