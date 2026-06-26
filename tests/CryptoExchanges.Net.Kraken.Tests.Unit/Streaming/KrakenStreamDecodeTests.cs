using System.Text;
using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Kraken;
using CryptoExchanges.Net.Kraken.Internal;
using CryptoExchanges.Net.Kraken.Streaming;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Kraken.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests for the four Kraken WS v2 stream decoder closures. Each feeds a full
/// push-frame envelope through the registered closure and asserts the resulting <c>Core.Models</c> values.
/// </summary>
[Trait("Category", "Unit")]
public class KrakenStreamDecodeTests
{
    // Kraken wire symbol for BTC/USD is XBT/USD (legacy ticker alias).
    private static readonly Symbol BtcUsd = new(Asset.Btc, Asset.Of("USD"));

    private static (IMapper mapper, ISymbolMapper symbolMapper) BuildMappers()
    {
        var symbolMapper = new SymbolMapper(KrakenSymbolFormat.Instance);
        // Seed the mapper with the XBT/USD wsname so FromWire("XBT/USD") returns BTC/USD.
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsd, [OrderType.Limit])]);
        var mapper = KrakenClientComposer.CreateMapper(symbolMapper);
        return (mapper, symbolMapper);
    }

    private static StreamDecoderRegistry BuildRegistry()
    {
        var (mapper, symbolMapper) = BuildMappers();
        return KrakenStreamDecoders.Build(mapper, symbolMapper);
    }

    private static ReadOnlyMemory<byte> Utf8Bytes(string json) =>
        new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json));

    // Wraps a leaf data payload in the Kraken WS v2 push-frame envelope the engine delivers to decoders.
    private static ReadOnlyMemory<byte> Envelope(string channel, string dataJson) =>
        Utf8Bytes($"{{\"channel\":\"{channel}\",\"type\":\"snapshot\",\"data\":{dataJson}}}");

    [Fact]
    public void Ticker_CannedFrame_MapsLastPriceAndSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        var frame = Envelope("ticker",
            "[{\"symbol\":\"XBT/USD\",\"last\":67000.00,\"high\":68000.00,\"low\":64000.00," +
            "\"volume\":12345.678,\"bid\":66999.00,\"ask\":67001.00,\"change\":100.00,\"change_pct\":0.15}]");

        var result = (Ticker)decoder(frame);

        result.Symbol.Should().Be(BtcUsd);
        result.LastPrice.Should().Be(67000.00m);
        result.HighPrice.Should().Be(68000.00m);
        result.LowPrice.Should().Be(64000.00m);
        result.Volume.Should().Be(12345.678m);
    }

    [Fact]
    public void Trade_CannedFrame_BuySide_MapsAllFields()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // "side" is the taker side: "buy" taker ⇒ seller is the maker, IsBuyerMaker = false.
        var frame = Envelope("trade",
            "[{\"symbol\":\"XBT/USD\",\"trade_id\":123456,\"price\":67050.00," +
            "\"qty\":0.001,\"side\":\"buy\",\"timestamp\":\"2024-06-19T08:00:01.000000Z\",\"ord_type\":\"limit\"}]");

        var result = (Trade)decoder(frame);

        result.Symbol.Should().Be(BtcUsd);
        result.Id.Should().Be("123456");
        result.Price.Should().Be(67050.00m);
        result.Quantity.Should().Be(0.001m);
        result.IsBuyerMaker.Should().BeFalse();
        result.Timestamp.Should().NotBeNull();
    }

    [Fact]
    public void Trade_SellSide_IsBuyerMakerIsTrue()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // "side" = "sell" taker ⇒ buyer is the maker, IsBuyerMaker = true.
        var frame = Envelope("trade",
            "[{\"symbol\":\"XBT/USD\",\"trade_id\":99,\"price\":66990.00," +
            "\"qty\":0.002,\"side\":\"sell\",\"timestamp\":\"2024-06-19T08:00:02.000000Z\",\"ord_type\":\"market\"}]");

        var result = (Trade)decoder(frame);

        result.IsBuyerMaker.Should().BeTrue();
        result.Price.Should().Be(66990.00m);
    }

    [Fact]
    public void Trade_MultiElementArray_EmitsLatestTrade()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // Kraken may batch multiple trades; decoder emits the most recent (last element).
        var frame = Envelope("trade",
            "[{\"symbol\":\"XBT/USD\",\"trade_id\":1,\"price\":100.00,\"qty\":0.001,\"side\":\"buy\",\"timestamp\":\"2024-01-01T00:00:01Z\",\"ord_type\":\"limit\"}," +
            "{\"symbol\":\"XBT/USD\",\"trade_id\":2,\"price\":200.00,\"qty\":0.002,\"side\":\"buy\",\"timestamp\":\"2024-01-01T00:00:02Z\",\"ord_type\":\"limit\"}," +
            "{\"symbol\":\"XBT/USD\",\"trade_id\":3,\"price\":300.00,\"qty\":0.003,\"side\":\"buy\",\"timestamp\":\"2024-01-01T00:00:03Z\",\"ord_type\":\"limit\"}]");

        var result = (Trade)decoder(frame);

        result.Id.Should().Be("3");
        result.Price.Should().Be(300.00m);
    }

    [Fact]
    public void OrderBook_SnapshotFrame_MapsBidsAsksAndSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        var frame = Envelope("book",
            "[{\"symbol\":\"XBT/USD\"," +
            "\"bids\":[{\"price\":66990.00,\"qty\":0.5},{\"price\":66980.00,\"qty\":1.2}]," +
            "\"asks\":[{\"price\":67010.00,\"qty\":0.3},{\"price\":67020.00,\"qty\":0.8}]," +
            "\"checksum\":9900001,\"timestamp\":\"2024-06-19T08:00:00Z\"}]");

        var result = (OrderBook)decoder(frame);

        result.Symbol.Should().Be(BtcUsd);
        // Kraken's checksum is a CRC, not a monotonic sequence id, so LastUpdateId stays null and
        // the frame timestamp populates Timestamp instead.
        result.LastUpdateId.Should().BeNull();
        result.Timestamp.Should().Be(DateTimeOffset.Parse("2024-06-19T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind));
        result.Bids.Should().HaveCount(2);
        result.Bids[0].Price.Should().Be(66990.00m);
        result.Bids[0].Quantity.Should().Be(0.5m);
        result.Bids[1].Price.Should().Be(66980.00m);
        result.Asks.Should().HaveCount(2);
        result.Asks[0].Price.Should().Be(67010.00m);
        result.Asks[0].Quantity.Should().Be(0.3m);
    }

    [Fact]
    public void Kline_CannedFrame_MapsOhlcvFromDto()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        var frame = Envelope("ohlc",
            "[{\"symbol\":\"XBT/USD\",\"open\":66900.00,\"high\":67100.00,\"low\":66850.00," +
            "\"close\":67050.00,\"volume\":45.678," +
            "\"timestamp\":\"2024-06-19T08:01:00.000000Z\"," +
            "\"interval_begin\":\"2024-06-19T08:00:00.000000Z\"}]");

        var result = (Candlestick)decoder(frame);

        result.Open.Should().Be(66900.00m);
        result.High.Should().Be(67100.00m);
        result.Low.Should().Be(66850.00m);
        result.Close.Should().Be(67050.00m);
        result.Volume.Should().Be(45.678m);
        result.OpenTime.Should().NotBe(DateTimeOffset.MinValue);
    }

    [Fact]
    public void Ticker_EmptyDataArray_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        var frame = Envelope("ticker", "[]");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("empty");
    }

    [Fact]
    public void Trade_NullDataElement_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // A null element must surface a clear decode exception, never an opaque NullReferenceException.
        var frame = Envelope("trade", "[null]");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("null");
    }

    [Fact]
    public void OrderBook_NullDataElement_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        var frame = Envelope("book", "[null]");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("null");
    }
}
