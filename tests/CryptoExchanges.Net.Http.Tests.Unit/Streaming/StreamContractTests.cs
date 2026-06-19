using System.Net.WebSockets;
using System.Text;
using FluentAssertions;
using Xunit;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Http.Tests.Unit.Streaming;

/// <summary>
/// Light contract tests asserting value semantics on the Http streaming contract types
/// and exercising the <see cref="FakeWebSocketConnection"/> test seam.
/// </summary>
public sealed class StreamContractTests
{
    // ── StreamFrame ───────────────────────────────────────────────────────────

    [Fact]
    public void StreamFrame_Equality_SameKindAndKey_AreEqual()
    {
        var a = new StreamFrame(FrameKind.Data, "btcusdt@trade");
        var b = new StreamFrame(FrameKind.Data, "btcusdt@trade");
        a.Should().Be(b);
    }

    [Fact]
    public void StreamFrame_Equality_DifferentKey_AreNotEqual()
    {
        var a = new StreamFrame(FrameKind.Data, "btcusdt@trade");
        var b = new StreamFrame(FrameKind.Data, "ethusdt@trade");
        a.Should().NotBe(b);
    }

    [Fact]
    public void StreamFrame_NullRoutingKey_Equality_IsValueBased()
    {
        var a = new StreamFrame(FrameKind.Pong, null);
        var b = new StreamFrame(FrameKind.Pong, null);
        a.Should().Be(b);
    }

    [Fact]
    public void StreamFrame_Kind_ReflectsConstructorArg()
    {
        var frame = new StreamFrame(FrameKind.Error, null);
        frame.Kind.Should().Be(FrameKind.Error);
        frame.RoutingKey.Should().BeNull();
    }

    // ── HeartbeatPolicy ───────────────────────────────────────────────────────

    [Fact]
    public void HeartbeatPolicy_Defaults_PingFormat_IsControlFrame()
    {
        var policy = new HeartbeatPolicy(
            Direction: HeartbeatDirection.ServerPingClientPong,
            Interval: TimeSpan.FromSeconds(20),
            Timeout: TimeSpan.FromSeconds(60));

        policy.PingFormat.Should().Be(PingFormat.ControlFrame);
    }

    [Fact]
    public void HeartbeatPolicy_Defaults_ClientPingPayload_IsEmpty()
    {
        var policy = new HeartbeatPolicy(
            Direction: HeartbeatDirection.ClientPing,
            Interval: TimeSpan.FromSeconds(30),
            Timeout: TimeSpan.FromSeconds(90));

        policy.ClientPingPayload.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void HeartbeatPolicy_Equality_IsValueBased()
    {
        var a = new HeartbeatPolicy(
            HeartbeatDirection.ClientPing,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(90),
            PingFormat: PingFormat.Json);

        var b = new HeartbeatPolicy(
            HeartbeatDirection.ClientPing,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(90),
            PingFormat: PingFormat.Json);

        a.Should().Be(b);
    }

    [Fact]
    public void HeartbeatPolicy_Stores_AllProperties()
    {
        var payload = Encoding.UTF8.GetBytes("{\"op\":\"ping\"}");
        var policy = new HeartbeatPolicy(
            Direction: HeartbeatDirection.ClientPing,
            Interval: TimeSpan.FromSeconds(15),
            Timeout: TimeSpan.FromSeconds(45),
            ClientPingPayload: payload,
            PingFormat: PingFormat.Json);

        policy.Direction.Should().Be(HeartbeatDirection.ClientPing);
        policy.Interval.Should().Be(TimeSpan.FromSeconds(15));
        policy.Timeout.Should().Be(TimeSpan.FromSeconds(45));
        policy.PingFormat.Should().Be(PingFormat.Json);
        policy.ClientPingPayload.Span.SequenceEqual(payload).Should().BeTrue();
    }

    // ── StreamRequest ─────────────────────────────────────────────────────────

    [Fact]
    public void StreamRequest_Defaults_OptionalParams_AreNull()
    {
        var req = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
        req.Depth.Should().BeNull();
        req.Interval.Should().BeNull();
    }

    [Fact]
    public void StreamRequest_OrderBook_CarriesDepth()
    {
        var req = new StreamRequest(StreamKind.OrderBook, "BTCUSDT", Depth: 20);
        req.Kind.Should().Be(StreamKind.OrderBook);
        req.WireSymbol.Should().Be("BTCUSDT");
        req.Depth.Should().Be(20);
    }

    [Fact]
    public void StreamRequest_Kline_CarriesInterval()
    {
        var req = new StreamRequest(StreamKind.Kline, "ETHUSDT", Interval: "1m");
        req.Kind.Should().Be(StreamKind.Kline);
        req.Interval.Should().Be("1m");
    }

    // ── StreamDecoderRegistry ─────────────────────────────────────────────────

    [Fact]
    public void StreamDecoderRegistry_Register_And_Resolve_RoundTrips()
    {
        var registry = new StreamDecoderRegistry();
        var sentinel = new object();
        registry.Register(StreamKind.Ticker, _ => sentinel);

        var decoder = registry.Resolve(StreamKind.Ticker);
        decoder(ReadOnlyMemory<byte>.Empty).Should().BeSameAs(sentinel);
    }

    [Fact]
    public void StreamDecoderRegistry_Contains_ReturnsTrueAfterRegister()
    {
        var registry = new StreamDecoderRegistry();
        registry.Register(StreamKind.Trade, _ => new object());
        registry.Contains(StreamKind.Trade).Should().BeTrue();
        registry.Contains(StreamKind.Kline).Should().BeFalse();
    }

    [Fact]
    public void StreamDecoderRegistry_Resolve_Unregistered_Throws()
    {
        var registry = new StreamDecoderRegistry();
        var act = () => registry.Resolve(StreamKind.OrderBook);
        act.Should().Throw<InvalidOperationException>();
    }

    // ── FakeWebSocketConnection ───────────────────────────────────────────────

    [Fact]
    public async Task Fake_ConnectAsync_SetsStateOpen()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);
        fake.IsOpen.Should().BeTrue();
        fake.State.Should().Be(WebSocketState.Open);
        fake.ConnectCount.Should().Be(1);
    }

    [Fact]
    public async Task Fake_EnqueueAndReceive_RoundTripsTextFrame()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        fake.EnqueueFrame("{\"stream\":\"btcusdt@trade\"}");
        var result = await fake.ReceiveAsync(TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        Encoding.UTF8.GetString(result!.Value.Span).Should().Be("{\"stream\":\"btcusdt@trade\"}");
    }

    [Fact]
    public async Task Fake_EnqueueBinaryFrame_RoundTrips()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        var bytes = new byte[] { 0x01, 0x02, 0x03 };
        fake.EnqueueFrame((ReadOnlyMemory<byte>)bytes);
        var result = await fake.ReceiveAsync(TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.Span.SequenceEqual(bytes).Should().BeTrue();
    }

    [Fact]
    public async Task Fake_SendTextAsync_CapturesMessages()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        await fake.SendTextAsync("{\"method\":\"SUBSCRIBE\"}", TestContext.Current.CancellationToken);
        await fake.SendTextAsync("{\"method\":\"UNSUBSCRIBE\"}", TestContext.Current.CancellationToken);

        fake.SentText.Should().HaveCount(2);
        fake.SentText[0].Should().Be("{\"method\":\"SUBSCRIBE\"}");
        fake.SentText[1].Should().Be("{\"method\":\"UNSUBSCRIBE\"}");
    }

    [Fact]
    public async Task Fake_SendPongAsync_CapturesPong()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        var payload = Encoding.UTF8.GetBytes("pong-data");
        await fake.SendPongAsync(payload, TestContext.Current.CancellationToken);

        fake.SentPongs.Should().HaveCount(1);
        fake.SentPongs[0].Span.SequenceEqual(payload).Should().BeTrue();
    }

    [Fact]
    public async Task Fake_SimulateDisconnect_ReturnsNullFromReceive()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        fake.SimulateDisconnect();
        var result = await fake.ReceiveAsync(TestContext.Current.CancellationToken);

        result.Should().BeNull();
        fake.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Fake_SimulateReconnect_RestoresOpenState()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        fake.SimulateDisconnect();
        fake.SimulateReconnect();

        fake.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Fake_CloseAsync_SetsStateClosed()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        await fake.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", TestContext.Current.CancellationToken);

        fake.State.Should().Be(WebSocketState.Closed);
        fake.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Fake_MultipleFrames_DeliveredInOrder()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        fake.EnqueueFrame("frame-1");
        fake.EnqueueFrame("frame-2");
        fake.EnqueueFrame("frame-3");

        var f1 = await fake.ReceiveAsync(TestContext.Current.CancellationToken);
        var f2 = await fake.ReceiveAsync(TestContext.Current.CancellationToken);
        var f3 = await fake.ReceiveAsync(TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(f1!.Value.Span).Should().Be("frame-1");
        Encoding.UTF8.GetString(f2!.Value.Span).Should().Be("frame-2");
        Encoding.UTF8.GetString(f3!.Value.Span).Should().Be("frame-3");
    }
}
