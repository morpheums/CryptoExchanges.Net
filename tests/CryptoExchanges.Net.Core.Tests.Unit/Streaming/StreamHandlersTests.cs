using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Streaming;
using FluentAssertions;
using Xunit;

namespace CryptoExchanges.Net.Core.Tests.Unit.Streaming;

public class StreamHandlersTests
{
    // ── StreamHandlers<T> construction ──────────────────────────────────────

    [Fact]
    public void StreamHandlers_RequiredOnUpdate_ShouldConstruct()
    {
        Func<Ticker, ValueTask> onUpdate = _ => ValueTask.CompletedTask;

        var handlers = new StreamHandlers<Ticker>(onUpdate);

        handlers.OnUpdate.Should().BeSameAs(onUpdate);
    }

    [Fact]
    public void StreamHandlers_OptionalCallbacks_DefaultToNull()
    {
        var handlers = new StreamHandlers<Ticker>(_ => ValueTask.CompletedTask);

        handlers.OnReconnecting.Should().BeNull();
        handlers.OnReconnected.Should().BeNull();
        handlers.OnLagged.Should().BeNull();
    }

    [Fact]
    public void StreamHandlers_AllCallbacksProvided_ShouldStore()
    {
        Func<Ticker, ValueTask> onUpdate = _ => ValueTask.CompletedTask;
        Func<ValueTask> onReconnecting = () => ValueTask.CompletedTask;
        Func<ValueTask> onReconnected = () => ValueTask.CompletedTask;
        Func<StreamLag, ValueTask> onLagged = _ => ValueTask.CompletedTask;

        var handlers = new StreamHandlers<Ticker>(onUpdate, onReconnecting, onReconnected, onLagged);

        handlers.OnUpdate.Should().BeSameAs(onUpdate);
        handlers.OnReconnecting.Should().BeSameAs(onReconnecting);
        handlers.OnReconnected.Should().BeSameAs(onReconnected);
        handlers.OnLagged.Should().BeSameAs(onLagged);
    }

    // ── StreamLag ───────────────────────────────────────────────────────────

    [Fact]
    public void StreamLag_ShouldStoreDroppedCount()
    {
        var lag = new StreamLag(42);

        lag.DroppedCount.Should().Be(42);
    }

    [Fact]
    public void StreamLag_ShouldSupportValueEquality()
    {
        var a = new StreamLag(5);
        var b = new StreamLag(5);

        a.Should().Be(b);
    }

    // ── IStreamSubscription / IsConnected ───────────────────────────────────

    [Theory]
    [InlineData(StreamConnectionState.Live, true)]
    [InlineData(StreamConnectionState.Connecting, false)]
    [InlineData(StreamConnectionState.Reconnecting, false)]
    [InlineData(StreamConnectionState.Closed, false)]
    public void IsConnected_ShouldReflect_StateLive(StreamConnectionState state, bool expected)
    {
        var sub = new FakeSubscription(state);

        sub.IsConnected.Should().Be(expected);
    }

    // ── Minimal fake subscription (compile-time contract test) ──────────────

    private sealed class FakeSubscription(StreamConnectionState state) : IStreamSubscription
    {
        public StreamConnectionState State => state;
        public bool IsConnected => State == StreamConnectionState.Live;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
