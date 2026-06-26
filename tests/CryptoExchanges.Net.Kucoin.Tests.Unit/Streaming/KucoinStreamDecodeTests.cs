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

    [Fact]
    public void Ticker_CannedInnerPayload_MapsAllFields()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        // Bare inner data.data payload (as observed live from /market/snapshot:BTC-USDT).
        // All numeric fields are JSON numbers, not strings.
        var data = Utf8Bytes(
            "{\"symbol\":\"BTC-USDT\",\"lastTradedPrice\":64052.6," +
            "\"buy\":64055.4,\"sell\":64055.5," +
            "\"high\":64583.1,\"low\":63730.7,\"open\":63958.2," +
            "\"vol\":1938.61,\"volValue\":124352841.5," +
            "\"changePrice\":94.4,\"changeRate\":0.0014," +
            "\"datetime\":1782053405029}");

        var result = (Ticker)decoder(data);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastPrice.Should().Be(64052.6m);
        result.OpenPrice.Should().Be(63958.2m);
        result.HighPrice.Should().Be(64583.1m);
        result.LowPrice.Should().Be(63730.7m);
        result.Volume.Should().Be(1938.61m);
        result.QuoteVolume.Should().Be(124352841.5m);
        result.PriceChange.Should().Be(94.4m);
        result.PriceChangePercent.Should().Be(0.0014m * 100m);
        result.Timestamp.Should().NotBeNull();
        result.Timestamp!.Value.ToUnixTimeMilliseconds().Should().Be(1782053405029L);
    }

    [Fact]
    public void Ticker_FullSnapshotFrame_DoubleNestedData_MapsAllFields()
    {
        // Full push frame from /market/snapshot — double-nested: data.data is the inner payload.
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        var fullFrame = Utf8Bytes(
            "{\"type\":\"message\",\"topic\":\"/market/snapshot:BTC-USDT\",\"subject\":\"trade.snapshot\"," +
            "\"data\":{\"sequence\":\"33762550100\"," +
            "\"data\":{\"symbol\":\"BTC-USDT\",\"lastTradedPrice\":64052.6," +
            "\"buy\":64055.4,\"sell\":64055.5," +
            "\"high\":64583.1,\"low\":63730.7,\"open\":63958.2," +
            "\"vol\":1938.61,\"volValue\":124352841.5," +
            "\"changePrice\":94.4,\"changeRate\":0.0014," +
            "\"datetime\":1782053405029}}}");

        var result = (Ticker)decoder(fullFrame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastPrice.Should().Be(64052.6m);
        result.OpenPrice.Should().Be(63958.2m);
        result.HighPrice.Should().Be(64583.1m);
        result.LowPrice.Should().Be(63730.7m);
        result.Volume.Should().Be(1938.61m);
        result.PriceChange.Should().Be(94.4m);
        result.Timestamp.Should().NotBeNull();
    }

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

    [Fact]
    public void Kline_CannedDataFrame_MapsOhlcv()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        // KuCoin candles data: [startAt(s), open, close, high, low, vol, quoteVol].
        // time is a JSON number (unix nanoseconds) per live wire — verified against real KuCoin WS.
        var data = Utf8Bytes(
            "{\"symbol\":\"BTC-USDT\"," +
            "\"candles\":[\"1589968800\",\"9786.9\",\"9740.8\",\"9790\",\"9738\",\"1006.95\",\"9839209.1\"]," +
            "\"time\":1589968800000000000}");

        var result = (Candlestick)decoder(data);

        result.Open.Should().Be(9786.9m);
        result.High.Should().Be(9790m);
        result.Low.Should().Be(9738m);
        result.Close.Should().Be(9740.8m);
        result.Volume.Should().Be(1006.95m);
        result.QuoteVolume.Should().Be(9839209.1m);
        result.OpenTime.ToUnixTimeSeconds().Should().Be(1589968800L);
    }

    [Fact]
    public void Ticker_NullInnerData_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        // A null data.data element must surface a clear decode exception, never an opaque NRE.
        var frame = Utf8Bytes("{\"data\":{\"data\":null}}");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("null");
    }

    [Fact]
    public void Trade_NullData_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        var frame = Utf8Bytes("{\"data\":null}");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("null");
    }

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
