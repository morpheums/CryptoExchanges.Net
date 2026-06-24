using System.Text;
using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Bitget;
using CryptoExchanges.Net.Bitget.Internal;
using CryptoExchanges.Net.Bitget.Streaming;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Bitget.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests for the four Bitget stream decoder closures. Each feeds a full
/// Bitget v2 push-frame envelope (the exact shape the engine pump delivers), including the
/// outer <c>{"action":...,"arg":{"instId":...},"data":[...]}</c> wrapper.
/// </summary>
[Trait("Category", "Unit")]
public class BitgetStreamDecodeTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (IMapper mapper, ISymbolMapper symbolMapper) BuildMappers()
    {
        var symbolMapper = new SymbolMapper(BitgetSymbolFormat.Instance);
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit])]);
        var mapper = BitgetClientComposer.CreateMapper(symbolMapper);
        return (mapper, symbolMapper);
    }

    private static StreamDecoderRegistry BuildRegistry()
    {
        var (mapper, symbolMapper) = BuildMappers();
        return BitgetStreamDecoders.Build(mapper, symbolMapper);
    }

    private static ReadOnlyMemory<byte> Utf8Bytes(string json) =>
        new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json));

    // Wraps a leaf data array in the Bitget v2 push envelope.
    private static ReadOnlyMemory<byte> Envelope(string channel, string instId, string dataJson, string action = "snapshot") =>
        Utf8Bytes($"{{\"action\":\"{action}\",\"arg\":{{\"instType\":\"SPOT\",\"channel\":\"{channel}\",\"instId\":\"{instId}\"}},\"data\":{dataJson}}}");

    [Fact]
    public void Ticker_CannedSnapshotFrame_MapsSymbolAndLastPrice()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        var frame = Envelope("ticker", "BTCUSDT",
            "[{\"instId\":\"BTCUSDT\",\"lastPr\":\"67000.00\",\"high24h\":\"68000.00\"," +
            "\"low24h\":\"64000.00\",\"baseVolume\":\"12345.678\",\"bidPr\":\"66999.00\"," +
            "\"askPr\":\"67001.00\",\"ts\":\"1718784000000\"}]");

        var result = (Ticker)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastPrice.Should().Be(67000.00m);
        result.HighPrice.Should().Be(68000.00m);
        result.LowPrice.Should().Be(64000.00m);
        result.Volume.Should().Be(12345.678m);
        result.Timestamp.Should().NotBeNull();
        result.Timestamp!.Value.ToUnixTimeMilliseconds().Should().Be(1718784000000L);
    }

    [Fact]
    public void Trade_CannedFrame_BuySide_MapsAllFields()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // side == "buy" means taker bought, so the seller was the resting maker — IsBuyerMaker = false.
        var frame = Envelope("trade", "BTCUSDT",
            "[{\"ts\":\"1718784001000\",\"price\":\"67050.00\",\"size\":\"0.001\"," +
            "\"side\":\"buy\",\"tradeId\":\"trade-abc-123\"}]");

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

        // side == "sell" means taker sold, so the buyer was the resting maker — IsBuyerMaker = true.
        var frame = Envelope("trade", "BTCUSDT",
            "[{\"ts\":\"1718784002000\",\"price\":\"66990.00\",\"size\":\"0.002\"," +
            "\"side\":\"sell\",\"tradeId\":\"trade-sell-456\"}]");

        var result = (Trade)decoder(frame);

        result.IsBuyerMaker.Should().BeTrue();
        result.Price.Should().Be(66990.00m);
    }

    [Fact]
    public void Trade_MultiElementArray_EmitsLatestTrade()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // Bitget batches trades oldest→newest; v1 emits only the most recent (last) per frame.
        var frame = Envelope("trade", "BTCUSDT",
            "[{\"ts\":\"1718784001000\",\"price\":\"100.00\",\"size\":\"0.001\",\"side\":\"buy\",\"tradeId\":\"old\"}," +
            "{\"ts\":\"1718784002000\",\"price\":\"200.00\",\"size\":\"0.002\",\"side\":\"sell\",\"tradeId\":\"mid\"}," +
            "{\"ts\":\"1718784003000\",\"price\":\"300.00\",\"size\":\"0.003\",\"side\":\"buy\",\"tradeId\":\"latest\"}]");

        var result = (Trade)decoder(frame);

        result.Id.Should().Be("latest");
        result.Price.Should().Be(300.00m);
    }

    [Fact]
    public void OrderBook_SnapshotFrame_MapsBidsAsksAndSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        var frame = Envelope("books5", "BTCUSDT",
            "[{\"bids\":[[\"66990.00\",\"0.5\"],[\"66980.00\",\"1.2\"]]," +
            "\"asks\":[[\"67010.00\",\"0.3\"],[\"67020.00\",\"0.8\"]]," +
            "\"ts\":\"1718784000000\",\"seqId\":9900001}]");

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
    public void OrderBook_UpdateFrame_MapsBidsAsksAndSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        // action == "update" — the decoder treats snapshot and update identically (no local-book maintenance).
        var frame = Envelope("books5", "BTCUSDT",
            "[{\"bids\":[[\"66990.00\",\"0.0\"]]," +
            "\"asks\":[[\"67015.00\",\"0.5\"]]," +
            "\"ts\":\"1718784001000\",\"seqId\":9900002}]",
            action: "update");

        var result = (OrderBook)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastUpdateId.Should().Be(9900002L);
        result.Bids.Should().ContainSingle();
        result.Asks.Should().ContainSingle();
        result.Asks[0].Price.Should().Be(67015.00m);
    }

    [Fact]
    public void Kline_CannedFrame_MapsOhlcvFromPositionalRow()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        // Bitget kline data is an array of positional arrays: [ts,open,high,low,close,baseVol,quoteVol,...].
        var frame = Envelope("candle1m", "BTCUSDT",
            "[[\"1718784000000\",\"66900.00\",\"67100.00\",\"66850.00\",\"67050.00\",\"45.678\",\"3060000.00\"]]");

        var result = (Candlestick)decoder(frame);

        result.Open.Should().Be(66900.00m);
        result.High.Should().Be(67100.00m);
        result.Low.Should().Be(66850.00m);
        result.Close.Should().Be(67050.00m);
        result.Volume.Should().Be(45.678m);
        result.QuoteVolume.Should().Be(3060000.00m);
        result.OpenTime.ToUnixTimeMilliseconds().Should().Be(1718784000000L);
    }

    [Fact]
    public void Kline_SymbolResolvedFromArgInstId()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        var frame = Envelope("candle1m", "BTCUSDT",
            "[[\"1718784000000\",\"66900.00\",\"67100.00\",\"66850.00\",\"67050.00\",\"45.678\",\"3060000.00\"]]");

        var result = (Candlestick)decoder(frame);

        result.Open.Should().BePositive();
        result.High.Should().BePositive();
        result.Low.Should().BePositive();
        result.Close.Should().BePositive();
        result.Volume.Should().BePositive();
    }
}
