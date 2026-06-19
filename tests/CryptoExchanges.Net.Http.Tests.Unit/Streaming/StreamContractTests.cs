using System.Net.WebSockets;
using System.Text;
using FluentAssertions;
using Xunit;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Http.Tests.Unit.Streaming;

/// <summary>
/// Behavior tests for the streaming decode registry and the <see cref="FakeWebSocketConnection"/>
/// test seam that the engine tests depend on. (Record/enum value semantics are not tested — that's
/// language behavior, not ours.)
/// </summary>
public sealed class StreamContractTests
{
    // ── StreamDecoderRegistry (our logic) ─────────────────────────────────────

    [Fact]
    public void StreamDecoderRegistry_Register_And_Resolve_RoundTrips()
    {
        var registry = new StreamDecoderRegistry();
        var sentinel = new object();
        registry.Register(StreamKind.Ticker, _ => sentinel);

        registry.Resolve(StreamKind.Ticker)(ReadOnlyMemory<byte>.Empty).Should().BeSameAs(sentinel);
    }

    [Fact]
    public void StreamDecoderRegistry_Contains_ReflectsRegistration()
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

    // ── FakeWebSocketConnection seam (relied on by the engine tests) ──────────

    [Fact]
    public async Task Fake_Connect_OpensAndCounts()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        fake.IsOpen.Should().BeTrue();
        fake.State.Should().Be(WebSocketState.Open);
        fake.ConnectCount.Should().Be(1);
    }

    [Fact]
    public async Task Fake_EnqueuedFrames_ReceivedInOrder()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        fake.EnqueueFrame("frame-1");
        fake.EnqueueFrame("frame-2");

        Encoding.UTF8.GetString((await fake.ReceiveAsync(TestContext.Current.CancellationToken))!.Value.Span).Should().Be("frame-1");
        Encoding.UTF8.GetString((await fake.ReceiveAsync(TestContext.Current.CancellationToken))!.Value.Span).Should().Be("frame-2");
    }

    [Fact]
    public async Task Fake_SendText_CapturesMessages()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        await fake.SendTextAsync("{\"method\":\"SUBSCRIBE\"}", TestContext.Current.CancellationToken);

        fake.SentText.ToArray().Should().ContainSingle().Which.Should().Be("{\"method\":\"SUBSCRIBE\"}");
    }

    [Fact]
    public async Task Fake_Disconnect_ReceiveReturnsNull()
    {
        await using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        fake.SimulateDisconnect();

        (await fake.ReceiveAsync(TestContext.Current.CancellationToken)).Should().BeNull();
        fake.IsOpen.Should().BeFalse();
    }
}
