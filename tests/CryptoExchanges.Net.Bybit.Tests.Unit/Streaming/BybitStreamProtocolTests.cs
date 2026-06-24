using System.Text;
using System.Text.Json;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Bybit.Streaming;
using CryptoExchanges.Net.Http.Streaming;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Bybit.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests for <see cref="BybitStreamProtocol"/>: Classify, BuildSubscribe,
/// BuildUnsubscribe, BuildSubscribeBatch, BuildUnsubscribeBatch, RoutingKeyFor, and
/// ResolveConnectionAsync. Canned byte frames only — no sockets.
/// </summary>
/// <remarks>Excluded from CI integration-test runs via the Category trait where applicable.</remarks>
[Trait("Category", "Unit")]
public class BybitStreamProtocolTests
{
    private static readonly BybitStreamOptions DefaultOptions = new();

    private static BybitStreamProtocol MakeProtocol() => new(DefaultOptions);

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public void Classify_TickerDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"topic\":\"tickers.BTCUSDT\",\"type\":\"snapshot\",\"ts\":1700000000000,\"data\":{\"symbol\":\"BTCUSDT\"}}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("tickers.BTCUSDT");
    }

    [Fact]
    public void Classify_TradeDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"topic\":\"publicTrade.BTCUSDT\",\"type\":\"snapshot\",\"ts\":1700000000000,\"data\":[]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("publicTrade.BTCUSDT");
    }

    [Fact]
    public void Classify_OrderBookSnapshotDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"topic\":\"orderbook.50.BTCUSDT\",\"type\":\"snapshot\",\"ts\":1700000000000,\"data\":{\"s\":\"BTCUSDT\"}}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("orderbook.50.BTCUSDT");
    }

    [Fact]
    public void Classify_OrderBookDeltaDataFrame_ReturnsDataWithSameRoutingKey()
    {
        // Confirmed: snapshot and delta both classify as Data on the same topic routing key.
        var protocol = MakeProtocol();
        var frame = Utf8("{\"topic\":\"orderbook.50.BTCUSDT\",\"type\":\"delta\",\"ts\":1700000001000,\"data\":{\"s\":\"BTCUSDT\"}}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("orderbook.50.BTCUSDT",
            "snapshot and delta frames share the same topic routing key — both classify as Data");
    }

    [Fact]
    public void Classify_KlineDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"topic\":\"kline.1.BTCUSDT\",\"type\":\"snapshot\",\"ts\":1700000000000,\"data\":[]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("kline.1.BTCUSDT");
    }

    [Fact]
    public void Classify_SubscribeAckFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"success\":true,\"ret_msg\":\"subscribe\",\"op\":\"subscribe\",\"req_id\":\"1\",\"conn_id\":\"abc\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Ack);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_UnsubscribeAckFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"success\":true,\"ret_msg\":\"unsubscribe\",\"op\":\"unsubscribe\",\"req_id\":\"2\",\"conn_id\":\"abc\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Ack);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_PongFrame_ReturnsPong()
    {
        // Bybit v5 replies with {"op":"pong",...} when the server responds to a client ping.
        var protocol = MakeProtocol();
        var frame = Utf8("{\"success\":true,\"ret_msg\":\"pong\",\"op\":\"pong\",\"req_id\":\"hb1\",\"conn_id\":\"abc\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Pong);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_SubscribeErrorFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"success\":false,\"ret_msg\":\"error: invalid topic\",\"op\":\"subscribe\",\"req_id\":\"1\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
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
    public void Classify_TopicFieldIsNumber_ReturnsErrorAndDoesNotThrow()
    {
        // "topic" with a numeric value: GetString() would throw InvalidOperationException
        // without the ValueKind guard. Classify must absorb this as FrameKind.Error.
        var protocol = MakeProtocol();
        var frame = Utf8("{\"topic\":123,\"type\":\"snapshot\",\"ts\":1700000000000,\"data\":{}}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_SuccessFieldIsString_ReturnsErrorAndDoesNotThrow()
    {
        // "success" with a string value: GetBoolean() would throw InvalidOperationException
        // without the ValueKind guard. Classify must absorb this as FrameKind.Error.
        var protocol = MakeProtocol();
        var frame = Utf8("{\"success\":\"yes\",\"ret_msg\":\"subscribe\",\"op\":\"subscribe\",\"req_id\":\"1\"}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_OpFieldIsNumber_ReturnsErrorAndDoesNotThrow()
    {
        // "op" with a numeric value: GetString() would throw InvalidOperationException
        // without the ValueKind guard. Classify must absorb this as FrameKind.Error.
        var protocol = MakeProtocol();
        var frame = Utf8("{\"op\":99,\"success\":true,\"req_id\":\"1\"}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void BuildSubscribe_Ticker_ProducesCorrectTopic()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("subscribe");
        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("tickers.BTCUSDT");
        doc.RootElement.GetProperty("req_id").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BuildSubscribe_Trade_ProducesCorrectTopic()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETHUSDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("subscribe");
        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("publicTrade.ETHUSDT");
    }

    [Fact]
    public void BuildSubscribe_OrderBook_DefaultDepth50()
    {
        // Confirmed default depth = 50 (Bybit v5 spot available levels: 1, 50, 200).
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTCUSDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("orderbook.50.BTCUSDT");
    }

    [Fact]
    public void BuildSubscribe_OrderBook_ExplicitDepth1()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTCUSDT", Depth: 1);

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("orderbook.1.BTCUSDT");
    }

    [Fact]
    public void BuildSubscribe_OrderBook_ExplicitDepth200()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTCUSDT", Depth: 200);

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("orderbook.200.BTCUSDT");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneMinute()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneMinute));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("kline.1.BTCUSDT");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneHour()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneHour));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("kline.60.BTCUSDT");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneDay()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneDay));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("kline.D.BTCUSDT");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneWeek()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneWeek));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("kline.W.BTCUSDT");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneMonth()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneMonth));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("kline.M.BTCUSDT");
    }

    [Fact]
    public void BuildUnsubscribe_Ticker_ProducesUnsubscribeOp()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");

        var wire = protocol.BuildUnsubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("unsubscribe");
        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("tickers.BTCUSDT");
    }

    [Fact]
    public void BuildUnsubscribe_Trade_ProducesCorrectTopic()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETHUSDT");

        var wire = protocol.BuildUnsubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("op").GetString().Should().Be("unsubscribe");
        doc.RootElement.GetProperty("args")[0].GetString().Should().Be("publicTrade.ETHUSDT");
    }

    [Fact]
    public void BuildSubscribeBatch_MultipleRequests_EmitsOneFrameWithAllTopics()
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
        args[0].GetString().Should().Be("tickers.BTCUSDT");
        args[1].GetString().Should().Be("publicTrade.ETHUSDT");
        args[2].GetString().Should().Be("orderbook.50.BNBUSDT");
        doc.RootElement.GetProperty("req_id").GetString().Should().NotBeNullOrEmpty();
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
        args[0].GetString().Should().Be("tickers.BTCUSDT");
        args[1].GetString().Should().Be("tickers.ETHUSDT");
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
        args[0].GetString().Should().Be("publicTrade.BTCUSDT");
    }

    [Fact]
    public void BuildSubscribeBatch_OneHundredRequests_EmitsExactlyOneHundredArgs()
    {
        // Engine pre-chunks to 100; Bybit allows many args per frame — batch cap = 100.
        var protocol = MakeProtocol();
        var requests = Enumerable.Range(0, 100)
            .Select(i => new StreamRequest(StreamKind.Ticker, $"SYM{i}USDT"))
            .ToArray();

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("args").GetArrayLength().Should().Be(100,
            "the engine pre-chunks at 100 — one Bybit batch frame may carry exactly 100 topics");
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
    public void RoutingKeyFor_Ticker_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
        var frame = Utf8("{\"topic\":\"tickers.BTCUSDT\",\"type\":\"snapshot\",\"ts\":1700000000000,\"data\":{}}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("tickers.BTCUSDT");
        classifiedKey.Should().Be("tickers.BTCUSDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Trade_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETHUSDT");
        var frame = Utf8("{\"topic\":\"publicTrade.ETHUSDT\",\"type\":\"snapshot\",\"ts\":1700000000000,\"data\":[]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("publicTrade.ETHUSDT");
        classifiedKey.Should().Be("publicTrade.ETHUSDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_OrderBook_DefaultDepth_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTCUSDT");
        var frame = Utf8("{\"topic\":\"orderbook.50.BTCUSDT\",\"type\":\"snapshot\",\"ts\":1700000000000,\"data\":{}}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("orderbook.50.BTCUSDT");
        classifiedKey.Should().Be("orderbook.50.BTCUSDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Kline_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTCUSDT", Interval: nameof(KlineInterval.OneMinute));
        var frame = Utf8("{\"topic\":\"kline.1.BTCUSDT\",\"type\":\"snapshot\",\"ts\":1700000000000,\"data\":[]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("kline.1.BTCUSDT");
        classifiedKey.Should().Be("kline.1.BTCUSDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsConfiguredEndpoint()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Should().Be(new Uri("wss://stream.bybit.com/v5/public/spot"));
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsServerPingClientPong()
    {
        // Confirmed: Bybit v5 sends server-side control Ping every 20 s; engine auto-pongs.
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Direction.Should().Be(HeartbeatDirection.ServerPingClientPong);
    }

    [Fact]
    public async Task ResolveConnectionAsync_HeartbeatInterval_Is20Seconds()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Interval.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task ResolveConnectionAsync_HeartbeatTimeout_Is60Seconds()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Timeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task ResolveConnectionAsync_MinOutboundInterval_Is100Ms()
    {
        // Confirmed: 100 ms pacing (10 msg/s), conservative per-venue floor.
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.MinOutboundInterval.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsCachedInstance()
    {
        // Bybit uses a static URL + static policy — same reference on every call.
        var protocol = MakeProtocol();
        var info1 = await protocol.ResolveConnectionAsync(CancellationToken.None);
        var info2 = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info1.Should().BeSameAs(info2);
    }

    [Fact]
    public async Task ResolveConnectionAsync_CustomBaseUrl_UsesConfiguredEndpoint()
    {
        var options = new BybitStreamOptions { StreamBaseUrl = "wss://stream.bybit.com/v5/public/linear" };
        var protocol = new BybitStreamProtocol(options);
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Should().Be(new Uri("wss://stream.bybit.com/v5/public/linear"));
    }
}
