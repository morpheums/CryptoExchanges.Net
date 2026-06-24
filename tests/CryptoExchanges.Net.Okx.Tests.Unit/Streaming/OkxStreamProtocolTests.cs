using System.Text;
using System.Text.Json;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Okx.Streaming;
using CryptoExchanges.Net.Http.Streaming;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Okx.Tests.Unit.Streaming;

[Trait("Category", "Unit")]
public class OkxStreamProtocolTests
{
    private static readonly StreamOptions DefaultOptions = new();

    private static OkxStreamProtocol MakeProtocol() => new(DefaultOptions);

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public void Classify_TickerDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"arg\":{\"channel\":\"tickers\",\"instId\":\"BTC-USDT\"},\"data\":[{\"instId\":\"BTC-USDT\"}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("tickers:BTC-USDT");
    }

    [Fact]
    public void Classify_TradeDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"arg\":{\"channel\":\"trades\",\"instId\":\"ETH-USDT\"},\"data\":[{\"instId\":\"ETH-USDT\"}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("trades:ETH-USDT");
    }

    [Fact]
    public void Classify_OrderBookDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"arg\":{\"channel\":\"books5\",\"instId\":\"BTC-USDT\"},\"data\":[{\"asks\":[]}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("books5:BTC-USDT");
    }

    [Fact]
    public void Classify_KlineDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"arg\":{\"channel\":\"candle1m\",\"instId\":\"BTC-USDT\"},\"data\":[[\"1700000000000\"]]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("candle1m:BTC-USDT");
    }

    [Fact]
    public void Classify_SubscribeAckFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"event\":\"subscribe\",\"arg\":{\"channel\":\"tickers\",\"instId\":\"BTC-USDT\"}}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Ack);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_UnsubscribeAckFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"event\":\"unsubscribe\",\"arg\":{\"channel\":\"tickers\",\"instId\":\"BTC-USDT\"}}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Ack);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_ErrorEventFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"event\":\"error\",\"code\":\"60018\",\"msg\":\"Invalid channel\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_TextPongFrame_ReturnsPong()
    {
        // OKX replies with bare-text "pong" (not JSON) to the client text "ping".
        var protocol = MakeProtocol();
        var frame = "pong"u8.ToArray();

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Pong);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_UnrecognisedFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"mystery\":true}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_EmptyFrame_ReturnsError()
    {
        var protocol = MakeProtocol();

        var result = protocol.Classify(ReadOnlySpan<byte>.Empty);

        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_InvalidJsonFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("not json at all");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_ArgFieldIsNotObject_ReturnsErrorAndDoesNotThrow()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"arg\":\"bad\",\"data\":[]}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_ArgMissingChannel_ReturnsErrorAndDoesNotThrow()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"arg\":{\"instId\":\"BTC-USDT\"},\"data\":[]}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_ChannelFieldIsNumber_ReturnsErrorAndDoesNotThrow()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"arg\":{\"channel\":42,\"instId\":\"BTC-USDT\"},\"data\":[]}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_EventFieldIsNumber_ReturnsErrorAndDoesNotThrow()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"event\":99}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void BuildSubscribe_Ticker_ProducesCorrectFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC-USDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("subscribe");
        var arg = doc.RootElement.GetProperty("args")[0];
        arg.GetProperty("channel").GetString().Should().Be("tickers");
        arg.GetProperty("instId").GetString().Should().Be("BTC-USDT");
    }

    [Fact]
    public void BuildSubscribe_Trade_ProducesCorrectFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETH-USDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("subscribe");
        var arg = doc.RootElement.GetProperty("args")[0];
        arg.GetProperty("channel").GetString().Should().Be("trades");
        arg.GetProperty("instId").GetString().Should().Be("ETH-USDT");
    }

    [Fact]
    public void BuildSubscribe_OrderBook_DefaultChannelIsBooks5()
    {
        // Confirmed: books5 is the top-5 order-book channel for OKX v5 public streams.
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTC-USDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("books5");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneMinute()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USDT", Interval: nameof(KlineInterval.OneMinute));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1m");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneHour()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USDT", Interval: nameof(KlineInterval.OneHour));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1H");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneDay()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USDT", Interval: nameof(KlineInterval.OneDay));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1D");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneWeek()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USDT", Interval: nameof(KlineInterval.OneWeek));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1W");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneMonth()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USDT", Interval: nameof(KlineInterval.OneMonth));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1M");
    }

    [Fact]
    public void BuildUnsubscribe_Ticker_ProducesUnsubscribeOp()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC-USDT");

        var wire = protocol.BuildUnsubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("unsubscribe");
        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("tickers");
        doc.RootElement.GetProperty("args")[0].GetProperty("instId").GetString().Should().Be("BTC-USDT");
    }

    [Fact]
    public void BuildUnsubscribe_Trade_ProducesCorrectFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETH-USDT");

        var wire = protocol.BuildUnsubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("unsubscribe");
        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("trades");
    }

    [Fact]
    public void BuildSubscribeBatch_MultipleRequests_EmitsOneFrameWithAllArgs()
    {
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTC-USDT"),
            new StreamRequest(StreamKind.Trade, "ETH-USDT"),
            new StreamRequest(StreamKind.OrderBook, "SOL-USDT"),
        };

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("op").GetString().Should().Be("subscribe");
        var args = doc.RootElement.GetProperty("args");
        args.GetArrayLength().Should().Be(3);
        args[0].GetProperty("channel").GetString().Should().Be("tickers");
        args[0].GetProperty("instId").GetString().Should().Be("BTC-USDT");
        args[1].GetProperty("channel").GetString().Should().Be("trades");
        args[1].GetProperty("instId").GetString().Should().Be("ETH-USDT");
        args[2].GetProperty("channel").GetString().Should().Be("books5");
        args[2].GetProperty("instId").GetString().Should().Be("SOL-USDT");
    }

    [Fact]
    public void BuildUnsubscribeBatch_MultipleRequests_EmitsOneUnsubscribeFrame()
    {
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTC-USDT"),
            new StreamRequest(StreamKind.Ticker, "ETH-USDT"),
        };

        var wire = protocol.BuildUnsubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("op").GetString().Should().Be("unsubscribe");
        var args = doc.RootElement.GetProperty("args");
        args.GetArrayLength().Should().Be(2);
        args[0].GetProperty("channel").GetString().Should().Be("tickers");
        args[1].GetProperty("channel").GetString().Should().Be("tickers");
    }

    [Fact]
    public void BuildSubscribeBatch_SingleRequest_EmitsSingleArgArray()
    {
        var protocol = MakeProtocol();
        var requests = new[] { new StreamRequest(StreamKind.Trade, "BTC-USDT") };

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("args").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("trades");
    }

    [Fact]
    public void BuildSubscribeBatch_OneHundredRequests_EmitsExactlyOneHundredArgs()
    {
        var protocol = MakeProtocol();
        var requests = Enumerable.Range(0, 100)
            .Select(i => new StreamRequest(StreamKind.Ticker, $"SYM{i}-USDT"))
            .ToArray();

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("args").GetArrayLength().Should().Be(100,
            "the engine pre-chunks at 100 — one OKX batch frame may carry exactly 100 args");
    }

    [Fact]
    public void BuildSubscribeBatch_EmptyList_ReturnsNull()
    {
        var protocol = MakeProtocol();
        protocol.BuildSubscribeBatch([]).Should().BeNull(
            "an empty request list has no batch frame to build");
    }

    [Fact]
    public void BuildUnsubscribeBatch_EmptyList_ReturnsNull()
    {
        var protocol = MakeProtocol();
        protocol.BuildUnsubscribeBatch([]).Should().BeNull(
            "an empty request list has no batch frame to build");
    }

    [Fact]
    public void RoutingKeyFor_MatchesClassify_DataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC-USDT");
        var frame = Utf8("{\"arg\":{\"channel\":\"tickers\",\"instId\":\"BTC-USDT\"},\"data\":[{\"instId\":\"BTC-USDT\"}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("tickers:BTC-USDT");
        classifiedKey.Should().Be("tickers:BTC-USDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Trade_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETH-USDT");
        var frame = Utf8("{\"arg\":{\"channel\":\"trades\",\"instId\":\"ETH-USDT\"},\"data\":[]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_OrderBook_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTC-USDT");
        var frame = Utf8("{\"arg\":{\"channel\":\"books5\",\"instId\":\"BTC-USDT\"},\"data\":[{\"asks\":[]}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("books5:BTC-USDT");
        classifiedKey.Should().Be("books5:BTC-USDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Kline_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USDT", Interval: nameof(KlineInterval.OneMinute));
        var frame = Utf8("{\"arg\":{\"channel\":\"candle1m\",\"instId\":\"BTC-USDT\"},\"data\":[[\"1700000000000\"]]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("candle1m:BTC-USDT");
        classifiedKey.Should().Be("candle1m:BTC-USDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsConfiguredEndpoint()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Should().Be(new Uri("wss://ws.okx.com:8443/ws/v5/public"));
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsClientPingDirection()
    {
        // Confirmed: OKX v5 expects the client to send "ping" every 25 s; server replies bare-text "pong".
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Direction.Should().Be(HeartbeatDirection.ClientPing);
    }

    [Fact]
    public async Task ResolveConnectionAsync_HeartbeatPingFormat_IsText()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.PingFormat.Should().Be(PingFormat.Text);
    }

    [Fact]
    public async Task ResolveConnectionAsync_HeartbeatInterval_Is25Seconds()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Interval.Should().Be(TimeSpan.FromSeconds(25));
    }

    [Fact]
    public async Task ResolveConnectionAsync_HeartbeatTimeout_Is35Seconds()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Timeout.Should().Be(TimeSpan.FromSeconds(35));
    }

    [Fact]
    public async Task ResolveConnectionAsync_PingPayload_IsPingText()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.ClientPingPayload.ToArray().Should().Equal("ping"u8.ToArray());
    }

    [Fact]
    public async Task ResolveConnectionAsync_MinOutboundInterval_Is100Ms()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.MinOutboundInterval.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsCachedInstance()
    {
        var protocol = MakeProtocol();
        var info1 = await protocol.ResolveConnectionAsync(CancellationToken.None);
        var info2 = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info1.Should().BeSameAs(info2);
    }

    [Fact]
    public async Task ResolveConnectionAsync_CustomBaseUrl_UsesConfiguredEndpoint()
    {
        var options = new StreamOptions { StreamBaseUrl = "wss://wspap.okx.com:8443/ws/v5/public?brokerId=9999" };
        var protocol = new OkxStreamProtocol(options);
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Should().Be(new Uri("wss://wspap.okx.com:8443/ws/v5/public?brokerId=9999"));
    }
}
