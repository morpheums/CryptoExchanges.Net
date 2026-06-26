using System.Text;
using Xunit;
using AwesomeAssertions;
using DeltaMapper;
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Binance.Internal;
using CryptoExchanges.Net.Binance.Streaming;
using CryptoExchanges.Net.Binance.Mapping;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Http.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Binance.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests for the four Binance stream decoder closures. Each feeds a full
/// combined-stream envelope (the exact shape the engine pump delivers), not bare data-level JSON.
/// </summary>
public class BinanceStreamDecodeTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private static (IMapper mapper, ISymbolMapper symbolMapper) BuildMappers()
    {
        var symbolMapper = new SymbolMapper(BinanceSymbolFormat.Instance);
        symbolMapper.UpdateSymbols([new SymbolInfo(BtcUsdt, [OrderType.Limit])]);
        var mapper = BinanceClientComposer.CreateMapper(symbolMapper);
        return (mapper, symbolMapper);
    }

    private static StreamDecoderRegistry BuildRegistry()
    {
        var (mapper, symbolMapper) = BuildMappers();
        return BinanceStreamDecoders.Build(mapper, symbolMapper);
    }

    private static ReadOnlyMemory<byte> Utf8Bytes(string json) =>
        new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json));

    // Wraps a leaf payload in the combined-stream envelope the engine actually delivers to decoders.
    private static ReadOnlyMemory<byte> Envelope(string streamToken, string dataJson) =>
        Utf8Bytes($"{{\"stream\":\"{streamToken}\",\"data\":{dataJson}}}");

    // ── Ticker ───────────────────────────────────────────────────────────────

    [Fact]
    public void Ticker_CannedFrame_MapsAllFields()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        // Full combined-stream ticker envelope (abbreviated data payload).
        var frame = Envelope("btcusdt@ticker",
            "{\"s\":\"BTCUSDT\",\"c\":\"67000.00\",\"o\":\"65000.00\",\"h\":\"68000.00\"," +
            "\"l\":\"64000.00\",\"v\":\"12345.678\",\"q\":\"820000000.00\"," +
            "\"p\":\"2000.00\",\"P\":\"3.08\",\"C\":1718784000000}");

        var result = (Ticker)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastPrice.Should().Be(67000.00m);
        result.OpenPrice.Should().Be(65000.00m);
        result.HighPrice.Should().Be(68000.00m);
        result.LowPrice.Should().Be(64000.00m);
        result.Volume.Should().Be(12345.678m);
        result.PriceChange.Should().Be(2000.00m);
        result.Timestamp.Should().NotBeNull();
    }

    // ── Trade ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Trade_CannedFrame_MapsAllFields()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Trade);

        var frame = Envelope("btcusdt@trade",
            "{\"s\":\"BTCUSDT\",\"t\":999001,\"p\":\"67050.00\",\"q\":\"0.001\"," +
            "\"T\":1718784001000,\"m\":false}");

        var result = (Trade)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.Id.Should().Be("999001");
        result.Price.Should().Be(67050.00m);
        result.Quantity.Should().Be(0.001m);
        result.IsBuyerMaker.Should().BeFalse();
        result.Timestamp.Should().NotBeNull();
    }

    // ── OrderBook ─────────────────────────────────────────────────────────────

    [Fact]
    public void OrderBook_DiffDepthEnvelope_MapsBidsAndAsks()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        // Diff-depth ('@depth') data payload — carries the symbol in "s".
        var frame = Envelope("btcusdt@depth",
            "{\"s\":\"BTCUSDT\",\"lastUpdateId\":99999," +
            "\"bids\":[[\"66990.00\",\"0.5\"],[\"66980.00\",\"1.2\"]]," +
            "\"asks\":[[\"67010.00\",\"0.3\"],[\"67020.00\",\"0.8\"]]}");

        var result = (OrderBook)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastUpdateId.Should().Be(99999L);
        result.Bids.Should().HaveCount(2);
        result.Bids[0].Price.Should().Be(66990.00m);
        result.Asks.Should().HaveCount(2);
        result.Asks[0].Price.Should().Be(67010.00m);
    }

    [Fact]
    public void OrderBook_PartialBookEnvelope_ResolvesSymbolFromStreamToken()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        // Partial-book ('@depth20') data payload — no "s"; the symbol lives only in the stream token.
        // This is the exact shape the live multi-symbol regression test subscribes to (depth: 20).
        var frame = Envelope("btcusdt@depth20",
            "{\"lastUpdateId\":12345," +
            "\"bids\":[[\"66990.00\",\"0.5\"]]," +
            "\"asks\":[[\"67010.00\",\"0.3\"]]}");

        var result = (OrderBook)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastUpdateId.Should().Be(12345L);
        result.Bids.Should().ContainSingle();
        result.Bids[0].Price.Should().Be(66990.00m);
        result.Asks.Should().ContainSingle();
        result.Asks[0].Price.Should().Be(67010.00m);
    }

    [Fact]
    public void OrderBook_NonStringStreamToken_DoesNotThrow_ResolvesFromDataSymbol()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        // A malformed/non-string "stream" must not throw from GetString(); the diff-depth
        // payload still carries "s", so the symbol resolves from the data element.
        var frame = Utf8Bytes(
            "{\"stream\":null,\"data\":{\"s\":\"BTCUSDT\",\"lastUpdateId\":42," +
            "\"bids\":[[\"66990.00\",\"0.5\"]],\"asks\":[[\"67010.00\",\"0.3\"]]}}");

        var result = (OrderBook)decoder(frame);

        result.Symbol.Should().Be(BtcUsdt);
        result.LastUpdateId.Should().Be(42L);
    }

    // ── Kline ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Kline_CannedFrame_MapsOhlcvAndInterval()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Kline);

        var frame = Envelope("btcusdt@kline_1m",
            "{\"s\":\"BTCUSDT\",\"k\":{\"t\":1718784000000,\"T\":1718784059999," +
            "\"o\":\"66900.00\",\"h\":\"67100.00\",\"l\":\"66850.00\",\"c\":\"67050.00\"," +
            "\"v\":\"45.678\",\"q\":\"3060000.00\",\"n\":1500,\"i\":\"1m\"}}");

        var result = (Candlestick)decoder(frame);

        result.Open.Should().Be(66900.00m);
        result.High.Should().Be(67100.00m);
        result.Low.Should().Be(66850.00m);
        result.Close.Should().Be(67050.00m);
        result.Volume.Should().Be(45.678m);
        result.TradeCount.Should().Be(1500);
        result.Interval.Should().Be(KlineInterval.OneMinute);
        result.OpenTime.ToUnixTimeMilliseconds().Should().Be(1718784000000L);
    }

    [Fact]
    public void Ticker_NullData_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.Ticker);

        // A null 'data' element must surface a clear decode exception, never an opaque NRE.
        var frame = Envelope("btcusdt@ticker", "null");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("null");
    }

    [Fact]
    public void OrderBook_NullData_ThrowsClearDecodeException_NotNre()
    {
        var registry = BuildRegistry();
        var decoder = registry.Resolve(StreamKind.OrderBook);

        var frame = Envelope("btcusdt@depth20", "null");

        var act = () => decoder(frame);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("null");
    }

    // ── DI resolution ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddBinanceStreams_AfterAddBinanceExchange_ResolvesKeyedStreamClient()
    {
        var services = new ServiceCollection();
        services.AddBinanceExchange();
        services.AddBinanceStreams();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IStreamClient>(ExchangeId.Binance);

        client.ExchangeId.Should().Be(ExchangeId.Binance);
    }
}
