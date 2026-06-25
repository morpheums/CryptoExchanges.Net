using System.Text;
using System.Text.Json;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Coinbase.Streaming;
using CryptoExchanges.Net.Http.Streaming;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Coinbase.Tests.Unit.Streaming;

[Trait("Category", "Unit")]
public class CoinbaseStreamProtocolTests
{
    private static readonly StreamOptions DefaultOptions = new();

    private static CoinbaseStreamProtocol MakeProtocol() => new(DefaultOptions);

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public void Classify_TickerDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        // product_id is inside tickers[0], not at the event level (real Coinbase format).
        var frame = Utf8("{\"channel\":\"ticker\",\"events\":[{\"type\":\"snapshot\",\"tickers\":[{\"product_id\":\"BTC-USD\",\"price\":\"50000\"}]}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("ticker:BTC-USD");
    }

    [Fact]
    public void Classify_TradeDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        // product_id is inside trades[0], not at the event level (real Coinbase format).
        var frame = Utf8("{\"channel\":\"market_trades\",\"events\":[{\"type\":\"snapshot\",\"trades\":[{\"product_id\":\"ETH-USD\",\"price\":\"2000\",\"size\":\"0.001\",\"side\":\"BUY\",\"time\":\"2024-01-01T00:00:00Z\",\"trade_id\":\"1\"}]}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("market_trades:ETH-USD");
    }

    [Fact]
    public void Classify_OrderBookDataFrame_ReturnsDataWithRoutingKey()
    {
        // level2 subscribe → l2_data push channel
        var protocol = MakeProtocol();
        var frame = Utf8("{\"channel\":\"l2_data\",\"events\":[{\"product_id\":\"BTC-USD\",\"updates\":[]}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("l2_data:BTC-USD");
    }

    [Fact]
    public void Classify_KlineDataFrame_ReturnsDataWithRoutingKey()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"channel\":\"candles\",\"events\":[{\"candles\":[{\"product_id\":\"BTC-USD\",\"start\":\"1718784000\"}]}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("candles:BTC-USD");
    }

    [Fact]
    public void Classify_HeartbeatFrame_ReturnsPong()
    {
        // Coinbase sends heartbeat channel frames when subscribed to the heartbeats channel.
        var protocol = MakeProtocol();
        var frame = Utf8("{\"channel\":\"heartbeats\",\"events\":[{\"heartbeat_counter\":1}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Pong);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_SubscriptionsAckFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"type\":\"subscriptions\",\"channels\":[{\"name\":\"ticker\",\"product_ids\":[\"BTC-USD\"]}]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Ack);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_ErrorFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"type\":\"error\",\"message\":\"Authentication required\"}");

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
    public void Classify_EventsArrayEmpty_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"channel\":\"ticker\",\"events\":[]}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void Classify_TypeFieldIsNumber_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"type\":42}");

        var ex = Record.Exception(() => protocol.Classify(frame));
        var result = protocol.Classify(frame);

        ex.Should().BeNull("Classify must not throw on a malformed frame");
        result.Kind.Should().Be(FrameKind.Error);
    }

    [Fact]
    public void BuildSubscribe_Ticker_ProducesCorrectFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC-USD");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("type").GetString().Should().Be("subscribe");
        doc.RootElement.GetProperty("channel").GetString().Should().Be("ticker");
        doc.RootElement.GetProperty("product_ids")[0].GetString().Should().Be("BTC-USD");
    }

    [Fact]
    public void BuildSubscribe_Trade_ProducesCorrectFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETH-USD");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("type").GetString().Should().Be("subscribe");
        doc.RootElement.GetProperty("channel").GetString().Should().Be("market_trades");
        doc.RootElement.GetProperty("product_ids")[0].GetString().Should().Be("ETH-USD");
    }

    [Fact]
    public void BuildSubscribe_OrderBook_UsesLevel2Channel()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTC-USD");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("channel").GetString().Should().Be("level2");
    }

    [Fact]
    public void BuildSubscribe_Kline_UsesCandlesChannel()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USD");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("channel").GetString().Should().Be("candles");
    }

    [Fact]
    public void BuildUnsubscribe_Ticker_ProducesUnsubscribeType()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC-USD");

        var wire = protocol.BuildUnsubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("type").GetString().Should().Be("unsubscribe");
        doc.RootElement.GetProperty("channel").GetString().Should().Be("ticker");
        doc.RootElement.GetProperty("product_ids")[0].GetString().Should().Be("BTC-USD");
    }

    [Fact]
    public void BuildSubscribeBatch_SameChannel_EmitsSingleFrameWithAllProductIds()
    {
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTC-USD"),
            new StreamRequest(StreamKind.Ticker, "ETH-USD"),
            new StreamRequest(StreamKind.Ticker, "SOL-USD"),
        };

        var wire = protocol.BuildSubscribeBatch(requests);
        using var doc = JsonDocument.Parse(wire!);

        doc.RootElement.GetProperty("type").GetString().Should().Be("subscribe");
        doc.RootElement.GetProperty("channel").GetString().Should().Be("ticker");
        var ids = doc.RootElement.GetProperty("product_ids");
        ids.GetArrayLength().Should().Be(3);
        ids[0].GetString().Should().Be("BTC-USD");
        ids[1].GetString().Should().Be("ETH-USD");
        ids[2].GetString().Should().Be("SOL-USD");
    }

    [Fact]
    public void BuildSubscribeBatch_MixedChannels_ReturnsNull()
    {
        // Coinbase requires one subscribe frame per channel; mixed channels must fall back to per-frame.
        var protocol = MakeProtocol();
        var requests = new[]
        {
            new StreamRequest(StreamKind.Ticker, "BTC-USD"),
            new StreamRequest(StreamKind.Trade, "BTC-USD"),
        };

        var wire = protocol.BuildSubscribeBatch(requests);

        wire.Should().BeNull("mixed channels require per-frame fallback");
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
    public void RoutingKeyFor_Ticker_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC-USD");
        var frame = Utf8("{\"channel\":\"ticker\",\"events\":[{\"type\":\"snapshot\",\"tickers\":[{\"product_id\":\"BTC-USD\",\"price\":\"50000\"}]}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("ticker:BTC-USD");
        classifiedKey.Should().Be("ticker:BTC-USD");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_OrderBook_UsesL2DataPushChannel()
    {
        // Subscribe uses "level2" but push frames arrive as "l2_data"; routing key uses the push channel.
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTC-USD");
        var frame = Utf8("{\"channel\":\"l2_data\",\"events\":[{\"product_id\":\"BTC-USD\",\"updates\":[]}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("l2_data:BTC-USD");
        classifiedKey.Should().Be("l2_data:BTC-USD");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Kline_MatchesClassifyDataFrame()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USD");
        var frame = Utf8("{\"channel\":\"candles\",\"events\":[{\"candles\":[{\"product_id\":\"BTC-USD\",\"start\":\"1718784000\"}]}]}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("candles:BTC-USD");
        classifiedKey.Should().Be("candles:BTC-USD");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public async Task ResolveConnectionAsync_ReturnsConfiguredEndpoint()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Should().Be(new Uri("wss://advanced-trade-ws.coinbase.com"));
    }

    [Fact]
    public async Task ResolveConnectionAsync_HeartbeatDirection_IsServerPingClientPong()
    {
        // Coinbase Advanced Trade WS sends WebSocket Ping control frames; engine responds automatically.
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Direction.Should().Be(HeartbeatDirection.ServerPingClientPong);
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
        var options = new StreamOptions { StreamBaseUrl = "wss://advanced-trade-ws-user.coinbase.com" };
        var protocol = new CoinbaseStreamProtocol(options);
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Should().Be(new Uri("wss://advanced-trade-ws-user.coinbase.com"));
    }

    [Fact]
    public void HeartbeatsSubscribeFrame_IsValidJson()
    {
        var ex = Record.Exception(() => JsonDocument.Parse(CoinbaseStreamProtocol.HeartbeatsSubscribeFrame));
        ex.Should().BeNull("heartbeats subscribe frame must be valid JSON");

        using var doc = JsonDocument.Parse(CoinbaseStreamProtocol.HeartbeatsSubscribeFrame);
        doc.RootElement.GetProperty("type").GetString().Should().Be("subscribe");
        doc.RootElement.GetProperty("channel").GetString().Should().Be("heartbeats");
        doc.RootElement.GetProperty("product_ids").GetArrayLength().Should().Be(0);
    }
}
