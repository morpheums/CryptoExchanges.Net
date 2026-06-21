using System.Text;
using System.Text.Json;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Kucoin.Dtos.Streaming;
using CryptoExchanges.Net.Kucoin.Streaming;
using CryptoExchanges.Net.Http.Streaming;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Kucoin.Tests.Unit.Streaming;

/// <summary>
/// No-network unit tests for <see cref="KucoinStreamProtocol"/>: Classify, BuildSubscribe,
/// BuildUnsubscribe, RoutingKeyFor, and ResolveConnectionAsync (via a fake bullet-public client).
/// Canned byte frames only — no sockets, no HTTP.
/// </summary>
[Trait("Category", "Unit")]
public class KucoinStreamProtocolTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static KucoinStreamProtocol MakeProtocol(IKucoinBulletPublicClient? bulletClient = null)
        => new(bulletClient ?? MakeFakeBulletClient());

    private static FakeBulletPublicClient MakeFakeBulletClient(
        string wsEndpoint = "wss://ws-api-spot.kucoin.com/",
        string token = "test-token",
        int pingInterval = 18000,
        int pingTimeout = 10000)
        => new FakeBulletPublicClient(wsEndpoint, token, pingInterval, pingTimeout);

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    // ── ResolveConnectionAsync — bullet-public negotiation ─────────────────────

    [Fact]
    public async Task ResolveConnectionAsync_FakeClient_ReturnsTokenInUri()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Query.Should().Contain("token=test-token");
    }

    [Fact]
    public async Task ResolveConnectionAsync_FakeClient_ReturnsConnectIdInUri()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Endpoint.Query.Should().Contain("connectId=");
    }

    [Fact]
    public async Task ResolveConnectionAsync_FakeClient_HeartbeatIsClientPing()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Direction.Should().Be(HeartbeatDirection.ClientPing);
    }

    [Fact]
    public async Task ResolveConnectionAsync_FakeClient_HeartbeatIntervalFromServer()
    {
        var protocol = MakeProtocol(MakeFakeBulletClient(pingInterval: 20000));
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Interval.Should().Be(TimeSpan.FromMilliseconds(20000));
    }

    [Fact]
    public async Task ResolveConnectionAsync_FakeClient_HeartbeatTimeoutFromServer()
    {
        var protocol = MakeProtocol(MakeFakeBulletClient(pingTimeout: 12000));
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.Timeout.Should().Be(TimeSpan.FromMilliseconds(12000));
    }

    [Fact]
    public async Task ResolveConnectionAsync_CalledTwice_ReturnsFreshToken_OnReNegotiate()
    {
        var fake = new CountingFakeBulletClient();
        var protocol = new KucoinStreamProtocol(fake);

        await protocol.ResolveConnectionAsync(CancellationToken.None);
        await protocol.ResolveConnectionAsync(CancellationToken.None);

        // Bullet-public must be called on every resolve (re-negotiation on reconnect — AC-4).
        fake.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task ResolveConnectionAsync_EachCall_HasUniqueConnectId()
    {
        var protocol = MakeProtocol();

        var info1 = await protocol.ResolveConnectionAsync(CancellationToken.None);
        var info2 = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info1.Endpoint.Query.Should().NotBe(info2.Endpoint.Query);
    }

    [Fact]
    public async Task ResolveConnectionAsync_PingFormat_IsJson()
    {
        var protocol = MakeProtocol();
        var info = await protocol.ResolveConnectionAsync(CancellationToken.None);

        info.Heartbeat.PingFormat.Should().Be(PingFormat.Json);
    }

    // ── SSRF validation ────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveConnectionAsync_NonKucoinHost_Throws()
    {
        var protocol = MakeProtocol(MakeFakeBulletClient(wsEndpoint: "wss://evil.example.com/"));

        Func<Task> act = () => protocol.ResolveConnectionAsync(CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResolveConnectionAsync_HttpScheme_Throws()
    {
        var protocol = MakeProtocol(MakeFakeBulletClient(wsEndpoint: "ws://ws-api-spot.kucoin.com/"));

        Func<Task> act = () => protocol.ResolveConnectionAsync(CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── BuildSubscribe ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildSubscribe_Ticker_ProducesCorrectTopic()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC-USDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("type").GetString().Should().Be("subscribe");
        doc.RootElement.GetProperty("topic").GetString().Should().Be("/market/ticker:BTC-USDT");
        doc.RootElement.GetProperty("privateChannel").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("response").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void BuildSubscribe_Trade_ProducesMatchTopic()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "ETH-USDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("topic").GetString().Should().Be("/market/match:ETH-USDT");
    }

    [Fact]
    public void BuildSubscribe_OrderBook_ProducesLevel2Topic()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTC-USDT");

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("topic").GetString().Should().Be("/market/level2:BTC-USDT");
    }

    [Fact]
    public void BuildSubscribe_Kline_ProducesCandlesTopic()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USDT", Interval: nameof(KlineInterval.OneMinute));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("topic").GetString().Should().Be("/market/candles:BTC-USDT_1min");
    }

    [Fact]
    public void BuildSubscribe_KlineOneHour_ProducesCandlesTopic()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USDT", Interval: nameof(KlineInterval.OneHour));

        var wire = protocol.BuildSubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("topic").GetString().Should().Be("/market/candles:BTC-USDT_1hour");
    }

    // ── BuildUnsubscribe ───────────────────────────────────────────────────────

    [Fact]
    public void BuildUnsubscribe_Ticker_ProducesUnsubscribeType()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC-USDT");

        var wire = protocol.BuildUnsubscribe(request);
        using var doc = JsonDocument.Parse(wire);

        doc.RootElement.GetProperty("type").GetString().Should().Be("unsubscribe");
        doc.RootElement.GetProperty("topic").GetString().Should().Be("/market/ticker:BTC-USDT");
    }

    // ── RoutingKeyFor / Classify round-trip ────────────────────────────────────

    [Fact]
    public void RoutingKeyFor_Ticker_MatchesClassifyRoutingKey()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Ticker, "BTC-USDT");
        var frame = Utf8("{\"type\":\"message\",\"topic\":\"/market/ticker:BTC-USDT\",\"subject\":\"trade.ticker\",\"data\":{}}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("/market/ticker:BTC-USDT");
        classifiedKey.Should().Be("/market/ticker:BTC-USDT");
        subscribeKey.Should().Be(classifiedKey,
            "RoutingKeyFor and Classify must share one keyspace so frames reach their subscription");
    }

    [Fact]
    public void RoutingKeyFor_Trade_MatchesClassifyRoutingKey()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Trade, "BTC-USDT");
        var frame = Utf8("{\"type\":\"message\",\"topic\":\"/market/match:BTC-USDT\",\"data\":{}}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("/market/match:BTC-USDT");
        classifiedKey.Should().Be("/market/match:BTC-USDT");
        subscribeKey.Should().Be(classifiedKey);
    }

    [Fact]
    public void RoutingKeyFor_OrderBook_MatchesClassifyRoutingKey()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.OrderBook, "BTC-USDT");
        var frame = Utf8("{\"type\":\"message\",\"topic\":\"/market/level2:BTC-USDT\",\"data\":{}}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("/market/level2:BTC-USDT");
        classifiedKey.Should().Be("/market/level2:BTC-USDT");
        subscribeKey.Should().Be(classifiedKey);
    }

    [Fact]
    public void RoutingKeyFor_Kline_MatchesClassifyRoutingKey()
    {
        var protocol = MakeProtocol();
        var request = new StreamRequest(StreamKind.Kline, "BTC-USDT", Interval: nameof(KlineInterval.OneMinute));
        var frame = Utf8("{\"type\":\"message\",\"topic\":\"/market/candles:BTC-USDT_1min\",\"data\":{}}");

        var subscribeKey = protocol.RoutingKeyFor(request);
        var classifiedKey = protocol.Classify(frame).RoutingKey;

        subscribeKey.Should().Be("/market/candles:BTC-USDT_1min");
        classifiedKey.Should().Be("/market/candles:BTC-USDT_1min");
        subscribeKey.Should().Be(classifiedKey);
    }

    // ── Classify ──────────────────────────────────────────────────────────────

    [Fact]
    public void Classify_MessageFrame_ReturnsDataWithTopic()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"type\":\"message\",\"topic\":\"/market/ticker:BTC-USDT\",\"data\":{}}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Data);
        result.RoutingKey.Should().Be("/market/ticker:BTC-USDT");
    }

    [Fact]
    public void Classify_AckFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"id\":\"123\",\"type\":\"ack\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Ack);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_PongFrame_ReturnsPong()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"id\":\"123\",\"type\":\"pong\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Pong);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_ErrorFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"id\":\"123\",\"type\":\"error\",\"code\":400,\"data\":\"Bad request\"}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
        result.RoutingKey.Should().BeNull();
    }

    [Fact]
    public void Classify_WelcomeFrame_ReturnsAck()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"id\":\"12345\",\"type\":\"welcome\"}");

        var result = protocol.Classify(frame);

        // Welcome frames are treated as Ack (discarded by the engine).
        result.Kind.Should().Be(FrameKind.Ack);
    }

    [Fact]
    public void Classify_UnknownTypeFrame_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"type\":\"mystery\",\"data\":{}}");

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
    public void Classify_MissingTypeField_ReturnsError()
    {
        var protocol = MakeProtocol();
        var frame = Utf8("{\"id\":\"123\",\"data\":{}}");

        var result = protocol.Classify(frame);

        result.Kind.Should().Be(FrameKind.Error);
    }

    // ── Fake helpers ──────────────────────────────────────────────────────────

    private sealed class FakeBulletPublicClient : IKucoinBulletPublicClient
    {
        private readonly string _wsEndpoint;
        private readonly string _token;
        private readonly int _pingInterval;
        private readonly int _pingTimeout;

        public FakeBulletPublicClient(string wsEndpoint, string token, int pingInterval, int pingTimeout)
        {
            _wsEndpoint = wsEndpoint;
            _token = token;
            _pingInterval = pingInterval;
            _pingTimeout = pingTimeout;
        }

        public Task<BulletPublicDto> NegotiateAsync(CancellationToken ct) =>
            Task.FromResult(new BulletPublicDto
            {
                Token = _token,
                InstanceServers =
                [
                    new InstanceServerDto
                    {
                        Endpoint = _wsEndpoint,
                        PingInterval = _pingInterval,
                        PingTimeout = _pingTimeout
                    }
                ]
            });
    }

    private sealed class CountingFakeBulletClient : IKucoinBulletPublicClient
    {
        public int CallCount { get; private set; }

        public Task<BulletPublicDto> NegotiateAsync(CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new BulletPublicDto
            {
                Token = $"token-{CallCount}",
                InstanceServers =
                [
                    new InstanceServerDto
                    {
                        Endpoint = "wss://ws-api-spot.kucoin.com/",
                        PingInterval = 18000,
                        PingTimeout = 10000
                    }
                ]
            });
        }
    }
}
