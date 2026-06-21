using System.Collections.Concurrent;
using System.Text;
using AwesomeAssertions;
using Xunit;
using CryptoExchanges.Net.Core.Streaming;
using CryptoExchanges.Net.Http.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoExchanges.Net.Http.Tests.Unit.Streaming;

/// <summary>
/// Unit tests verifying that <see cref="StreamEngine"/> calls
/// <see cref="IStreamProtocol.ResolveConnectionAsync"/> at the correct points in the
/// engine lifecycle — no network required.
/// </summary>
public sealed class StreamEngineResolveConnectionTests
{
    private static StreamEngine BuildEngine(
        FakeStreamProtocol protocol,
        FakeWebSocketConnection fake,
        StreamEngineOptions? options = null)
    {
        options ??= new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromMilliseconds(200),
            BackoffInitial = TimeSpan.FromMilliseconds(10),
            BackoffMax = TimeSpan.FromMilliseconds(50),
            BackoffMultiplier = 2.0,
        };

        var registry = new StreamDecoderRegistry();
        registry.Register(StreamKind.Ticker, bytes => Encoding.UTF8.GetString(bytes.Span));

        return new StreamEngine(
            protocol,
            registry,
            options,
            () => fake,
            NullLogger.Instance);
    }

    [Fact]
    public async Task Engine_CallsResolveConnectionAsync_OnFirstConnect()
    {
        // Arrange
        var protocol = new FakeStreamProtocol();
        var fake = new FakeWebSocketConnection();
        await using var engine = BuildEngine(protocol, fake);

        protocol.ResolveCount.Should().Be(0, "resolve must not be called before any subscribe");

        // Act
        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
        await using var sub = await engine.SubscribeAsync(
            request, new StreamHandlers<string>(OnUpdate: _ => ValueTask.CompletedTask),
            TestContext.Current.CancellationToken);

        // Assert
        protocol.ResolveCount.Should().Be(1, "resolve must be called exactly once on first connect");
        fake.ConnectCount.Should().Be(1);
    }

    [Fact]
    public async Task Engine_CallsResolveConnectionAsync_OnEachReconnect()
    {
        // Arrange
        var protocol = new FakeStreamProtocol();
        var fake = new FakeWebSocketConnection();
        var options = new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromMilliseconds(500),
            BackoffInitial = TimeSpan.FromMilliseconds(10),
            BackoffMax = TimeSpan.FromMilliseconds(50),
            BackoffMultiplier = 2.0,
        };
        await using var engine = BuildEngine(protocol, fake, options);

        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
        await using var sub = await engine.SubscribeAsync(
            request, new StreamHandlers<string>(OnUpdate: _ => ValueTask.CompletedTask),
            TestContext.Current.CancellationToken);

        protocol.ResolveCount.Should().Be(1);

        // Act — simulate venue disconnect to trigger reconnect
        fake.SimulateDisconnect();

        // Wait for reconnect (ConnectCount goes from 1 to 2)
        var deadline = Task.Delay(5000, TestContext.Current.CancellationToken);
        while (fake.ConnectCount < 2 && !deadline.IsCompleted)
            await Task.Delay(20, TestContext.Current.CancellationToken);

        // Assert — resolve called again on reconnect
        fake.ConnectCount.Should().BeGreaterThanOrEqualTo(2, "engine must have reconnected");
        protocol.ResolveCount.Should().BeGreaterThanOrEqualTo(2,
            "ResolveConnectionAsync must be called on every reconnect attempt, not cached from the first connect");
    }

    [Fact]
    public async Task Engine_ResolveOnReconnect_NotCachedFromFirstConnect()
    {
        // Arrange — verify resolve count increments with each connect by simulating two disconnects
        var protocol = new FakeStreamProtocol();
        var fake = new FakeWebSocketConnection();
        var options = new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromMilliseconds(500),
            BackoffInitial = TimeSpan.FromMilliseconds(10),
            BackoffMax = TimeSpan.FromMilliseconds(50),
            BackoffMultiplier = 2.0,
        };
        await using var engine = BuildEngine(protocol, fake, options);

        var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
        await using var sub = await engine.SubscribeAsync(
            request, new StreamHandlers<string>(OnUpdate: _ => ValueTask.CompletedTask),
            TestContext.Current.CancellationToken);

        // First reconnect
        fake.SimulateDisconnect();
        var deadline = Task.Delay(5000, TestContext.Current.CancellationToken);
        while (fake.ConnectCount < 2 && !deadline.IsCompleted)
            await Task.Delay(20, TestContext.Current.CancellationToken);

        var resolveAfterFirst = protocol.ResolveCount;

        // Second reconnect
        fake.SimulateDisconnect();
        deadline = Task.Delay(5000, TestContext.Current.CancellationToken);
        while (fake.ConnectCount < 3 && !deadline.IsCompleted)
            await Task.Delay(20, TestContext.Current.CancellationToken);

        protocol.ResolveCount.Should().BeGreaterThan(resolveAfterFirst,
            "each reconnect must call ResolveConnectionAsync afresh");
    }

    [Fact]
    public async Task Engine_PropagatesOperationCanceledException_WhenResolveIsCancelled()
    {
        // Arrange — a protocol that throws OCE on resolve
        var cancellingProtocol = new CancellingStreamProtocol();
        var fake = new FakeWebSocketConnection();

        var registry = new StreamDecoderRegistry();
        registry.Register(StreamKind.Ticker, bytes => Encoding.UTF8.GetString(bytes.Span));
        var engine = new StreamEngine(
            cancellingProtocol,
            registry,
            new StreamEngineOptions
            {
                IdleCloseDelay = TimeSpan.FromMilliseconds(200),
                BackoffInitial = TimeSpan.FromMilliseconds(10),
                BackoffMax = TimeSpan.FromMilliseconds(50),
                BackoffMultiplier = 2.0,
            },
            () => fake,
            NullLogger.Instance);

        await using (engine)
        {
            // Act — SubscribeAsync triggers OpenSocketAsync which calls ResolveConnectionAsync;
            // the CancellingStreamProtocol throws OperationCanceledException, which must propagate.
            var act = async () => await engine.SubscribeAsync(
                new StreamRequest(StreamKind.Ticker, "BTCUSDT"),
                new StreamHandlers<string>(OnUpdate: _ => ValueTask.CompletedTask),
                TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>(
                "cancellation from ResolveConnectionAsync must abort the connect attempt");
        }
    }

    /// <summary>
    /// A protocol that throws <see cref="OperationCanceledException"/> from
    /// <see cref="ResolveConnectionAsync"/> to test engine cancellation propagation.
    /// </summary>
    private sealed class CancellingStreamProtocol : IStreamProtocol
    {
        /// <inheritdoc/>
        public ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct)
            => throw new OperationCanceledException("Simulated cancellation from resolve.");

        /// <inheritdoc/>
        public string RoutingKeyFor(StreamRequest request) => "btcusdt@ticker";

        /// <inheritdoc/>
        public string BuildSubscribe(StreamRequest request) => "SUBSCRIBE";

        /// <inheritdoc/>
        public string BuildUnsubscribe(StreamRequest request) => "UNSUBSCRIBE";

        /// <inheritdoc/>
        public StreamFrame Classify(ReadOnlySpan<byte> frame) => new(FrameKind.Data, "btcusdt@ticker");
    }
}
