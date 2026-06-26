using System.Text;
using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Coinbase;
using CryptoExchanges.Net.Coinbase.Internal;
using CryptoExchanges.Net.Coinbase.Mapping;
using CryptoExchanges.Net.Coinbase.Streaming;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Coinbase.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests for the four Coinbase stream decoder closures. Each feeds a full Coinbase push-frame
/// envelope (with the outer <c>{"channel":"...","events":[...]}</c> wrapper) through the registered closure
/// and asserts the resulting <c>Core.Models</c> values.
/// </summary>
[Trait("Category", "Unit")]
public class CoinbaseStreamDecodeTests
{
    private static readonly Symbol BtcUsd = new(Asset.Btc, Asset.Of("USD"));

    private static (IMapper mapper, ISymbolMapper symbolMapper) BuildMappers()
    {
        var symbolMapper = new SymbolMapper(CoinbaseSymbolFormat.Instance);
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsd, [OrderType.Limit, OrderType.Market])]);
        var mapper = CoinbaseClientComposer.CreateMapper(symbolMapper);
        return (mapper, symbolMapper);
    }

    private static StreamDecoderRegistry BuildRegistry()
    {
        var (mapper, symbolMapper) = BuildMappers();
        return CoinbaseStreamDecoders.Build(mapper, symbolMapper);
    }

    private static ReadOnlyMemory<byte> Utf8Bytes(string json) =>
        new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Ticker_CannedFrame_MapsLastPriceAndSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        // Real Coinbase ticker format: events[0].tickers[0] holds the data.
        var frame = Utf8Bytes(
            "{\"channel\":\"ticker\",\"events\":[{\"type\":\"snapshot\",\"tickers\":[{\"product_id\":\"BTC-USD\"," +
            "\"price\":\"67000.00\",\"price_percent_chg_24h\":\"1.5\"," +
            "\"high_24h\":\"68000.00\",\"low_24h\":\"64000.00\"," +
            "\"volume_24h\":\"12345.678\",\"volume_24h_usd\":\"826000000.00\"," +
            "\"time\":\"2024-06-19T00:00:01Z\"}]}]}");

        var result = (Ticker)decoder(frame);

        result.Symbol.Should().Be(BtcUsd);
        result.LastPrice.Should().Be(67000.00m);
        result.HighPrice.Should().Be(68000.00m);
        result.LowPrice.Should().Be(64000.00m);
        result.Volume.Should().Be(12345.678m);
        result.PriceChangePercent.Should().Be(1.5m);
        result.Timestamp.Should().NotBeNull();
    }

    [Fact]
    public void Trade_SellSide_IsBuyerMakerIsTrue()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // Coinbase "side" is taker side: SELL taker ⇒ buyer is the maker.
        var frame = Utf8Bytes(
            "{\"channel\":\"market_trades\",\"events\":[{\"product_id\":\"BTC-USD\"," +
            "\"trades\":[{\"trade_id\":\"trade-sell-123\",\"product_id\":\"BTC-USD\"," +
            "\"price\":\"67050.00\",\"size\":\"0.001\",\"side\":\"SELL\"," +
            "\"time\":\"2024-06-19T00:00:01Z\"}]}]}");

        var result = (Trade)decoder(frame);

        result.Symbol.Should().Be(BtcUsd);
        result.Id.Should().Be("trade-sell-123");
        result.Price.Should().Be(67050.00m);
        result.Quantity.Should().Be(0.001m);
        result.IsBuyerMaker.Should().BeTrue();
        result.Timestamp.Should().NotBeNull();
    }

    [Fact]
    public void Trade_BuySide_IsBuyerMakerIsFalse()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        var frame = Utf8Bytes(
            "{\"channel\":\"market_trades\",\"events\":[{\"product_id\":\"BTC-USD\"," +
            "\"trades\":[{\"trade_id\":\"trade-buy-456\",\"product_id\":\"BTC-USD\"," +
            "\"price\":\"66990.00\",\"size\":\"0.002\",\"side\":\"BUY\"," +
            "\"time\":\"2024-06-19T00:00:02Z\"}]}]}");

        var result = (Trade)decoder(frame);

        result.IsBuyerMaker.Should().BeFalse();
        result.Price.Should().Be(66990.00m);
    }

    [Fact]
    public void Trade_MultipleTradesArray_EmitsLatestTrade()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        var frame = Utf8Bytes(
            "{\"channel\":\"market_trades\",\"events\":[{\"product_id\":\"BTC-USD\"," +
            "\"trades\":[" +
            "{\"trade_id\":\"old\",\"product_id\":\"BTC-USD\",\"price\":\"100.00\",\"size\":\"0.001\",\"side\":\"BUY\",\"time\":\"2024-01-01T00:00:01Z\"}," +
            "{\"trade_id\":\"latest\",\"product_id\":\"BTC-USD\",\"price\":\"200.00\",\"size\":\"0.002\",\"side\":\"BUY\",\"time\":\"2024-01-01T00:00:02Z\"}" +
            "]}]}");

        var result = (Trade)decoder(frame);

        result.Id.Should().Be("latest");
        result.Price.Should().Be(200.00m);
    }

    [Fact]
    public void OrderBook_SnapshotFrame_MapsBidsAsksAndSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        var frame = Utf8Bytes(
            "{\"channel\":\"l2_data\",\"events\":[{\"product_id\":\"BTC-USD\"," +
            "\"updates\":[" +
            "{\"side\":\"bid\",\"price_level\":\"66990.00\",\"new_quantity\":\"0.5\"}," +
            "{\"side\":\"bid\",\"price_level\":\"66980.00\",\"new_quantity\":\"1.2\"}," +
            "{\"side\":\"offer\",\"price_level\":\"67010.00\",\"new_quantity\":\"0.3\"}," +
            "{\"side\":\"offer\",\"price_level\":\"67020.00\",\"new_quantity\":\"0.8\"}" +
            "]}]}");

        var result = (OrderBook)decoder(frame);

        result.Symbol.Should().Be(BtcUsd);
        result.Bids.Should().HaveCount(2);
        result.Bids[0].Price.Should().Be(66990.00m);
        result.Bids[0].Quantity.Should().Be(0.5m);
        result.Bids[1].Price.Should().Be(66980.00m);
        result.Asks.Should().HaveCount(2);
        result.Asks[0].Price.Should().Be(67010.00m);
        result.Asks[0].Quantity.Should().Be(0.3m);
    }

    [Fact]
    public void Kline_CannedFrame_MapsOhlcvAndInterval()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        // candles channel always delivers 1-minute intervals on Coinbase Advanced Trade.
        var frame = Utf8Bytes(
            "{\"channel\":\"candles\",\"events\":[{\"candles\":[{" +
            "\"product_id\":\"BTC-USD\",\"start\":\"1718784000\"," +
            "\"open\":\"66900.00\",\"high\":\"67100.00\",\"low\":\"66850.00\"," +
            "\"close\":\"67050.00\",\"volume\":\"45.678\"}]}]}");

        var result = (Candlestick)decoder(frame);

        result.Open.Should().Be(66900.00m);
        result.High.Should().Be(67100.00m);
        result.Low.Should().Be(66850.00m);
        result.Close.Should().Be(67050.00m);
        result.Volume.Should().Be(45.678m);
        result.Interval.Should().Be(KlineInterval.OneMinute);
        result.OpenTime.ToUnixTimeSeconds().Should().Be(1718784000L);
    }

    [Fact]
    public void Ticker_EmptyEventsArray_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        var frame = Utf8Bytes("{\"channel\":\"ticker\",\"events\":[]}");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("empty");
    }

    [Fact]
    public void Ticker_NullNestedArrayElement_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        // A null tickers[0] element must surface a clear decode exception, never an opaque NRE.
        var frame = Utf8Bytes("{\"channel\":\"ticker\",\"events\":[{\"type\":\"snapshot\",\"tickers\":[null]}]}");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("null");
    }

    [Fact]
    public void OrderBook_NullEventElement_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        var frame = Utf8Bytes("{\"channel\":\"l2_data\",\"events\":[null]}");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("null");
    }

    [Fact]
    public void Kline_EmptyCandlesArray_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        var frame = Utf8Bytes("{\"channel\":\"candles\",\"events\":[{\"candles\":[]}]}");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("empty");
    }
}
