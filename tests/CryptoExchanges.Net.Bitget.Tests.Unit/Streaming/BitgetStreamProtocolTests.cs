using System.Text;
using System.Text.Json;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Bitget.Streaming;
using CryptoExchanges.Net.Http.Streaming;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Bitget.Tests.Unit.Streaming;

[Trait("Category", "Unit")]
public class BitgetStreamProtocolTests
{
    private static readonly StreamOptions DefaultOptions = new();

    private static BitgetStreamProtocol MakeProtocol() => new(DefaultOptions);

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Classify_TickerSnapshotFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"action\":\"snapshot\",\"arg\":{\"instType\":\"SPOT\",\"channel\":\"ticker\",\"instId\":\"BTCUSDT\"},\"data\":[{}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("ticker:BTCUSDT");
    }

    [Fact]
    public void Classify_TradeUpdateFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"action\":\"update\",\"arg\":{\"instType\":\"SPOT\",\"channel\":\"trade\",\"instId\":\"ETHUSDT\"},\"data\":[{}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("trade:ETHUSDT");
    }

    [Fact]
    public void Classify_OrderBookSnapshotFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"action\":\"snapshot\",\"arg\":{\"instType\":\"SPOT\",\"channel\":\"books5\",\"instId\":\"BTCUSDT\"},\"data\":[{}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("books5:BTCUSDT");
    }

    [Fact]
    public void Classify_OrderBookUpdateFrame_ReturnsDataWithSameRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"action\":\"update\",\"arg\":{\"instType\":\"SPOT\",\"channel\":\"books5\",\"instId\":\"BTCUSDT\"},\"data\":[{}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("books5:BTCUSDT",
            "snapshot and update frames share the same channel routing key — both classify as Data");
    }

    [Fact]
    public void Classify_KlineFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"action\":\"snapshot\",\"arg\":{\"instType\":\"SPOT\",\"channel\":\"candle1m\",\"instId\":\"BTCUSDT\"},\"data\":[[]]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("candle1m:BTCUSDT");
    }

    [Fact]
    public void Classify_SubscribeAckFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"event\":\"subscribe\",\"arg\":{\"instType\":\"SPOT\",\"channel\":\"ticker\",\"instId\":\"BTCUSDT\"},\"code\":\"0\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Ack);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_ErrorEventFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"event\":\"error\",\"code\":\"30001\",\"msg\":\"instId error\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_NonZeroCodeEventFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"event\":\"subscribe\",\"code\":\"40003\",\"msg\":\"Missing instId\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_TextPongFrame_ReturnsPong()
    {
        // Bitget v2: client sends text "ping"; server replies with bare text "pong".
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
    public void Classify_ActionFieldPresentButArgIsNotObject_ReturnsErrorAndDoesNotThrow()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"action\":\"snapshot\",\"arg\":\"bad\",\"data\":[]}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_EventFieldIsNumber_ReturnsErrorAndDoesNotThrow()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"event\":99,\"code\":\"0\"}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_ChannelFieldIsNumber_ReturnsErrorAndDoesNotThrow()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"action\":\"snapshot\",\"arg\":{\"instType\":\"SPOT\",\"channel\":123,\"instId\":\"BTCUSDT\"},\"data\":[]}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void BuildSubscribe_Ticker_ProducesCorrectFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("subscribe");
        var arg = doc.RootElement.GetProperty("args")[0];
        arg.GetProperty("instType").GetString().Should().Be("SPOT");
        arg.GetProperty("channel").GetString().Should().Be("ticker");
        arg.GetProperty("instId").GetString().Should().Be("BTCUSDT");
    }

    [Fact]
    public void BuildSubscribe_Trade_ProducesCorrectChannel()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETHUSDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("trade");
    }

    [Fact]
    public void BuildSubscribe_OrderBook_DefaultChannelIsBooks5()
    {
        // Confirmed default order-book channel: books5 (5 levels).
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTCUSDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("books5");
    }

    [Fact]
    public void BuildSubscribe_OrderBook_ExplicitDepth15_UsesBooks15()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTCUSDT", Depth: 15);

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("books15");
    }

    [Fact]
    public void BuildSubscribe_OrderBook_ExplicitDepthOther_UsesBooks()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTCUSDT", Depth: 200);

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("books");
    }

    [Fact]
    public void BuildSubscribe_Kline_DefaultIsCandle1m()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1m");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneMinute()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneMinute));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1m");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneHour()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneHour));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1H");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneDay()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneDay));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1D");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneWeek()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneWeek));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1W");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneMonth()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneMonth));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("candle1M");
    }

    [Fact]
    public void BuildUnsubscribe_Ticker_ProducesUnsubscribeOp()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");

        var wire = protocol.BuildUnsubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("unsubscribe");
        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("ticker");
    }

    [Fact]
    public void BuildUnsubscribe_Trade_ProducesCorrectChannel()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETHUSDT");

        var wire = protocol.BuildUnsubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("unsubscribe");
        doc.RootElement.GetProperty("args")[0].GetProperty("channel").GetString().Should().Be("trade");
    }

    [Fact]
    public void BuildSubscribeBatch_MultipleRequests_EmitsOneFrameWithAllArgs()
    {
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTCUSDT"),
            new StreamRequest(StreamKind.Trade, "ETHUSDT"),
            new StreamRequest(StreamKind.OrderBook, "BNBUSDT"),
        };

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("op").GetString().Should().Be("subscribe");
        var args = doc.RootElement.GetProperty("args");
        args.GetArrayLength().Should().Be(3);
        args[0].GetProperty("channel").GetString().Should().Be("ticker");
        args[0].GetProperty("instId").GetString().Should().Be("BTCUSDT");
        args[1].GetProperty("channel").GetString().Should().Be("trade");
        args[1].GetProperty("instId").GetString().Should().Be("ETHUSDT");
        args[2].GetProperty("channel").GetString().Should().Be("books5");
        args[2].GetProperty("instId").GetString().Should().Be("BNBUSDT");
    }

    [Fact]
    public void BuildUnsubscribeBatch_MultipleRequests_EmitsOneUnsubscribeFrame()
    {
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTCUSDT"),
            new StreamRequest(StreamKind.Ticker, "ETHUSDT"),
        };

        var wire = protocol.BuildUnsubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("op").GetString().Should().Be("unsubscribe");
        var args = doc.RootElement.GetProperty("args");
        args.GetArrayLength().Should().Be(2);
        args[0].GetProperty("instId").GetString().Should().Be("BTCUSDT");
        args[1].GetProperty("instId").GetString().Should().Be("ETHUSDT");
    }

    [Fact]
    public void BuildSubscribeBatch_SingleRequest_EmitsSingleArgArray()
    {
        var protocol = MakeProtocol();
        var requests = new[] { new StreamRequest(StreamKind.Trade, "BTCUSDT") };

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        var args = doc.RootElement.GetProperty("args");
        args.GetArrayLength().Should().Be(1);
        args[0].GetProperty("channel").GetString().Should().Be("trade");
    }

    [Fact]
    public void BuildSubscribeBatch_OneHundredRequests_EmitsExactlyOneHundredArgs()
    {
        var protocol = MakeProtocol();
        var requests = Enumerable.Range(0, 100)
            .Select(i => new StreamRequest(StreamKind.Ticker, $"SYM{i}USDT"))
            .ToArray();

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("args").GetArrayLength().Should().Be(100,
            "the engine pre-chunks at 100 — one Bitget batch frame may carry exactly 100 args");
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
    public void BuildSubscribeBatch_AllArgsHaveInstTypeSPOT()
    {
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTCUSDT"),
            new StreamRequest(StreamKind.Trade, "ETHUSDT"),
        };

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        var args = doc.RootElement.GetProperty("args");
        for (var i = 0; i < args.GetArrayLength(); i++)
            args[i].GetProperty("instType").GetString().Should().Be("SPOT");
    }

    [Fact]
    public void RoutingKeyFor_Ticker_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
        var frame = Utf8("{\"action\":\"snapshot\",\"arg\":{\"instType\":\"SPOT\",\"channel\":\"ticker\",\"instId\":\"BTCUSDT\"},\"data\":[{}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("ticker:BTCUSDT");
        classifiedKey.Should().Be("ticker:BTCUSDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Trade_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETHUSDT");
        var frame = Utf8("{\"action\":\"update\",\"arg\":{\"instType\":\"SPOT\",\"channel\":\"trade\",\"instId\":\"ETHUSDT\"},\"data\":[{}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("trade:ETHUSDT");
        classifiedKey.Should().Be("trade:ETHUSDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_OrderBook_DefaultDepth_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTCUSDT");
        var frame = Utf8("{\"action\":\"snapshot\",\"arg\":{\"instType\":\"SPOT\",\"channel\":\"books5\",\"instId\":\"BTCUSDT\"},\"data\":[{}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("books5:BTCUSDT");
        classifiedKey.Should().Be("books5:BTCUSDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Kline_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneMinute));
        var frame = Utf8("{\"action\":\"snapshot\",\"arg\":{\"instType\":\"SPOT\",\"channel\":\"candle1m\",\"instId\":\"BTCUSDT\"},\"data\":[[]]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("candle1m:BTCUSDT");
        classifiedKey.Should().Be("candle1m:BTCUSDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsConfiguredEndpoint()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Should().Be(new Uri("wss://ws.bitget.com/v2/ws/public"));
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsClientPingHeartbeat()
    {
        // Confirmed: Bitget v2 expects client to send text "ping" every 30 s.
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Direction.Should().Be(HeartbeatDirection.ClientPing);
        info.Heartbeat.PingFormat.Should().Be(PingFormat.Text);
    }

    [Fact]
    public async Task ResolveConnectionAsync_HeartbeatInterval_Is30Seconds()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Interval.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ResolveConnectionAsync_PingPayload_IsTextPing()
    {
        // Client sends literal text "ping"; server replies with text "pong".
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        var payload = Encoding.UTF8.GetString(info.Heartbeat.ClientPingPayload.Span);
        payload.Should().Be("ping");
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
        var options = new StreamOptions { StreamBaseUrl = "wss://ws.bitget.com/v2/ws/public?custom=1" };
        var protocol = new BitgetStreamProtocol(options);
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Should().Be(new Uri("wss://ws.bitget.com/v2/ws/public?custom=1"));
    }
}
