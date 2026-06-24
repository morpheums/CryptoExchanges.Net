using CryptoExchanges.Net.Bitget.Dtos.Streaming;
using CryptoExchanges.Net.Bitget.Internal;
using CryptoExchanges.Net.Http.Streaming;
using DeltaMapper;

namespace CryptoExchanges.Net.Bitget.Streaming;

/// <summary>
/// Builds the <see cref="StreamDecoderRegistry"/> for the Bitget v2 public WebSocket streams —
/// each closure unwraps the <c>{"action":...,"arg":{"instId":...},"data":[...]}</c> envelope,
/// deserializes the leaf DTO, and maps to <c>Core.Models</c>.
/// </summary>
internal static class BitgetStreamDecoders
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = false
    };

    /// <summary>
    /// Builds a fully-populated <see cref="StreamDecoderRegistry"/> with decoders for all four
    /// stream kinds, capturing the provided mapper and symbol mapper as closures.
    /// </summary>
    /// <param name="mapper">The keyed DeltaMapper instance from <c>AddBitgetExchange</c>.</param>
    /// <param name="symbolMapper">The bespoke keyed symbol mapper from <c>AddBitgetExchange</c>.</param>
    /// <returns>A registry ready for injection into the engine.</returns>
    public static StreamDecoderRegistry Build(IMapper mapper, ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(symbolMapper);

        var registry = new StreamDecoderRegistry();

        registry.Register(StreamKind.Ticker, bytes =>
        {
            var dto = DeserializeFirstArrayElement<StreamTickerDto>(bytes)!;
            return mapper.Map<StreamTickerDto, Ticker>(dto);
        });

        // v1 emits only the latest trade per frame; "sell" is the taker side, so the buyer is the maker.
        registry.Register(StreamKind.Trade, bytes =>
        {
            var symbol = ResolveSymbolFromArg(bytes, symbolMapper);
            var dto = DeserializeLastArrayElement<StreamTradeDto>(bytes)!;
            var ms = BitgetValueParsers.ParseMs(dto.Ts);
            return new Trade(
                Symbol: symbol,
                Id: dto.TradeId,
                Price: BitgetValueParsers.ParseDecimal(dto.Price),
                Quantity: BitgetValueParsers.ParseDecimal(dto.Size),
                Timestamp: ms > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null,
                IsBuyerMaker: string.Equals(dto.Side, "sell", StringComparison.OrdinalIgnoreCase));
        });

        registry.Register(StreamKind.OrderBook, bytes =>
        {
            var symbol = ResolveSymbolFromArg(bytes, symbolMapper);
            var dto = DeserializeFirstArrayElement<StreamDepthDto>(bytes)!;
            var bids = new List<OrderBookEntry>(dto.Bids.Count);
            foreach (var b in dto.Bids)
                bids.Add(new OrderBookEntry(
                    BitgetValueParsers.ParseDecimal(b[0]),
                    BitgetValueParsers.ParseDecimal(b[1])));
            var asks = new List<OrderBookEntry>(dto.Asks.Count);
            foreach (var a in dto.Asks)
                asks.Add(new OrderBookEntry(
                    BitgetValueParsers.ParseDecimal(a[0]),
                    BitgetValueParsers.ParseDecimal(a[1])));
            return new OrderBook(symbol, bids, asks, null, dto.SeqId);
        });

        // Positional kline row: [ts,open,high,low,close,baseVol,quoteVol,...].
        registry.Register(StreamKind.Kline, bytes =>
        {
            var symbol = ResolveSymbolFromArg(bytes, symbolMapper);
            var row = DeserializeFirstKlineRow(bytes);
            var ms = BitgetValueParsers.ParseMs(row[0]);
            return new Candlestick(
                OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(ms),
                CloseTime: null,
                Open: BitgetValueParsers.ParseDecimal(row[1]),
                High: BitgetValueParsers.ParseDecimal(row[2]),
                Low: BitgetValueParsers.ParseDecimal(row[3]),
                Close: BitgetValueParsers.ParseDecimal(row[4]),
                Volume: BitgetValueParsers.ParseDecimal(row[5]),
                QuoteVolume: row.Count > 6 ? BitgetValueParsers.ParseDecimal(row[6]) : 0m,
                TradeCount: null,
                Interval: null);
        });

        return registry;
    }

    // Symbol lives in arg.instId, not in the data rows.
    private static Symbol ResolveSymbolFromArg(ReadOnlyMemory<byte> frame, ISymbolMapper symbolMapper)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("arg"u8, out var argProp)
            || !argProp.TryGetProperty("instId"u8, out var instIdProp)
            || instIdProp.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                "Bitget push frame missing arg.instId; cannot resolve the symbol.");
        }
        return symbolMapper.FromWire(instIdProp.GetString()!);
    }

    private static T? DeserializeFirstArrayElement<T>(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("data"u8, out var dataProp))
            throw new InvalidOperationException(
                "Bitget push frame has no 'data' element; cannot decode the payload.");
        if (dataProp.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"Expected Bitget 'data' to be an array for {typeof(T).Name}; got {dataProp.ValueKind}.");
        var enumerator = dataProp.EnumerateArray();
        if (!enumerator.MoveNext())
            throw new InvalidOperationException(
                $"Bitget 'data' array is empty; cannot decode {typeof(T).Name}.");
        return enumerator.Current.Deserialize<T>(JsonOpts);
    }

    private static T? DeserializeLastArrayElement<T>(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("data"u8, out var dataProp))
            throw new InvalidOperationException(
                "Bitget push frame has no 'data' element; cannot decode the payload.");
        if (dataProp.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"Expected Bitget 'data' to be an array for {typeof(T).Name}; got {dataProp.ValueKind}.");
        JsonElement? last = null;
        foreach (var el in dataProp.EnumerateArray())
            last = el;
        if (last is null)
            throw new InvalidOperationException(
                $"Bitget 'data' array is empty; cannot decode {typeof(T).Name}.");
        return last.Value.Deserialize<T>(JsonOpts);
    }

    private static List<string> DeserializeFirstKlineRow(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("data"u8, out var dataProp))
            throw new InvalidOperationException(
                "Bitget kline push frame has no 'data' element; cannot decode the payload.");
        if (dataProp.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"Expected Bitget kline 'data' to be an array; got {dataProp.ValueKind}.");
        var rows = dataProp.EnumerateArray();
        if (!rows.MoveNext())
            throw new InvalidOperationException("Bitget kline 'data' array is empty; cannot decode.");
        var row = rows.Current.Deserialize<List<string>>(JsonOpts)
            ?? throw new InvalidOperationException("Bitget kline row deserialized as null.");
        if (row.Count < 6)
            throw new InvalidOperationException(
                $"Bitget kline row has {row.Count} elements; expected at least 6 (ts,o,h,l,c,baseVol).");
        return row;
    }
}
