using System.Text.Json;
using CryptoExchanges.Net.Http.Streaming;
using CryptoExchanges.Net.Kucoin.Dtos.Streaming;
using CryptoExchanges.Net.Kucoin.Internal;
using DeltaMapper;

namespace CryptoExchanges.Net.Kucoin.Streaming;

/// <summary>
/// Builds the <see cref="StreamDecoderRegistry"/> for the KuCoin public WebSocket streams.
/// Each closure deserializes the KuCoin push-frame <c>data</c> payload into the venue DTO
/// and maps it to the matching <see cref="CryptoExchanges.Net.Core.Models"/> type, reusing
/// the keyed <see cref="IMapper"/> and bespoke <see cref="ISymbolMapper"/>.
/// </summary>
/// <remarks>
/// Binding constraint K1: DTO deserialization, DeltaMapper projection, and symbol resolution
/// all happen here in the Kucoin package. The Http engine receives only the resulting opaque
/// <see cref="StreamDecoderRegistry"/> of <c>Func&lt;ReadOnlyMemory&lt;byte&gt;, object&gt;</c>.
/// </remarks>
internal static class KucoinStreamDecoders
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Builds a fully-populated <see cref="StreamDecoderRegistry"/> with decoders for
    /// all four stream kinds, capturing the provided mapper and symbol mapper as closures.
    /// </summary>
    /// <param name="mapper">The keyed DeltaMapper instance from <c>AddKucoinExchange</c>.</param>
    /// <param name="symbolMapper">The bespoke keyed symbol mapper from <c>AddKucoinExchange</c>.</param>
    /// <returns>A registry ready for injection into the engine.</returns>
    public static StreamDecoderRegistry Build(IMapper mapper, ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(symbolMapper);

        var registry = new StreamDecoderRegistry();

        // ── Ticker ────────────────────────────────────────────────────────────
        // Ticker uses DeltaMapper (StreamTickerDto -> Ticker profile in KucoinResponseProfile).
        registry.Register(StreamKind.Ticker, bytes =>
        {
            var data = ExtractDataBytes(bytes);
            var dto = JsonSerializer.Deserialize<StreamTickerDto>(data.Span, JsonOpts)!;
            return mapper.Map<StreamTickerDto, Ticker>(dto);
        });

        // ── Trade ─────────────────────────────────────────────────────────────
        // Trade is hand-mapped (matches the convention for the match stream).
        registry.Register(StreamKind.Trade, bytes =>
        {
            var data = ExtractDataBytes(bytes);
            var dto = JsonSerializer.Deserialize<StreamTradeDto>(data.Span, JsonOpts)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            var sideStr = dto.Side;
            // The taker side identifies the aggressor: "buy" taker → seller is maker (IsBuyerMaker = false).
            var isBuyerMaker = string.Equals(sideStr, "sell", StringComparison.OrdinalIgnoreCase);
            var tsMs = KucoinValueParsers.ParseNsToMs(dto.Time);
            return new Trade(
                Symbol: symbol,
                Id: dto.TradeId,
                Price: KucoinValueParsers.ParseDecimal(dto.Price),
                Quantity: KucoinValueParsers.ParseDecimal(dto.Size),
                Timestamp: tsMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(tsMs) : null,
                IsBuyerMaker: isBuyerMaker);
        });

        // ── OrderBook ─────────────────────────────────────────────────────────
        // Hand-mapped following the REST GetOrderBookAsync convention.
        // KuCoin level2 stream delivers incremental diffs; each entry is [price, size, sequence].
        registry.Register(StreamKind.OrderBook, bytes =>
        {
            var data = ExtractDataBytes(bytes);
            var dto = JsonSerializer.Deserialize<StreamDepthDto>(data.Span, JsonOpts)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            var bids = dto.Changes.Bids
                .Select(b => new OrderBookEntry(
                    KucoinValueParsers.ParseDecimal(b[0]),
                    KucoinValueParsers.ParseDecimal(b[1])))
                .ToList();
            var asks = dto.Changes.Asks
                .Select(a => new OrderBookEntry(
                    KucoinValueParsers.ParseDecimal(a[0]),
                    KucoinValueParsers.ParseDecimal(a[1])))
                .ToList();
            return new OrderBook(symbol, bids, asks, null, dto.SequenceEnd);
        });

        // ── Kline ─────────────────────────────────────────────────────────────
        // Hand-mapped. KuCoin candles array: [startAt(s), open, close, high, low, vol, quoteVol].
        // Indices: 0=startAt, 1=open, 2=close, 3=high, 4=low, 5=volume, 6=quoteVolume.
        // The interval is encoded in the routing-key topic but not in the data payload;
        // it is set to null here as the decoder receives only the data bytes.
        registry.Register(StreamKind.Kline, bytes =>
        {
            var data = ExtractDataBytes(bytes);
            var dto = JsonSerializer.Deserialize<StreamKlineDto>(data.Span, JsonOpts)!;
            // candles: [startAt(unix-s), open, close, high, low, vol, quoteVol]
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the <c>data</c> field bytes from a full KuCoin push frame
    /// (<c>{"type":"message","topic":"...","data":{...}}</c>). When the input bytes do
    /// not contain a root-level <c>"data"</c> field the original bytes are returned
    /// unchanged — this allows tests to feed the data payload directly.
    /// </summary>
    private static ReadOnlyMemory<byte> ExtractDataBytes(ReadOnlyMemory<byte> frame)
    {
        try
        {
            var reader = new Utf8JsonReader(frame.Span);
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("data"u8, out var dataProp))
            {
                // Re-serialize the data element to get its raw bytes.
                using var ms = new System.IO.MemoryStream();
                using var writer = new Utf8JsonWriter(ms);
                dataProp.WriteTo(writer);
                writer.Flush();
                return ms.ToArray();
            }
        }
        catch (JsonException)
        {
            // Malformed JSON: fall through and let the deserializer handle or throw.
        }

        return frame;
    }
}
