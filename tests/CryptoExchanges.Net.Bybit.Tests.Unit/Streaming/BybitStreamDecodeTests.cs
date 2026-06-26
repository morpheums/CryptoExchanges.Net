using System.Text;
using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Bybit;
using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Bybit.Streaming;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Bybit.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests for the four Bybit stream decoder closures. Each feeds a full
/// Bybit v5 push-frame envelope (the exact shape the engine pump delivers), including the
/// outer <c>{"topic":...,"type":...,"data":...}</c> wrapper, through the registered closure
/// and asserts the resulting <c>Core.Models</c> values.
/// </summary>
[Trait("Category", "Unit")]
public class BybitStreamDecodeTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (IMapper mapper, ISymbolMapper symbolMapper) BuildMappers()
    {
        // Use the same composition path as production to exercise the real DeltaMapper profile.
        var symbolMapper = new SymbolMapper(BybitSymbolFormat.Instance);
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit])]);
        var mapper = BybitClientComposer.CreateMapper(symbolMapper);
        return (mapper, symbolMapper);
    }

    private static StreamDecoderRegistry BuildRegistry()
    {
        var (mapper, symbolMapper) = BuildMappers();
        return BybitStreamDecoders.Build(mapper, symbolMapper);
    }

    private static ReadOnlyMemory<byte> Utf8Bytes(string json) =>
        new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json));

    // Wraps a leaf payload in the Bybit v5 push-frame envelope the engine delivers to decoders.
    private static ReadOnlyMemory<byte> Envelope(string topic, string type, string dataJson) =>
        Utf8Bytes($"{{\"topic\":\"{topic}\",\"type\":\"{type}\",\"ts\":1700000000000,\"data\":{dataJson}}}");

    [Fact]
    public void Ticker_CannedSnapshotFrame_MapsAllFields()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        // Full Bybit v5 ticker push envelope (data is a single object).
        var frame = Envelope("tickers.BTCUSDT", "snapshot",
            "{\"symbol\":\"BTCUSDT\",\"lastPrice\":\"67000.00\"," +
            "\"highPrice24h\":\"68000.00\",\"lowPrice24h\":\"64000.00\"," +
            "\"volume24h\":\"12345.678\",\"turnover24h\":\"820000000.00\"," +
            "\"prevPrice24h\":\"65000.00\",\"price24hPcnt\":\"0.0308\"}");

        var result = (Ticker)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastPrice.Should().Be(67000.00m);
        result.OpenPrice.Should().Be(65000.00m);
        result.HighPrice.Should().Be(68000.00m);
        result.LowPrice.Should().Be(64000.00m);
        result.Volume.Should().Be(12345.678m);
        result.QuoteVolume.Should().Be(820000000.00m);
        // PriceChange = lastPrice - prevPrice24h
        result.PriceChange.Should().Be(2000.00m);
        // PriceChangePercent = price24hPcnt * 100
        result.PriceChangePercent.Should().Be(3.08m);
    }

    [Fact]
    public void Trade_CannedFrame_BuySide_MapsAllFields()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // Full Bybit v5 publicTrade push envelope (data is an array of trade objects).
        // S == "Buy" means taker is a buyer, so the seller is the maker — IsBuyerMaker = false.
        var frame = Envelope("publicTrade.BTCUSDT", "snapshot",
            "[{\"T\":1718784001000,\"s\":\"BTCUSDT\",\"S\":\"Buy\"," +
            "\"v\":\"0.001\",\"p\":\"67050.00\",\"i\":\"trade-abc-123\"}]");

        var result = (Trade)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.Id.Should().Be("trade-abc-123");
        result.Price.Should().Be(67050.00m);
        result.Quantity.Should().Be(0.001m);
        result.IsBuyerMaker.Should().BeFalse();
        result.Timestamp.Should().NotBeNull();
        result.Timestamp!.Value.ToUnixTimeMilliseconds().Should().Be(1718784001000L);
    }

    [Fact]
    public void Trade_SellSide_IsBuyerMakerIsTrue()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // S == "Sell" means taker is a seller, so the buyer is the market maker — IsBuyerMaker = true.
        var frame = Envelope("publicTrade.BTCUSDT", "snapshot",
            "[{\"T\":1718784002000,\"s\":\"BTCUSDT\",\"S\":\"Sell\"," +
            "\"v\":\"0.002\",\"p\":\"66990.00\",\"i\":\"trade-sell-456\"}]");

        var result = (Trade)decoder(frame);

        result.IsBuyerMaker.Should().BeTrue();
        result.Price.Should().Be(66990.00m);
    }

    [Fact]
    public void Trade_MultiElementArray_EmitsLatestTrade()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // Bybit batches trades oldest→newest; v1 emits the most recent (last) entry per frame.
        var frame = Envelope("publicTrade.BTCUSDT", "snapshot",
            "[{\"T\":1718784003000,\"s\":\"BTCUSDT\",\"S\":\"Buy\",\"v\":\"0.001\",\"p\":\"100.00\",\"i\":\"old\"}," +
            "{\"T\":1718784003500,\"s\":\"BTCUSDT\",\"S\":\"Sell\",\"v\":\"0.002\",\"p\":\"200.00\",\"i\":\"mid\"}," +
            "{\"T\":1718784004000,\"s\":\"BTCUSDT\",\"S\":\"Buy\",\"v\":\"0.003\",\"p\":\"300.00\",\"i\":\"latest\"}]");

        var result = (Trade)decoder(frame);

        result.Id.Should().Be("latest");
        result.Price.Should().Be(300.00m);
    }

    [Fact]
    public void OrderBook_SnapshotFrame_MapsBidsAndAsksAndSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        // Full Bybit v5 orderbook snapshot envelope (data is a single object with "s","b","a","u","seq").
        var frame = Envelope("orderbook.50.BTCUSDT", "snapshot",
            "{\"s\":\"BTCUSDT\"," +
            "\"b\":[[\"66990.00\",\"0.5\"],[\"66980.00\",\"1.2\"]]," +
            "\"a\":[[\"67010.00\",\"0.3\"],[\"67020.00\",\"0.8\"]]," +
            "\"u\":1001,\"seq\":9900001}");

        var result = (OrderBook)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastUpdateId.Should().Be(1001L);
        result.Bids.Should().HaveCount(2);
        result.Bids[0].Price.Should().Be(66990.00m);
        result.Bids[0].Quantity.Should().Be(0.5m);
        result.Bids[1].Price.Should().Be(66980.00m);
        result.Asks.Should().HaveCount(2);
        result.Asks[0].Price.Should().Be(67010.00m);
        result.Asks[0].Quantity.Should().Be(0.3m);
    }

    [Fact]
    public void OrderBook_DeltaFrame_MapsBidsAndAsksAndSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        // Bybit v5 delta frames share the same data shape as snapshots; the "type" field
        // distinguishes them but the decoder treats both identically (no local-book maintenance).
        var frame = Envelope("orderbook.50.BTCUSDT", "delta",
            "{\"s\":\"BTCUSDT\"," +
            "\"b\":[[\"66990.00\",\"0.0\"]]," +
            "\"a\":[[\"67015.00\",\"0.5\"]]," +
            "\"u\":1002,\"seq\":9900002}");

        var result = (OrderBook)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastUpdateId.Should().Be(1002L);
        result.Bids.Should().ContainSingle();
        result.Asks.Should().ContainSingle();
        result.Asks[0].Price.Should().Be(67015.00m);
    }

    [Fact]
    public void Kline_CannedFrame_MapsOhlcvAndInterval()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        // Full Bybit v5 kline push envelope (data is an array of kline objects).
        // Bybit wire interval "1" maps to KlineInterval.OneMinute.
        var frame = Envelope("kline.1.BTCUSDT", "snapshot",
            "[{\"start\":1718784000000,\"open\":\"66900.00\",\"high\":\"67100.00\"," +
            "\"low\":\"66850.00\",\"close\":\"67050.00\",\"volume\":\"45.678\"," +
            "\"turnover\":\"3060000.00\",\"interval\":\"1\",\"confirm\":false}]");

        var result = (Candlestick)decoder(frame);

        result.Open.Should().Be(66900.00m);
        result.High.Should().Be(67100.00m);
        result.Low.Should().Be(66850.00m);
        result.Close.Should().Be(67050.00m);
        result.Volume.Should().Be(45.678m);
        result.QuoteVolume.Should().Be(3060000.00m);
        result.Interval.Should().Be(KlineInterval.OneMinute);
        result.OpenTime.ToUnixTimeMilliseconds().Should().Be(1718784000000L);
    }

    [Fact]
    public void Kline_ClosedBar_ConfirmTrue_StillDecodes()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        // confirm: true indicates a closed bar — decoder emits successfully regardless.
        var frame = Envelope("kline.60.BTCUSDT", "snapshot",
            "[{\"start\":1718784000000,\"open\":\"66900.00\",\"high\":\"67100.00\"," +
            "\"low\":\"66850.00\",\"close\":\"67050.00\",\"volume\":\"45.678\"," +
            "\"turnover\":\"3060000.00\",\"interval\":\"60\",\"confirm\":true}]");

        var result = (Candlestick)decoder(frame);

        result.Open.Should().Be(66900.00m);
        result.Interval.Should().Be(KlineInterval.OneHour);
    }

    [Fact]
    public void Ticker_NullData_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        // A null 'data' element must surface a clear decode exception, never an opaque NRE.
        var frame = Envelope("tickers.BTCUSDT", "snapshot", "null");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("null");
    }

    [Fact]
    public void Trade_NullDataElement_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        var frame = Envelope("publicTrade.BTCUSDT", "snapshot", "[null]");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("null");
    }

    [Fact]
    public void Kline_EmptyDataArray_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        var frame = Envelope("kline.60.BTCUSDT", "snapshot", "[]");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("empty");
    }
}
