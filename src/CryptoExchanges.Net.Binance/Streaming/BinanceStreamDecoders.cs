using CryptoExchanges.Net.Binance.Dtos.Streaming;
using CryptoExchanges.Net.Binance.Internal;
using CryptoExchanges.Net.Http.Streaming;
using DeltaMapper;

namespace CryptoExchanges.Net.Binance.Streaming;

/// <summary>
/// Builds the <see cref="StreamDecoderRegistry"/> for the venue WebSocket streams.
/// Each closure deserializes the combined-stream <c>data</c> payload into its venue DTO
/// and maps it to the matching <see cref="CryptoExchanges.Net.Core.Models"/> type,
/// reusing the keyed <see cref="IMapper"/> and bespoke <see cref="ISymbolMapper"/>.
/// </summary>
/// <remarks>
/// Binding constraint K1: DTO deserialization, DeltaMapper projection, and symbol resolution
/// all happen here in the exchange package. The Http engine receives only the resulting
/// opaque <see cref="StreamDecoderRegistry"/> of <c>Func&lt;ReadOnlyMemory&lt;byte&gt;, object&gt;</c>.
/// </remarks>
internal static class BinanceStreamDecoders
{
    // Case-sensitive: the venue uses single-character keys where case distinguishes fields
    // (e.g. ticker "p" = price change, "P" = percent change). Web defaults are case-insensitive
    // and would cause property-name collisions at runtime.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = false
    };

    /// <summary>
    /// Builds a fully-populated <see cref="StreamDecoderRegistry"/> with decoders for
    /// all four stream kinds, capturing the provided mapper and symbol mapper as closures.
    /// </summary>
    /// <param name="mapper">The keyed DeltaMapper instance from <c>AddBinanceExchange</c>.</param>
    /// <param name="symbolMapper">The bespoke keyed symbol mapper from <c>AddBinanceExchange</c>.</param>
    /// <returns>A registry ready for injection into the engine.</returns>
    public static StreamDecoderRegistry Build(IMapper mapper, ISymbolMapper symbolMapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(symbolMapper);

        var registry = new StreamDecoderRegistry();

        // ── Ticker ────────────────────────────────────────────────────────────
        registry.Register(StreamKind.Ticker, bytes =>
        {
            var dto = DeserializeData<StreamTickerDto>(bytes)!;
            return mapper.Map<StreamTickerDto, Ticker>(dto);
        });

        // ── Trade ─────────────────────────────────────────────────────────────
        // Trade mapping is hand-written (DeltaMapper not used for trade projection;
        // matches the REST convention: FillDto -> Trade is skipped in the profile).
        registry.Register(StreamKind.Trade, bytes =>
        {
            var dto = DeserializeData<StreamTradeDto>(bytes)!;
            var symbol = symbolMapper.FromWire(dto.Symbol);
            return new Trade(
                Symbol: symbol,
                Id: dto.TradeId.ToString(),
                Price: BinanceValueParsers.ParseDecimal(dto.Price),
                Quantity: BinanceValueParsers.ParseDecimal(dto.Quantity),
                Timestamp: dto.TradeTime > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(dto.TradeTime)
                    : null,
                IsBuyerMaker: dto.IsBuyerMaker);
        });

        // ── OrderBook ─────────────────────────────────────────────────────────
        // Hand-mapped following the REST GetOrderBookAsync convention.
        // The diff-depth ('@depth') data payload carries the symbol in 's'; the partial-book
        // ('@depthN') payload does not, so fall back to the combined-stream 'stream' token
        // (e.g. "btcusdt@depth20"), which always names the symbol.
        registry.Register(StreamKind.OrderBook, bytes =>
        {
            var (dto, streamToken) = DeserializeDepth(bytes);
            var wire = !string.IsNullOrEmpty(dto.Symbol)
                ? dto.Symbol
                : WireSymbolFromStreamToken(streamToken)
                    ?? throw new InvalidOperationException(
                        "Order-book depth frame carries neither a 's' symbol field nor a resolvable " +
                        "'stream' token; cannot resolve the symbol.");
            var symbol = symbolMapper.FromWire(wire);
            var bids = dto.Bids
                .Select(b => new OrderBookEntry(
                    BinanceValueParsers.ParseDecimal(b[0]),
                    BinanceValueParsers.ParseDecimal(b[1])))
                .ToList();
            var asks = dto.Asks
                .Select(a => new OrderBookEntry(
                    BinanceValueParsers.ParseDecimal(a[0]),
                    BinanceValueParsers.ParseDecimal(a[1])))
                .ToList();
            return new OrderBook(symbol, bids, asks, null, dto.LastUpdateId);
        });

        // ── Kline ─────────────────────────────────────────────────────────────
        // Hand-mapped following the REST GetCandlesticksAsync convention.
        registry.Register(StreamKind.Kline, bytes =>
        {
            var dto = DeserializeData<StreamKlineDto>(bytes)!;
            var k = dto.Kline;
            var interval = MapWireInterval(k.Interval);
            return new Candlestick(
                OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(k.OpenTime),
                CloseTime: DateTimeOffset.FromUnixTimeMilliseconds(k.CloseTime),
                Open: BinanceValueParsers.ParseDecimal(k.Open),
                High: BinanceValueParsers.ParseDecimal(k.High),
                Low: BinanceValueParsers.ParseDecimal(k.Low),
                Close: BinanceValueParsers.ParseDecimal(k.Close),
                Volume: BinanceValueParsers.ParseDecimal(k.Volume),
                QuoteVolume: BinanceValueParsers.ParseDecimal(k.QuoteVolume),
                TradeCount: k.TradeCount,
                Interval: interval);
        });

        return registry;
    }

    private static KlineInterval? MapWireInterval(string wire) => wire switch
    {
        "1m" => KlineInterval.OneMinute,
        "3m" => KlineInterval.ThreeMinutes,
        "5m" => KlineInterval.FiveMinutes,
        "15m" => KlineInterval.FifteenMinutes,
        "30m" => KlineInterval.ThirtyMinutes,
        "1h" => KlineInterval.OneHour,
        "2h" => KlineInterval.TwoHours,
        "4h" => KlineInterval.FourHours,
        "6h" => KlineInterval.SixHours,
        "8h" => KlineInterval.EightHours,
        "12h" => KlineInterval.TwelveHours,
        "1d" => KlineInterval.OneDay,
        "3d" => KlineInterval.ThreeDays,
        "1w" => KlineInterval.OneWeek,
        "1M" => KlineInterval.OneMonth,
        _ => null
    };

    // The /stream endpoint is always combined-stream: every data frame is the envelope
    // {"stream":"<token>","data":{...}}. The engine pump passes the whole envelope here, so
    // the leaf DTO lives under "data" — deserializing the envelope directly leaves every field
    // unset. Unwrap "data" first; a missing "data" is a malformed combined-stream frame.
    private static T? DeserializeData<T>(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("data"u8, out var dataProp))
            throw new InvalidOperationException(
                "Combined-stream frame has no 'data' element; cannot decode the payload.");
        return dataProp.Deserialize<T>(JsonOpts);
    }

    // Order books need both the unwrapped "data" payload and the "stream" token: partial-book
    // ('@depthN') frames omit the symbol from "data", so the token is the only symbol source.
    private static (StreamDepthDto Dto, string? StreamToken) DeserializeDepth(ReadOnlyMemory<byte> frame)
    {
        var reader = new Utf8JsonReader(frame.Span);
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data"u8, out var dataProp))
            throw new InvalidOperationException(
                "Combined-stream frame has no 'data' element; cannot decode the order book.");
        var streamToken = root.TryGetProperty("stream"u8, out var streamProp)
            ? streamProp.GetString()
            : null;
        return (dataProp.Deserialize<StreamDepthDto>(JsonOpts)!, streamToken);
    }

    // Extracts the wire symbol from a combined-stream token (e.g. "btcusdt@depth20" -> "BTCUSDT").
    // The token is lower-cased on the wire; FromWire's warm table is keyed upper-case.
    private static string? WireSymbolFromStreamToken(string? streamToken)
    {
        if (string.IsNullOrEmpty(streamToken))
            return null;
        var at = streamToken.IndexOf('@', StringComparison.Ordinal);
        var symbol = at > 0 ? streamToken[..at] : streamToken;
        return symbol.Length > 0 ? symbol.ToUpperInvariant() : null;
    }
}
