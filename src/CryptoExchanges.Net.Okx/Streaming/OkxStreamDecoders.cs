using System.Text.Json;
using CryptoExchanges.Net.Okx.Dtos.Streaming;
using CryptoExchanges.Net.Okx.Internal;
using CryptoExchanges.Net.Http.Streaming;
using DeltaMapper;

namespace CryptoExchanges.Net.Okx.Streaming;

/// <summary>
/// Builds the <see cref="StreamDecoderRegistry"/> for the OKX v5 public WebSocket streams —
/// each closure unwraps the <c>arg</c>+<c>data</c>-array envelope, deserializes the leaf DTO,
/// and maps to <c>Core.Models</c>.
/// </summary>
internal static class OkxStreamDecoders
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = false
    };

    /// <summary>
    /// Builds a fully-populated <see cref="StreamDecoderRegistry"/> with decoders for all four
    /// stream kinds, capturing the provided mapper and symbol mapper as closures.
    /// </summary>
    /// <param name="mapper">The keyed DeltaMapper instance from <c>AddOkxStreams</c>.</param>
    /// <param name="symbolMapper">The bespoke keyed symbol mapper from <c>AddOkxStreams</c>.</param>
    /// <returns>A registry ready for injection into the engine.</returns>
    public static StreamDecoderRegistry Build(IMapper mapper, ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(symbolMapper);

        var registry = new StreamDecoderRegistry();

        registry.Register(StreamKind.Ticker, bytes =>
        {
            var instId = ReadInstId(bytes);
            var dto = DeserializeFirstDataElement<StreamTickerDto>(bytes);
            var result = mapper.Map<StreamTickerDto, Ticker>(dto);
            return result with { Symbol = symbolMapper.FromWire(instId) };
        });

        // v1 emits only the latest trade per frame (data batches oldest→newest).
        registry.Register(StreamKind.Trade, bytes =>
        {
            var instId = ReadInstId(bytes);
            var dto = DeserializeLastDataElement<StreamTradeDto>(bytes);
            var symbol = symbolMapper.FromWire(instId);
            return new Trade(
                Symbol: symbol,
                Id: dto.TradeId,
                Price: OkxValueParsers.ParseDecimal(dto.Px),
                Quantity: OkxValueParsers.ParseDecimal(dto.Sz),
                Timestamp: long.TryParse(dto.Ts, out var ms) && ms > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                    : null,
                // OKX "side" is the taker side: "sell" taker ⇒ buyer is the maker.
                IsBuyerMaker: string.Equals(dto.Side, "sell", StringComparison.Ordinal));
        });

        registry.Register(StreamKind.OrderBook, bytes =>
        {
            var instId = ReadInstId(bytes);
            var dto = DeserializeFirstDataElement<StreamDepthDto>(bytes);
            var symbol = symbolMapper.FromWire(instId);
            var bids = new List<OrderBookEntry>(dto.Bids.Count);
            foreach (var b in dto.Bids)
                bids.Add(new OrderBookEntry(
                    OkxValueParsers.ParseDecimal(b[0]),
                    OkxValueParsers.ParseDecimal(b[1])));
            var asks = new List<OrderBookEntry>(dto.Asks.Count);
            foreach (var a in dto.Asks)
                asks.Add(new OrderBookEntry(
                    OkxValueParsers.ParseDecimal(a[0]),
                    OkxValueParsers.ParseDecimal(a[1])));
            return new OrderBook(symbol, bids, asks, null, dto.SeqId > 0 ? dto.SeqId : null);
        });

        // Positional kline row: [ts,o,h,l,c,vol,volCcy,volCcyQuote,confirm].
        registry.Register(StreamKind.Kline, bytes =>
        {
            var instId = ReadInstId(bytes);
            var symbol = symbolMapper.FromWire(instId);
            var row = DeserializeFirstDataElement<List<string>>(bytes);
            if (row.Count < 6)
                throw new InvalidOperationException(
                    $"OKX kline row has {row.Count} elements; expected at least 6 (ts,o,h,l,c,vol).");
            return new Candlestick(
                OpenTime: long.TryParse(row[0], out var ts) && ts > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ts)
                    : DateTimeOffset.MinValue,
                CloseTime: null,
                Open: OkxValueParsers.ParseDecimal(row[1]),
                High: OkxValueParsers.ParseDecimal(row[2]),
                Low: OkxValueParsers.ParseDecimal(row[3]),
                Close: OkxValueParsers.ParseDecimal(row[4]),
                Volume: OkxValueParsers.ParseDecimal(row[5]),
                QuoteVolume: row.Count > 7 ? OkxValueParsers.ParseDecimal(row[7]) : null,
                TradeCount: null,
                Interval: null);
        });

        return registry;
    }

    // Symbol is on arg.instId, not in the data rows.
    private static string ReadInstId(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("arg"u8, out var arg)
            || !arg.TryGetProperty("instId"u8, out var instId)
            || instId.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException(
                "OKX push frame missing or non-string 'arg.instId'; cannot resolve symbol.");
        return instId.GetString()!;
    }

    private static T DeserializeFirstDataElement<T>(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("data"u8, out var data)
            || data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"OKX push frame missing or non-array 'data'; cannot decode {typeof(T).Name}.");
        var enumerator = data.EnumerateArray();
        if (!enumerator.MoveNext())
            throw new InvalidOperationException(
                $"OKX 'data' array is empty; cannot decode {typeof(T).Name}.");
        return enumerator.Current.Deserialize<T>(JsonOpts)
            ?? throw new InvalidOperationException(
                $"OKX 'data' element deserialized to null; cannot decode {typeof(T).Name}.");
    }

    private static T DeserializeLastDataElement<T>(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("data"u8, out var data)
            || data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"OKX push frame missing or non-array 'data'; cannot decode {typeof(T).Name}.");
        JsonElement? last = null;
        foreach (var element in data.EnumerateArray())
            last = element;
        if (last is null)
            throw new InvalidOperationException(
                $"OKX 'data' array is empty; cannot decode {typeof(T).Name}.");
        return last.Value.Deserialize<T>(JsonOpts)
            ?? throw new InvalidOperationException(
                $"OKX 'data' element deserialized to null; cannot decode {typeof(T).Name}.");
    }
}
