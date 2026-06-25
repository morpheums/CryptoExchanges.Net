using System.Text;
using System.Text.Json;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Kraken.Streaming;
using CryptoExchanges.Net.Http.Streaming;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Kraken.Tests.Unit.Streaming;

[Trait("Category", "Unit")]
public class KrakenStreamProtocolTests
{
    private static readonly StreamOptions DefaultOptions = new();

    private static KrakenStreamProtocol MakeProtocol() => new(DefaultOptions);

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public void Classify_TickerDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"channel\":\"ticker\",\"type\":\"snapshot\",\"data\":[{\"symbol\":\"BTC/USD\"}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("ticker:BTC/USD");
    }

    [Fact]
    public void Classify_TradeDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"channel\":\"trade\",\"type\":\"snapshot\",\"data\":[{\"symbol\":\"ETH/USD\"}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("trade:ETH/USD");
    }

    [Fact]
    public void Classify_OrderBookDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"channel\":\"book\",\"type\":\"snapshot\",\"data\":[{\"symbol\":\"BTC/USD\",\"bids\":[],\"asks\":[]}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("book:BTC/USD");
    }

    [Fact]
    public void Classify_KlineDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"channel\":\"ohlc\",\"type\":\"snapshot\",\"data\":[{\"symbol\":\"BTC/USD\",\"open\":\"67000\"}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("ohlc:BTC/USD");
    }

    [Fact]
    public void Classify_PongFrame_ReturnsPong()
    {
        // Kraken WS v2 replies with JSON {"method":"pong"} to the client {"method":"ping"}.
        var protocol = MakeProtocol();
        var frame = Utf8("{\"method\":\"pong\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Pong);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_SubscribeAckFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"method\":\"subscribe\",\"result\":{\"channel\":\"ticker\",\"symbol\":[\"BTC/USD\"]},\"success\":true}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Ack);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_UnsubscribeAckFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"method\":\"unsubscribe\",\"result\":{\"channel\":\"ticker\",\"symbol\":[\"BTC/USD\"]},\"success\":true}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Ack);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_SubscribeErrorFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"method\":\"subscribe\",\"success\":false,\"error\":\"Unknown channel\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_UnrecognisedMethodFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"method\":\"mystery\"}");

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
    public void Classify_UnrecognisedFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"mystery\":true}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_DataFrameWithNoSymbolInFirstElement_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"channel\":\"ticker\",\"type\":\"snapshot\",\"data\":[{\"price\":\"100\"}]}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_DataFrameEmptyDataArray_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"channel\":\"ticker\",\"type\":\"snapshot\",\"data\":[]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void BuildSubscribe_Ticker_ProducesCorrectFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC/USD");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("method").GetString().Should().Be("subscribe");
        var p = doc.RootElement.GetProperty("params");
        p.GetProperty("channel").GetString().Should().Be("ticker");
        p.GetProperty("symbol")[0].GetString().Should().Be("BTC/USD");
    }

    [Fact]
    public void BuildSubscribe_Trade_ProducesCorrectFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETH/USD");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("params").GetProperty("channel").GetString().Should().Be("trade");
    }

    [Fact]
    public void BuildSubscribe_OrderBook_ProducesBookChannel()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTC/USD");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("params").GetProperty("channel").GetString().Should().Be("book");
    }

    [Fact]
    public void BuildSubscribe_Kline_OneMinute_ProducesOhlcWithInterval1()
    {
        // Confirmed: Kraken WS v2 ohlc params include integer-minutes "interval":1 for OneMinute.
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC/USD", Interval: nameof(KlineInterval.OneMinute));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        var p = doc.RootElement.GetProperty("params");
        p.GetProperty("channel").GetString().Should().Be("ohlc");
        p.GetProperty("interval").GetInt32().Should().Be(1);
    }

    [Fact]
    public void BuildSubscribe_Kline_OneHour_ProducesInterval60()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC/USD", Interval: nameof(KlineInterval.OneHour));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("params").GetProperty("interval").GetInt32().Should().Be(60);
    }

    [Fact]
    public void BuildSubscribe_Kline_OneDay_ProducesInterval1440()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC/USD", Interval: nameof(KlineInterval.OneDay));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("params").GetProperty("interval").GetInt32().Should().Be(1440);
    }

    [Fact]
    public void BuildUnsubscribe_Ticker_ProducesUnsubscribeMethod()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC/USD");

        var wire = protocol.BuildUnsubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("method").GetString().Should().Be("unsubscribe");
        doc.RootElement.GetProperty("params").GetProperty("channel").GetString().Should().Be("ticker");
        doc.RootElement.GetProperty("params").GetProperty("symbol")[0].GetString().Should().Be("BTC/USD");
    }

    [Fact]
    public void BuildSubscribeBatch_MultipleRequests_EmitsOneFrameWithAllSymbols()
    {
        // Confirmed: Kraken WS v2 batching uses the "symbol" array within params (single channel per frame).
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTC/USD"),
            new StreamRequest(StreamKind.Ticker, "ETH/USD"),
            new StreamRequest(StreamKind.Ticker, "SOL/USD"),
        };

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("method").GetString().Should().Be("subscribe");
        doc.RootElement.GetProperty("params").GetProperty("channel").GetString().Should().Be("ticker");
        var syms = doc.RootElement.GetProperty("params").GetProperty("symbol");
        syms.GetArrayLength().Should().Be(3);
        syms[0].GetString().Should().Be("BTC/USD");
        syms[1].GetString().Should().Be("ETH/USD");
        syms[2].GetString().Should().Be("SOL/USD");
    }

    [Fact]
    public void BuildSubscribeBatch_MixedChannels_ReturnsNull()
    {
        // Kraken batch frames must all share one channel; mixed-channel sets fall back per-frame.
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTC/USD"),
            new StreamRequest(StreamKind.Trade, "ETH/USD"),
        };

        var wire = protocol.BuildSubscribeBatch(requests);

        wire.Should().BeNull("mixed-channel batch is not supported; engine falls back per-frame");
    }

    [Fact]
    public void BuildUnsubscribeBatch_MultipleRequests_EmitsOneUnsubscribeFrame()
    {
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTC/USD"),
            new StreamRequest(StreamKind.Ticker, "ETH/USD"),
        };

        var wire = protocol.BuildUnsubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("method").GetString().Should().Be("unsubscribe");
        doc.RootElement.GetProperty("params").GetProperty("symbol").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void BuildSubscribeBatch_EmptyList_ReturnsNull()
    {
        var protocol = MakeProtocol();
        protocol.BuildSubscribeBatch([]).Should().BeNull();
    }

    [Fact]
    public void BuildUnsubscribeBatch_EmptyList_ReturnsNull()
    {
        var protocol = MakeProtocol();
        protocol.BuildUnsubscribeBatch([]).Should().BeNull();
    }

    [Fact]
    public void RoutingKeyFor_MatchesClassify_TickerDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC/USD");
        var frame = Utf8("{\"channel\":\"ticker\",\"type\":\"snapshot\",\"data\":[{\"symbol\":\"BTC/USD\"}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("ticker:BTC/USD");
        classifiedKey.Should().Be("ticker:BTC/USD");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Trade_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETH/USD");
        var frame = Utf8("{\"channel\":\"trade\",\"type\":\"snapshot\",\"data\":[{\"symbol\":\"ETH/USD\"}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_OrderBook_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTC/USD");
        var frame = Utf8("{\"channel\":\"book\",\"type\":\"snapshot\",\"data\":[{\"symbol\":\"BTC/USD\",\"bids\":[],\"asks\":[]}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("book:BTC/USD");
        classifiedKey.Should().Be("book:BTC/USD");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Kline_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC/USD", Interval: nameof(KlineInterval.OneMinute));
        var frame = Utf8("{\"channel\":\"ohlc\",\"type\":\"snapshot\",\"data\":[{\"symbol\":\"BTC/USD\",\"open\":\"67000\"}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("ohlc:BTC/USD");
        classifiedKey.Should().Be("ohlc:BTC/USD");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsConfiguredEndpoint()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Should().Be(new Uri("wss://ws.kraken.com/v2"));
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsClientPingDirection()
    {
        // Confirmed: Kraken WS v2 expects the client to send {"method":"ping"} every 30 s; server replies {"method":"pong"}.
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Direction.Should().Be(HeartbeatDirection.ClientPing);
    }

    [Fact]
    public async Task ResolveConnectionAsync_HeartbeatPingFormat_IsJson()
    {
        // Confirmed: Kraken WS v2 ping payload is JSON text (not a WebSocket control frame).
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.PingFormat.Should().Be(PingFormat.Json);
    }

    [Fact]
    public async Task ResolveConnectionAsync_HeartbeatInterval_Is30Seconds()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Interval.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ResolveConnectionAsync_HeartbeatTimeout_Is60Seconds()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Timeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task ResolveConnectionAsync_PingPayload_IsJsonPingMethod()
    {
        // Confirmed: Kraken WS v2 ping payload is {"method":"ping"}.
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.ClientPingPayload.ToArray()
            .Should().Equal(Encoding.UTF8.GetBytes("{\"method\":\"ping\"}"));
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
        var options = new StreamOptions { StreamBaseUrl = "wss://ws.kraken.com/v2?test=1" };
        var protocol = new KrakenStreamProtocol(options);
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Should().Be(new Uri("wss://ws.kraken.com/v2?test=1"));
    }
}
