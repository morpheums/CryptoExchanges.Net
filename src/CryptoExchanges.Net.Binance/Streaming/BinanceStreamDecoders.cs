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
            var dto = JsonSerializer.Deserialize<StreamTickerDto>(bytes.Span, JsonOpts)!;
            return mapper.Map<StreamTickerDto, Ticker>(dto);
        });

        // ── Trade ─────────────────────────────────────────────────────────────
        // Trade mapping is hand-written (DeltaMapper not used for trade projection;
        // matches the REST convention: FillDto -> Trade is skipped in the profile).
        registry.Register(StreamKind.Trade, bytes =>
        {
            var dto = JsonSerializer.Deserialize<StreamTradeDto>(bytes.Span, JsonOpts)!;
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
        // The diff-depth stream payload includes the symbol wire string; partial-book frames do not.
        registry.Register(StreamKind.OrderBook, bytes =>
        {
            var dto = JsonSerializer.Deserialize<StreamDepthDto>(bytes.Span, JsonOpts)!;
            var symbol = !string.IsNullOrEmpty(dto.Symbol)
                ? symbolMapper.FromWire(dto.Symbol)
                : throw new InvalidOperationException(
                    "Order-book depth frame does not carry a symbol field. " +
                    "Use the diff-depth stream (e.g. '@depth') which includes 's'.");
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
            var dto = JsonSerializer.Deserialize<StreamKlineDto>(bytes.Span, JsonOpts)!;
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
}
