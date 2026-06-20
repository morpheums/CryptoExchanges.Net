using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
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
        using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        fake.IsOpen.Should().BeTrue();
        fake.State.Should().Be(WebSocketState.Open);
        fake.ConnectCount.Should().Be(1);
    }

    [Fact]
    public async Task Fake_EnqueuedFrames_ReceivedInOrder()
    {
        using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        fake.EnqueueFrame("frame-1");
        fake.EnqueueFrame("frame-2");

        Encoding.UTF8.GetString((await fake.ReceiveAsync(TestContext.Current.CancellationToken))!.Value.Span).Should().Be("frame-1");
        Encoding.UTF8.GetString((await fake.ReceiveAsync(TestContext.Current.CancellationToken))!.Value.Span).Should().Be("frame-2");
    }

    [Fact]
    public async Task Fake_SendText_CapturesMessages()
    {
        using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        await fake.SendTextAsync("{\"method\":\"SUBSCRIBE\"}", TestContext.Current.CancellationToken);

        fake.SentText.ToArray().Should().ContainSingle().Which.Should().Be("{\"method\":\"SUBSCRIBE\"}");
    }

    [Fact]
    public async Task Fake_Disconnect_ReceiveReturnsNull()
    {
        using var fake = new FakeWebSocketConnection();
        await fake.ConnectAsync(new Uri("wss://example.com/ws"), TestContext.Current.CancellationToken);

        fake.SimulateDisconnect();

        (await fake.ReceiveAsync(TestContext.Current.CancellationToken)).Should().BeNull();
        fake.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Fake_ConnectAsync_NullUri_Throws()
    {
        using var fake = new FakeWebSocketConnection();
        var act = async () => await fake.ConnectAsync(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── ClientWebSocketConnection guards ──────────────────────────────────────

    [Fact]
    public async Task ClientWebSocketConnection_ConnectAsync_HttpScheme_Throws()
    {
        // The URI scheme guard fires before any I/O; no network required.
        await using var conn = new ClientWebSocketConnection();
        var act = async () => await conn.ConnectAsync(new Uri("http://example.com"), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ws*");
    }

    [Fact]
    public async Task ClientWebSocketConnection_ConnectAsync_HttpsScheme_Throws()
    {
        await using var conn = new ClientWebSocketConnection();
        var act = async () => await conn.ConnectAsync(new Uri("https://example.com"), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ws*");
    }

    [Fact]
    public async Task ClientWebSocketConnection_ConnectAsync_WssScheme_DoesNotThrowArgumentException()
    {
        // Scheme check passes for wss (the underlying ConnectAsync will fail with a network
        // error, but the guard must not throw ArgumentException for a valid scheme).
        await using var conn = new ClientWebSocketConnection();
        var act = async () => await conn.ConnectAsync(new Uri("wss://example.com"), TestContext.Current.CancellationToken);
        // Must NOT throw ArgumentException (scheme guard rejects only non-ws/wss URIs).
        // It will throw some WebSocketException / network exception — that is expected.
        await act.Should().NotThrowAsync<ArgumentException>(
            "wss URIs must pass the scheme guard; only network errors are expected here.");
    }

    [Fact]
    public void ClientWebSocketConnection_MaxMessageBytes_Is4Mib()
    {
        // The const must be exactly 4 MiB so the guard matches the documented limit.
        // Tested via reflection because the field is private.
        var field = typeof(ClientWebSocketConnection)
            .GetField("MaxMessageBytes", BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull("MaxMessageBytes const must exist on ClientWebSocketConnection");
        ((int)field!.GetValue(null)!).Should().Be(4 * 1024 * 1024,
            "MaxMessageBytes must be 4 MiB (4 * 1024 * 1024 bytes)");
    }
}
