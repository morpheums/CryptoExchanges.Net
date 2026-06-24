using System.Text;
using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Okx;
using CryptoExchanges.Net.Okx.Internal;
using CryptoExchanges.Net.Okx.Streaming;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Okx.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests for the four OKX stream decoder closures. Each feeds a full OKX push-frame
/// envelope (with the outer <c>{"arg":{...},"data":[...]}</c> wrapper) through the registered closure
/// and asserts the resulting <c>Core.Models</c> values.
/// </summary>
[Trait("Category", "Unit")]
public class OkxStreamDecodeTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (IMapper mapper, ISymbolMapper symbolMapper) BuildMappers()
    {
        var symbolMapper = new SymbolMapper(OkxSymbolFormat.Instance);
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit])]);
        var mapper = OkxClientComposer.CreateMapper(symbolMapper);
        return (mapper, symbolMapper);
    }

    private static StreamDecoderRegistry BuildRegistry()
    {
        var (mapper, symbolMapper) = BuildMappers();
        return OkxStreamDecoders.Build(mapper, symbolMapper);
    }

    private static ReadOnlyMemory<byte> Utf8Bytes(string json) =>
        new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json));

    // Wraps a leaf payload in the OKX push-frame envelope the engine delivers to decoders.
    private static ReadOnlyMemory<byte> Envelope(string channel, string instId, string dataJson) =>
        Utf8Bytes($"{{\"arg\":{{\"channel\":\"{channel}\",\"instId\":\"{instId}\"}},\"data\":{dataJson}}}");

    [Fact]
    public void Ticker_CannedFrame_MapsLastPriceAndSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        var frame = Envelope("tickers", "BTC-USDT",
            "[{\"instId\":\"BTC-USDT\",\"last\":\"67000.00\",\"high24h\":\"68000.00\"," +
            "\"low24h\":\"64000.00\",\"vol24h\":\"12345.678\",\"bidPx\":\"66999.00\"," +
            "\"askPx\":\"67001.00\",\"ts\":\"1700000000000\"}]");

        var result = (Ticker)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
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

        // OKX "side" is the taker side: "buy" taker ⇒ seller is the maker, IsBuyerMaker = false.
        var frame = Envelope("trades", "BTC-USDT",
            "[{\"instId\":\"BTC-USDT\",\"tradeId\":\"trade-abc-123\",\"px\":\"67050.00\"," +
            "\"sz\":\"0.001\",\"side\":\"buy\",\"ts\":\"1718784001000\"}]");

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

        // OKX "side" is the taker side: "sell" taker ⇒ buyer is the maker, IsBuyerMaker = true.
        var frame = Envelope("trades", "BTC-USDT",
            "[{\"instId\":\"BTC-USDT\",\"tradeId\":\"trade-sell-456\",\"px\":\"66990.00\"," +
            "\"sz\":\"0.002\",\"side\":\"sell\",\"ts\":\"1718784002000\"}]");

        var result = (Trade)decoder(frame);

        result.IsBuyerMaker.Should().BeTrue();
        result.Price.Should().Be(66990.00m);
    }

    [Fact]
    public void Trade_MultiElementArray_EmitsLatestTrade()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // OKX batches trades oldest→newest; v1 emits the most recent (last) entry per frame.
        var frame = Envelope("trades", "BTC-USDT",
            "[{\"instId\":\"BTC-USDT\",\"tradeId\":\"old\",\"px\":\"100.00\",\"sz\":\"0.001\",\"side\":\"buy\",\"ts\":\"1000\"}," +
            "{\"instId\":\"BTC-USDT\",\"tradeId\":\"mid\",\"px\":\"200.00\",\"sz\":\"0.002\",\"side\":\"buy\",\"ts\":\"2000\"}," +
            "{\"instId\":\"BTC-USDT\",\"tradeId\":\"latest\",\"px\":\"300.00\",\"sz\":\"0.003\",\"side\":\"buy\",\"ts\":\"3000\"}]");

        var result = (Trade)decoder(frame);

        result.Id.Should().Be("latest");
        result.Price.Should().Be(300.00m);
    }

    [Fact]
    public void OrderBook_SnapshotFrame_MapsBidsAsksAndSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        // OKX order-book data: each price level is [price, qty, liquidatedOrders, orders].
        var frame = Envelope("books5", "BTC-USDT",
            "[{\"bids\":[[\"66990.00\",\"0.5\",\"0\",\"1\"],[\"66980.00\",\"1.2\",\"0\",\"2\"]]," +
            "\"asks\":[[\"67010.00\",\"0.3\",\"0\",\"1\"],[\"67020.00\",\"0.8\",\"0\",\"2\"]]," +
            "\"ts\":\"1700000000000\",\"seqId\":9900001}]");

        var result = (OrderBook)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastUpdateId.Should().Be(9900001L);
        result.Bids.Should().HaveCount(2);
        result.Bids[0].Price.Should().Be(66990.00m);
        result.Bids[0].Quantity.Should().Be(0.5m);
        result.Bids[1].Price.Should().Be(66980.00m);
        result.Asks.Should().HaveCount(2);
        result.Asks[0].Price.Should().Be(67010.00m);
        result.Asks[0].Quantity.Should().Be(0.3m);
    }

    [Fact]
    public void Kline_CannedFrame_MapsOhlcvFromPositionalRow()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        // OKX kline rows are positional arrays: [ts,o,h,l,c,vol,volCcy,volCcyQuote,confirm].
        var frame = Envelope("candle1m", "BTC-USDT",
            "[[\"1718784000000\",\"66900.00\",\"67100.00\",\"66850.00\",\"67050.00\"," +
            "\"45.678\",\"3000000.00\",\"3060000.00\",\"0\"]]");

        var result = (Candlestick)decoder(frame);

        result.Open.Should().Be(66900.00m);
        result.High.Should().Be(67100.00m);
        result.Low.Should().Be(66850.00m);
        result.Close.Should().Be(67050.00m);
        result.Volume.Should().Be(45.678m);
        result.QuoteVolume.Should().Be(3060000.00m);
        result.OpenTime.ToUnixTimeMilliseconds().Should().Be(1718784000000L);
    }
}
