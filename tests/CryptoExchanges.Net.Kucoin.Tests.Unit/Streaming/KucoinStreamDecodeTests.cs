using System.Text;
using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Kucoin;
using CryptoExchanges.Net.Kucoin.Internal;
using CryptoExchanges.Net.Kucoin.Streaming;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Http.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Kucoin.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests for the four KuCoin stream decoder closures.
/// Each test feeds a canned KuCoin push-frame <c>data</c> payload (raw bytes) through
/// the registered closure and asserts the resulting <see cref="Core.Models"/> values.
/// </summary>
[Trait("Category", "Unit")]
public class KucoinStreamDecodeTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (IMapper mapper, ISymbolMapper symbolMapper) BuildMappers()
    {
        var symbolMapper = new KucoinSymbolMapper();
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit])]);
        var mapper = KucoinClientComposer.CreateMapper(symbolMapper);
        return (mapper, symbolMapper);
    }

    private static StreamDecoderRegistry BuildRegistry()
    {
        var (mapper, symbolMapper) = BuildMappers();
        return KucoinStreamDecoders.Build(mapper, symbolMapper);
    }

    private static ReadOnlyMemory<byte> Utf8Bytes(string json) =>
        new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json));

    // ── Ticker ────────────────────────────────────────────────────────────────

    [Fact]
    public void Ticker_CannedDataFrame_MapsAllFields()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        // KuCoin ticker data payload (inner "data" object of a push frame).
        var data = Utf8Bytes(
            "{\"sequence\":\"1720000000000\",\"price\":\"67000.00\",\"bestBid\":\"66999.00\"," +
            "\"bestBidSize\":\"0.5\",\"bestAsk\":\"67001.00\",\"bestAskSize\":\"0.3\"," +
            "\"open\":\"65000.00\",\"high\":\"68000.00\",\"low\":\"64000.00\"," +
            "\"vol\":\"12345.678\",\"volValue\":\"820000000.00\"," +
            "\"symbol\":\"BTC-USDT\",\"time\":\"1718784000000000000\"}");

        var result = (Ticker)decoder(data);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastPrice.Should().Be(67000.00m);
        result.OpenPrice.Should().Be(65000.00m);
        result.HighPrice.Should().Be(68000.00m);
        result.LowPrice.Should().Be(64000.00m);
        result.Volume.Should().Be(12345.678m);
        result.PriceChange.Should().Be(67000.00m - 65000.00m);
        result.Timestamp.Should().NotBeNull();
    }

    [Fact]
    public void Ticker_FullFrame_ExtractsDataAndMapsAllFields()
    {
        // Test that the decoder correctly extracts the "data" field from a full push frame.
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        var fullFrame = Utf8Bytes(
            "{\"type\":\"message\",\"topic\":\"/market/ticker:BTC-USDT\",\"subject\":\"trade.ticker\"," +
            "\"data\":{\"sequence\":\"1720000000000\",\"price\":\"67000.00\",\"bestBid\":\"66999.00\"," +
            "\"bestBidSize\":\"0.5\",\"bestAsk\":\"67001.00\",\"bestAskSize\":\"0.3\"," +
            "\"open\":\"65000.00\",\"high\":\"68000.00\",\"low\":\"64000.00\"," +
            "\"vol\":\"12345.678\",\"volValue\":\"820000000.00\"," +
            "\"symbol\":\"BTC-USDT\",\"time\":\"1718784000000000000\"}}");

        var result = (Ticker)decoder(fullFrame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastPrice.Should().Be(67000.00m);
    }

    // ── Trade ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Trade_CannedDataFrame_MapsAllFields()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        var data = Utf8Bytes(
            "{\"sequence\":\"1720000000001\",\"type\":\"match\",\"symbol\":\"BTC-USDT\"," +
            "\"side\":\"buy\",\"price\":\"67050.00\",\"size\":\"0.001\"," +
            "\"tradeId\":\"trade-abc-123\"," +
            "\"makerOrderId\":\"maker-id\",\"takerOrderId\":\"taker-id\"," +
            "\"time\":\"1718784001000000000\"}");

        var result = (Trade)decoder(data);

        result.Symbol.Should().Be(BtcUsdt);
        result.Id.Should().Be("trade-abc-123");
        result.Price.Should().Be(67050.00m);
        result.Quantity.Should().Be(0.001m);
        result.Timestamp.Should().NotBeNull();
        // Taker side is "buy" → buyer is the aggressor → IsBuyerMaker = false
        result.IsBuyerMaker.Should().BeFalse();
    }

    [Fact]
    public void Trade_SellSide_IsBuyerMakerIsTrue()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        // Taker side "sell" → seller is aggressor, buyer is maker → IsBuyerMaker = true
        var data = Utf8Bytes(
            "{\"sequence\":\"1720000000002\",\"type\":\"match\",\"symbol\":\"BTC-USDT\"," +
            "\"side\":\"sell\",\"price\":\"66990.00\",\"size\":\"0.002\"," +
            "\"tradeId\":\"trade-sell-456\"," +
            "\"makerOrderId\":\"maker-id\",\"takerOrderId\":\"taker-id\"," +
            "\"time\":\"1718784002000000000\"}");

        var result = (Trade)decoder(data);

        result.IsBuyerMaker.Should().BeTrue();
    }

    // ── OrderBook ─────────────────────────────────────────────────────────────

    [Fact]
    public void OrderBook_CannedDataFrame_MapsBidsAndAsks()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        // KuCoin level2 diff frame data payload.
        var data = Utf8Bytes(
            "{\"sequenceStart\":3,\"sequenceEnd\":5,\"symbol\":\"BTC-USDT\"," +
            "\"changes\":{" +
            "\"bids\":[[\"66990.00\",\"0.5\",\"3\"],[\"66980.00\",\"1.2\",\"4\"]]," +
            "\"asks\":[[\"67010.00\",\"0.3\",\"3\"],[\"67020.00\",\"0.8\",\"5\"]]}}");

        var result = (OrderBook)decoder(data);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastUpdateId.Should().Be(5L);
        result.Bids.Should().HaveCount(2);
        result.Bids[0].Price.Should().Be(66990.00m);
        result.Bids[0].Quantity.Should().Be(0.5m);
        result.Asks.Should().HaveCount(2);
        result.Asks[0].Price.Should().Be(67010.00m);
        result.Asks[0].Quantity.Should().Be(0.3m);
    }

    // ── Kline ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Kline_CannedDataFrame_MapsOhlcv()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        // KuCoin candles data: [startAt(s), open, close, high, low, vol, quoteVol]
        var data = Utf8Bytes(
            "{\"symbol\":\"BTC-USDT\"," +
            "\"candles\":[\"1589968800\",\"9786.9\",\"9740.8\",\"9790\",\"9738\",\"1006.95\",\"9839209.1\"]," +
            "\"time\":\"1589968800000000000\"}");

        var result = (Candlestick)decoder(data);

        result.Open.Should().Be(9786.9m);
        result.High.Should().Be(9790m);
        result.Low.Should().Be(9738m);
        result.Close.Should().Be(9740.8m);
        result.Volume.Should().Be(1006.95m);
        result.QuoteVolume.Should().Be(9839209.1m);
        result.OpenTime.ToUnixTimeSeconds().Should().Be(1589968800L);
    }

    // ── DI resolution ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddKucoinStreams_AfterAddKucoinExchange_ResolvesKeyedStreamClient()
    {
        var services = new ServiceCollection();
        services.AddKucoinExchange();
        services.AddKucoinStreams();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IStreamClient>(ExchangeId.Kucoin);

        client.ExchangeId.Should().Be(ExchangeId.Kucoin);
    }
}
