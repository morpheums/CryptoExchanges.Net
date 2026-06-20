using System.Collections.Concurrent;
using System.Text;
using AwesomeAssertions;
using Xunit;
using CryptoExchanges.Net.Core.Streaming;
using CryptoExchanges.Net.Http.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoExchanges.Net.Http.Tests.Unit.Streaming;

/// <summary>
/// Unit tests for <see cref="StreamEngine"/> using <see cref="FakeWebSocketConnection"/>
/// and <see cref="FakeStreamProtocol"/> — no network required.
/// </summary>
public sealed class StreamEngineTests
{

    private static (StreamEngine engine, FakeWebSocketConnection fake) BuildEngine(
        FakeStreamProtocol? protocol = null,
        StreamEngineOptions? options = null,
        FakeWebSocketConnection? fake = null)
    {
        fake ??= new FakeWebSocketConnection();
        protocol ??= new FakeStreamProtocol();
        options ??= new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromMilliseconds(200),
            BackoffInitial = TimeSpan.FromMilliseconds(10),
            BackoffMax = TimeSpan.FromMilliseconds(50),
            BackoffMultiplier = 2.0,
        };

        var registry = new StreamDecoderRegistry();
        // Register an identity decoder for Ticker: returns the raw frame string wrapped in a box.
        registry.Register(StreamKind.Ticker, bytes => Encoding.UTF8.GetString(bytes.Span));
        registry.Register(StreamKind.Trade, bytes => Encoding.UTF8.GetString(bytes.Span));
        registry.Register(StreamKind.OrderBook, bytes => Encoding.UTF8.GetString(bytes.Span));
        registry.Register(StreamKind.Kline, bytes => Encoding.UTF8.GetString(bytes.Span));

        var capturedFake = fake;
        var engine = new StreamEngine(
            protocol,
            registry,
            options,
            () => capturedFake,
            NullLogger.Instance);

        return (engine, fake);
    }

    private static StreamHandlers<string> MakeHandlers(
        ConcurrentQueue<string>? received = null,
        ConcurrentQueue<StreamLag>? lagged = null,
        ConcurrentQueue<string>? lifecycle = null,
        Func<string, ValueTask>? onUpdate = null)
    {
        received ??= new ConcurrentQueue<string>();
        return new StreamHandlers<string>(
            OnUpdate: onUpdate ?? (item =>
            {
                received.Enqueue(item);
                return ValueTask.CompletedTask;
            }),
            OnReconnecting: lifecycle is null ? null : () =>
            {
                lifecycle.Enqueue("reconnecting");
                return ValueTask.CompletedTask;
            },
            OnReconnected: lifecycle is null ? null : () =>
            {
                lifecycle.Enqueue("reconnected");
                return ValueTask.CompletedTask;
            },
            OnLagged: lagged is null ? null : (lag =>
            {
                lagged.Enqueue(lag);
                return ValueTask.CompletedTask;
            }));
    }


    [Fact]
    public void BuildRoutingKey_Ticker_ProducesExpectedKey()
    {
        var key = StreamEngine.BuildRoutingKey(new StreamRequest(StreamKind.Ticker, "BTCUSDT"));
        key.Should().Be("BTCUSDT@TICKER");
    }

    [Fact]
    public void BuildRoutingKey_OrderBook_WithDepth_ProducesExpectedKey()
    {
        var key = StreamEngine.BuildRoutingKey(new StreamRequest(StreamKind.OrderBook, "BTCUSDT", Depth: 20));
        key.Should().Be("BTCUSDT@ORDERBOOK/20");
    }

    [Fact]
    public void BuildRoutingKey_Kline_WithInterval_ProducesExpectedKey()
    {
        var key = StreamEngine.BuildRoutingKey(new StreamRequest(StreamKind.Kline, "ETHUSDT", Interval: "1m"));
        key.Should().Be("ETHUSDT@KLINE/1m");
    }


    [Fact]
    public void BackoffSchedule_Next_AdvancesAttemptAndDelay()
    {
        var sched = new BackoffSchedule(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), 2.0);
        var d1 = sched.Next();
        var d2 = sched.Next();
        sched.Attempt.Should().Be(2);
        d1.Should().BeCloseTo(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(150));
        d2.Should().BeGreaterThan(d1);
    }

    [Fact]
    public void BackoffSchedule_Reset_ResetsToInitial()
    {
        var sched = new BackoffSchedule(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), 2.0);
        sched.Next();
        sched.Next();
        sched.Reset();
        sched.Attempt.Should().Be(0);
        sched.Next().Should().BeCloseTo(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(150));
    }

    [Fact]
    public void BackoffSchedule_Max_IsCapped()
    {
        var sched = new BackoffSchedule(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50), 100.0);
        // After a few calls the delay should be capped at 50 ms.
        for (var i = 0; i < 5; i++) sched.Next();
        sched.Next().Should().BeLessThanOrEqualTo(TimeSpan.FromMilliseconds(60)); // allow small jitter
    }

    [Fact]
    public void BackoffSchedule_InvalidArgs_Throw()
    {
        var act1 = () => new BackoffSchedule(TimeSpan.Zero, TimeSpan.FromSeconds(1), 2.0);
        var act2 = () => new BackoffSchedule(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1), 2.0);
        var act3 = () => new BackoffSchedule(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), 0.5);
        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
        act3.Should().Throw<ArgumentOutOfRangeException>();
    }


    [Fact]
    public async Task Engine_Subscribe_OpensSocketOnFirstSubscribe()
    {
        var (engine, fake) = BuildEngine();
        await using (engine)
        {
            var received = new ConcurrentQueue<string>();
            var handlers = MakeHandlers(received);
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");

            await using var sub = await engine.SubscribeAsync(request, handlers, TestContext.Current.CancellationToken);

            fake.ConnectCount.Should().Be(1);
            fake.IsOpen.Should().BeTrue();
            sub.State.Should().Be(StreamConnectionState.Live);
            sub.IsConnected.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Engine_Subscribe_SendsSubscribeMessage()
    {
        var (engine, fake) = BuildEngine();
        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(), TestContext.Current.CancellationToken);

            fake.SentText.Should().ContainSingle(m => m.Contains("SUBSCRIBE:btcusdt@ticker"));
        }
    }


    [Fact]
    public async Task Engine_DataFrame_RoutedToCorrectSubscription()
    {
        var (engine, fake) = BuildEngine();
        await using (engine)
        {
            var received = new ConcurrentQueue<string>();
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(received), TestContext.Current.CancellationToken);

            // Enqueue a Data frame with the correct routing key.
            fake.EnqueueFrame("{\"price\":\"50000\"}");

            // Wait for the delivery.
            var deadline = Task.Delay(2000, TestContext.Current.CancellationToken);
            while (received.IsEmpty && !deadline.IsCompleted)
                await Task.Delay(10, TestContext.Current.CancellationToken);

            received.Should().ContainSingle();
            received.TryDequeue(out var item).Should().BeTrue();
            item.Should().Be("{\"price\":\"50000\"}");
        }
    }

    [Fact]
    public async Task Engine_DataFrame_RoutedToCorrectSub_WhenMultipleActive()
    {
        var protocol = new FakeStreamProtocol();
        var (engine, fake) = BuildEngine(protocol: protocol);
        await using (engine)
        {
            var tickerReceived = new ConcurrentQueue<string>();
            var tradeReceived = new ConcurrentQueue<string>();

            var tickerRequest = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            var tradeRequest = new StreamRequest(StreamKind.Trade, "BTCUSDT");

            // Subscribe ticker with the default NextRoutingKey ("btcusdt@ticker").
            await using var tickerSub = await engine.SubscribeAsync(tickerRequest, MakeHandlers(tickerReceived), TestContext.Current.CancellationToken);

            // Switch the fake's routing key BEFORE subscribing trade so the engine registers
            // trade under "btcusdt@trade" (a different key than ticker).
            protocol.NextRoutingKey = "btcusdt@trade";
            await using var tradeSub = await engine.SubscribeAsync(tradeRequest, MakeHandlers(tradeReceived), TestContext.Current.CancellationToken);

            // Enqueue a frame now; NextRoutingKey is "btcusdt@trade" so Classify routes it
            // to the trade subscription and NOT to the ticker subscription.
            fake.EnqueueFrame("{\"side\":\"buy\"}");

            var deadline = Task.Delay(2000, TestContext.Current.CancellationToken);
            while (tradeReceived.IsEmpty && !deadline.IsCompleted)
                await Task.Delay(10, TestContext.Current.CancellationToken);

            tradeReceived.Should().ContainSingle();
            tickerReceived.Should().BeEmpty("Ticker subscription should not receive trade frames.");
        }
    }

    [Fact]
    public async Task Engine_AckFrame_IsDiscarded()
    {
        var protocol = new FakeStreamProtocol { DefaultKind = FrameKind.Ack };
        var (engine, fake) = BuildEngine(protocol: protocol);
        await using (engine)
        {
            var received = new ConcurrentQueue<string>();
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(received), TestContext.Current.CancellationToken);

            fake.EnqueueFrame("{\"ack\":true}");
            await Task.Delay(200, TestContext.Current.CancellationToken);

            received.Should().BeEmpty("Ack frames must be discarded by the engine.");
        }
    }

    [Fact]
    public async Task Engine_PongFrame_ResetsLiveness_AndIsNotRouted()
    {
        var protocol = new FakeStreamProtocol { DefaultKind = FrameKind.Pong };
        var (engine, fake) = BuildEngine(protocol: protocol);
        await using (engine)
        {
            var received = new ConcurrentQueue<string>();
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(received), TestContext.Current.CancellationToken);

            fake.EnqueueFrame("{\"pong\":true}");
            await Task.Delay(200, TestContext.Current.CancellationToken);

            received.Should().BeEmpty("Pong frames must not be routed to subscription channels.");
        }
    }


    [Fact]
    public async Task Engine_DecodeClosureInvoked_ObjectDeliveredToCallback()
    {
        // Build a registry where Ticker decodes to a specific sentinel object.
        var registry = new StreamDecoderRegistry();
        var sentinel = new object();
        registry.Register(StreamKind.Ticker, _ => sentinel);

        var fake = new FakeWebSocketConnection();
        var options = new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromMilliseconds(200),
            BackoffInitial = TimeSpan.FromMilliseconds(10),
            BackoffMax = TimeSpan.FromMilliseconds(50),
        };

        var received = new ConcurrentQueue<object>();
        var engine = new StreamEngine(
            new FakeStreamProtocol(),
            registry,
            options,
            () => fake,
            NullLogger.Instance);

        await using (engine)
        {
            var handlers = new StreamHandlers<object>(
                OnUpdate: item =>
                {
                    received.Enqueue(item);
                    return ValueTask.CompletedTask;
                });

            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, handlers, TestContext.Current.CancellationToken);

            fake.EnqueueFrame("irrelevant-bytes");

            var deadline = Task.Delay(2000, TestContext.Current.CancellationToken);
            while (received.IsEmpty && !deadline.IsCompleted)
                await Task.Delay(10, TestContext.Current.CancellationToken);

            received.Should().ContainSingle();
            received.TryDequeue(out var item).Should().BeTrue();
            item.Should().BeSameAs(sentinel);
        }
    }


    [Fact]
    public async Task Engine_FIFO_PreservedWithinSingleSubscription()
    {
        var (engine, fake) = BuildEngine();
        await using (engine)
        {
            var received = new ConcurrentQueue<string>();
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(received), TestContext.Current.CancellationToken);

            // Enqueue multiple frames in order.
            for (var i = 0; i < 5; i++)
                fake.EnqueueFrame($"{{\"seq\":{i}}}");

            var deadline = Task.Delay(3000, TestContext.Current.CancellationToken);
            while (received.Count < 5 && !deadline.IsCompleted)
                await Task.Delay(10, TestContext.Current.CancellationToken);

            var items = received.ToArray();
            items.Should().HaveCount(5);
            for (var i = 0; i < 5; i++)
                items[i].Should().Be($"{{\"seq\":{i}}}");
        }
    }


    [Fact]
    public async Task Engine_Backpressure_DropOldest_LagCallbackFires()
    {
        // Use a channel capacity of 2 so it fills with 3+ concurrent items.
        var options = new StreamEngineOptions
        {
            ChannelCapacity = 2,
            IdleCloseDelay = TimeSpan.FromMilliseconds(200),
            BackoffInitial = TimeSpan.FromMilliseconds(10),
            BackoffMax = TimeSpan.FromMilliseconds(50),
        };
        var fake = new FakeWebSocketConnection();
        var registry = new StreamDecoderRegistry();
        registry.Register(StreamKind.Ticker, bytes => Encoding.UTF8.GetString(bytes.Span));

        var lagged = new ConcurrentQueue<StreamLag>();
        var received = new ConcurrentQueue<string>();

        // The consumer stall gate: starts at 0 so first OnUpdate blocks.
        var stall = new SemaphoreSlim(0);
        // Signal from consumer that it has started processing frame0.
        var consumerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var protocol = new FakeStreamProtocol();
        var engine = new StreamEngine(protocol, registry, options, () => fake, NullLogger.Instance);

        await using (engine)
        {
            var handlers = new StreamHandlers<string>(
                OnUpdate: async item =>
                {
                    received.Enqueue(item);
                    // Signal once that the consumer has started.
                    consumerStarted.TrySetResult();
                    // Stall until released.
                    await stall.WaitAsync(TestContext.Current.CancellationToken);
                },
                OnLagged: lag =>
                {
                    lagged.Enqueue(lag);
                    return ValueTask.CompletedTask;
                });

            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, handlers, TestContext.Current.CancellationToken);

            // Enqueue frame0 — pump routes it to consumer, consumer stalls.
            fake.EnqueueFrame("frame0");

            // Wait until the consumer is stalled on frame0 before flooding.
            await consumerStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

            // Now flood — consumer is blocked, channel will fill and drops will occur.
            // With capacity=2: frame1, frame2 fill the channel; frame3..frame7 drop frame1 or frame2.
            for (var i = 1; i <= 7; i++)
                fake.EnqueueFrame($"frame{i}");

            // Give pump time to process all 7 overflow frames.
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Release consumer — it will now process frame0 and then report the accumulated lag.
            stall.Release(20);

            var deadline = Task.Delay(3000, TestContext.Current.CancellationToken);
            while (lagged.IsEmpty && !deadline.IsCompleted)
                await Task.Delay(20, TestContext.Current.CancellationToken);

            lagged.Should().NotBeEmpty("OnLagged must fire when frames are dropped due to backpressure.");
            lagged.TryDequeue(out var lagEvent).Should().BeTrue();
            lagEvent.DroppedCount.Should().BeGreaterThan(0);
        }
    }


    [Fact]
    public async Task Engine_CallbackException_PumpSurvives_SubsequentFramesDelivered()
    {
        var (engine, fake) = BuildEngine();
        await using (engine)
        {
            var received = new ConcurrentQueue<string>();
            var callCount = 0;

            var handlers = new StreamHandlers<string>(
                OnUpdate: item =>
                {
                    Interlocked.Increment(ref callCount);
                    if (callCount == 1)
                        throw new InvalidOperationException("Simulated callback failure.");
                    received.Enqueue(item);
                    return ValueTask.CompletedTask;
                });

            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, handlers, TestContext.Current.CancellationToken);

            // First frame causes the callback to throw.
            fake.EnqueueFrame("frame-throws");
            await Task.Delay(200, TestContext.Current.CancellationToken);

            // Second frame should still be delivered — pump must survive the exception.
            fake.EnqueueFrame("frame-ok");

            var deadline = Task.Delay(3000, TestContext.Current.CancellationToken);
            while (received.IsEmpty && !deadline.IsCompleted)
                await Task.Delay(10, TestContext.Current.CancellationToken);

            received.Should().ContainSingle("The pump must continue delivering frames after a callback exception.");
            received.TryDequeue(out var item).Should().BeTrue();
            item.Should().Be("frame-ok");
        }
    }


    [Fact]
    public async Task Engine_Reconnect_AutoResubscribes_StoredSubscribeSet()
    {
        var protocol = new FakeStreamProtocol();
        var options = new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromMilliseconds(500),
            BackoffInitial = TimeSpan.FromMilliseconds(20),
            BackoffMax = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0,
        };

        var fake = new FakeWebSocketConnection();
        var registry = new StreamDecoderRegistry();
        registry.Register(StreamKind.Ticker, bytes => Encoding.UTF8.GetString(bytes.Span));

        var engine = new StreamEngine(protocol, registry, options, () => fake, NullLogger.Instance);

        var lifecycle = new ConcurrentQueue<string>();
        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            var handlers = MakeHandlers(lifecycle: lifecycle);
            await using var sub = await engine.SubscribeAsync(request, handlers, TestContext.Current.CancellationToken);

            // Verify initial subscribe message was sent.
            var sentBefore = fake.SentText.ToArray();
            sentBefore.Should().ContainSingle(m => m.Contains("SUBSCRIBE"));

            // Simulate venue disconnect — engine should reconnect automatically.
            fake.SimulateDisconnect();

            // Engine will reconnect; fake ConnectAsync increments ConnectCount automatically.
            var deadline = Task.Delay(5000, TestContext.Current.CancellationToken);
            while (fake.ConnectCount < 2 && !deadline.IsCompleted)
                await Task.Delay(20, TestContext.Current.CancellationToken);

            fake.ConnectCount.Should().BeGreaterThanOrEqualTo(2, "Engine must reconnect.");

            // Engine should have sent a subscribe message again (K2 replay).
            var allSent = fake.SentText.ToArray();
            allSent.Where(m => m.Contains("SUBSCRIBE", StringComparison.Ordinal)).Should().HaveCountGreaterThanOrEqualTo(2,
                "Engine must replay the subscribe set on reconnect (K2).");

            // State should go Reconnecting → Live.
            var deadline2 = Task.Delay(3000, TestContext.Current.CancellationToken);
            while (!lifecycle.Contains("reconnected") && !deadline2.IsCompleted)
                await Task.Delay(20, TestContext.Current.CancellationToken);

            lifecycle.Should().Contain("reconnecting");
            lifecycle.Should().Contain("reconnected");
            sub.State.Should().Be(StreamConnectionState.Live);
        }
    }

    [Fact]
    public async Task Engine_Unsubscribe_RemovesFromReplaySet_NotResurrectedOnReconnect()
    {
        var protocol = new FakeStreamProtocol();
        var options = new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromMilliseconds(500),
            BackoffInitial = TimeSpan.FromMilliseconds(20),
            BackoffMax = TimeSpan.FromMilliseconds(100),
        };

        var fake = new FakeWebSocketConnection();
        var registry = new StreamDecoderRegistry();
        registry.Register(StreamKind.Ticker, bytes => Encoding.UTF8.GetString(bytes.Span));
        registry.Register(StreamKind.Trade, bytes => Encoding.UTF8.GetString(bytes.Span));

        var engine = new StreamEngine(protocol, registry, options, () => fake, NullLogger.Instance);

        await using (engine)
        {
            var tickerRequest = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            var tradeRequest = new StreamRequest(StreamKind.Trade, "BTCUSDT");

            // Subscribe ticker under the default NextRoutingKey ("btcusdt@ticker").
            var tickerSub = await engine.SubscribeAsync(tickerRequest, MakeHandlers(), TestContext.Current.CancellationToken);

            // Switch the fake's routing key so trade registers under a distinct key.
            protocol.NextRoutingKey = "btcusdt@trade";
            await using var tradeSub = await engine.SubscribeAsync(tradeRequest, MakeHandlers(), TestContext.Current.CancellationToken);

            // Unsubscribe ticker — removes "btcusdt@ticker" from replay set (K2).
            await tickerSub.DisposeAsync();

            // Capture the set of messages sent before the disconnect boundary.
            var sentBefore = fake.SentText.ToArray();

            // Simulate disconnect.
            fake.SimulateDisconnect();

            // Wait for the engine to reconnect (ConnectCount increments during ConnectAsync).
            var deadline = Task.Delay(5000, TestContext.Current.CancellationToken);
            while (fake.ConnectCount < 2 && !deadline.IsCompleted)
                await Task.Delay(20, TestContext.Current.CancellationToken);

            fake.ConnectCount.Should().BeGreaterThanOrEqualTo(2);

            // Wait for the K2 replay subscribe to arrive in SentText. The replay happens
            // inside ReconnectCoreAsync after ConnectAsync returns, so ConnectCount >= 2
            // does not guarantee the replay send has completed. We poll until at least one
            // new message (the Trade replay) appears beyond sentBefore.Length.
            var replayDeadline = Task.Delay(3000, TestContext.Current.CancellationToken);
            while (fake.SentText.Count <= sentBefore.Length && !replayDeadline.IsCompleted)
                await Task.Delay(20, TestContext.Current.CancellationToken);

            // After reconnect, only Trade should be resubscribed (not Ticker — removed from replay set).
            var allSent = fake.SentText.ToArray();
            var replaySubscribes = allSent.Skip(sentBefore.Length)
                .Where(m => m.Contains("SUBSCRIBE", StringComparison.Ordinal))
                .ToArray();

            replaySubscribes.Should().NotBeEmpty("Engine must replay the subscribe set on reconnect (K2).");
            replaySubscribes.Should().AllSatisfy(m => m.Should().Contain("btcusdt@trade"),
                "Only the still-active Trade subscription should be replayed (K2).");
            replaySubscribes.Should().NotContain(m => m.Contains("btcusdt@ticker", StringComparison.Ordinal),
                "The unsubscribed Ticker stream must not be resurrected on reconnect (K2).");
        }
    }


    [Fact]
    public async Task Engine_LifecycleState_ConnectingToLiveOnSubscribe()
    {
        var (engine, fake) = BuildEngine();
        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(), TestContext.Current.CancellationToken);

            sub.State.Should().Be(StreamConnectionState.Live);
        }
    }

    [Fact]
    public async Task Engine_LifecycleState_ClosedOnDispose()
    {
        var (engine, fake) = BuildEngine();
        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            var sub = await engine.SubscribeAsync(request, MakeHandlers(), TestContext.Current.CancellationToken);
            await sub.DisposeAsync();
            sub.State.Should().Be(StreamConnectionState.Closed);
        }
    }


    [Fact]
    public async Task Engine_IdleClose_ClosesSocketAfterLastUnsubscribe()
    {
        var options = new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromMilliseconds(100),
            BackoffInitial = TimeSpan.FromMilliseconds(10),
            BackoffMax = TimeSpan.FromMilliseconds(50),
        };
        var fake = new FakeWebSocketConnection();
        var (engine, _) = BuildEngine(options: options, fake: fake);

        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            var sub = await engine.SubscribeAsync(request, MakeHandlers(), TestContext.Current.CancellationToken);
            fake.IsOpen.Should().BeTrue();

            // Unsubscribe — starts the idle-close timer.
            await sub.DisposeAsync();

            // After the idle window passes, the socket should be closed.
            var deadline = Task.Delay(3000, TestContext.Current.CancellationToken);
            while (fake.IsOpen && !deadline.IsCompleted)
                await Task.Delay(20, TestContext.Current.CancellationToken);

            fake.IsOpen.Should().BeFalse("Socket must be closed after idle window with no active subscriptions.");
        }
    }


    [Fact]
    public async Task Engine_HeartbeatClientPing_Text_SendsPingAtInterval()
    {
        var payload = Encoding.UTF8.GetBytes("{\"op\":\"ping\"}");
        var protocol = new FakeStreamProtocol
        {
            HeartbeatPolicy = new HeartbeatPolicy(
                Direction: HeartbeatDirection.ClientPing,
                Interval: TimeSpan.FromMilliseconds(100),
                Timeout: TimeSpan.FromSeconds(10),
                ClientPingPayload: payload,
                PingFormat: PingFormat.Text),
        };

        var fake = new FakeWebSocketConnection();
        var (engine, _) = BuildEngine(protocol: protocol, fake: fake);

        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(), TestContext.Current.CancellationToken);

            // Wait for at least 2 ping cycles.
            var deadline = Task.Delay(3000, TestContext.Current.CancellationToken);
            while (fake.SentText.Count(m => m == "{\"op\":\"ping\"}") < 2 && !deadline.IsCompleted)
                await Task.Delay(20, TestContext.Current.CancellationToken);

            var pingCount = fake.SentText.Count(m => m == "{\"op\":\"ping\"}");
            pingCount.Should().BeGreaterThanOrEqualTo(2, "Engine must send client pings at the configured interval (C1).");
        }
    }

    [Fact]
    public async Task Engine_HeartbeatClientPing_ControlFrame_NoManualSend()
    {
        // PingFormat.ControlFrame: ClientWebSocket.SendAsync cannot emit RFC 6455 control frames.
        // The engine must NOT attempt a manual Ping/Pong data-frame send; framework keep-alive
        // (ClientWebSocketOptions.KeepAliveInterval on ClientWebSocketConnection) handles the
        // control-frame handshake automatically. The engine loop still sets livenessFlag = 1
        // on each heartbeat tick so the watchdog does not falsely trigger.
        var payload = Encoding.UTF8.GetBytes("ping");
        var protocol = new FakeStreamProtocol
        {
            HeartbeatPolicy = new HeartbeatPolicy(
                Direction: HeartbeatDirection.ClientPing,
                Interval: TimeSpan.FromMilliseconds(100),
                Timeout: TimeSpan.FromSeconds(10),
                ClientPingPayload: payload,
                PingFormat: PingFormat.ControlFrame),
        };

        var fake = new FakeWebSocketConnection();
        var (engine, _) = BuildEngine(protocol: protocol, fake: fake);

        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(), TestContext.Current.CancellationToken);

            // Wait several heartbeat intervals to confirm no manual send occurs.
            await Task.Delay(400, TestContext.Current.CancellationToken);

            fake.SentPings.Should().BeEmpty(
                "PingFormat.ControlFrame must NOT send a data-frame ping — framework keep-alive handles it.");
            fake.SentPongs.Should().BeEmpty(
                "PingFormat.ControlFrame must NOT send a data-frame pong.");
            // Subscribe text was sent; no heartbeat data frames added.
            fake.SentText.Should().ContainSingle(
                "only the subscribe message should be in SentText; no heartbeat text frames.");
        }
    }

    [Fact]
    public async Task Engine_HeartbeatServerPingClientPong_SendsPong()
    {
        var protocol = new FakeStreamProtocol
        {
            HeartbeatPolicy = new HeartbeatPolicy(
                Direction: HeartbeatDirection.ServerPingClientPong,
                Interval: TimeSpan.FromSeconds(30),
                Timeout: TimeSpan.FromSeconds(60)),
            // Classify all frames as Pong to simulate server sending a ping that the engine responds to.
            DefaultKind = FrameKind.Pong,
        };

        var fake = new FakeWebSocketConnection();
        var (engine, _) = BuildEngine(protocol: protocol, fake: fake);

        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(), TestContext.Current.CancellationToken);

            // Enqueue a pong frame — engine receives it and resets liveness.
            fake.EnqueueFrame("pong-payload");
            await Task.Delay(200, TestContext.Current.CancellationToken);

            // The engine should not have forcibly disconnected; state stays Live.
            sub.State.Should().Be(StreamConnectionState.Live);
        }
    }


    [Fact]
    public async Task Engine_Watchdog_TriggersReconnect_WhenNoLiveness()
    {
        var protocol = new FakeStreamProtocol
        {
            HeartbeatPolicy = new HeartbeatPolicy(
                Direction: HeartbeatDirection.ServerPingClientPong,
                Interval: TimeSpan.FromSeconds(30),
                Timeout: TimeSpan.FromMilliseconds(150)), // very short watchdog
        };

        var options = new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromSeconds(5),
            BackoffInitial = TimeSpan.FromMilliseconds(20),
            BackoffMax = TimeSpan.FromMilliseconds(50),
        };

        var fake = new FakeWebSocketConnection();
        var registry = new StreamDecoderRegistry();
        registry.Register(StreamKind.Ticker, bytes => Encoding.UTF8.GetString(bytes.Span));

        var engine = new StreamEngine(protocol, registry, options, () => fake, NullLogger.Instance);

        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(), TestContext.Current.CancellationToken);

            // Do NOT send any frames — watchdog will fire after Timeout.
            var deadline = Task.Delay(5000, TestContext.Current.CancellationToken);
            while (fake.ConnectCount < 2 && !deadline.IsCompleted)
                await Task.Delay(30, TestContext.Current.CancellationToken);

            fake.ConnectCount.Should().BeGreaterThanOrEqualTo(2,
                "Watchdog must trigger a reconnect when no liveness is observed within Timeout (C1).");
        }
    }

    // Regression for Finding 1: engine registered subscriptions under a canonical (uppercase)
    // key while the venue Classify returned a venue-native (lowercase) key — they never matched,
    // so every live data frame was discarded. The fix: single-source the routing keyspace through
    // IStreamProtocol.RoutingKeyFor, ensuring subscribe-time registration and receive-time lookup
    // both use the protocol's venue-native keyspace.

    [Fact]
    public async Task Engine_RoutingKey_VenueNativeKeyspace_FrameReachesSubscription()
    {
        // Use a protocol stub whose RoutingKeyFor and Classify both return a venue-native
        // (lowercase) key — mimicking the real per-exchange protocol convention.
        // The test FAILS against old code where the engine used BuildRoutingKey (canonical
        // uppercase e.g. "BTCUSDT@TICKER") while Classify returned "btcusdt@ticker".
        var venueProtocol = new VenueKeyProtocol();
        var fake = new FakeWebSocketConnection();
        var registry = new StreamDecoderRegistry();
        registry.Register(StreamKind.Ticker, bytes => Encoding.UTF8.GetString(bytes.Span));
        var engineOptions = new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromMilliseconds(200),
            BackoffInitial = TimeSpan.FromMilliseconds(10),
            BackoffMax = TimeSpan.FromMilliseconds(50),
        };
        var engine = new StreamEngine(venueProtocol, registry, engineOptions, () => fake, NullLogger.Instance);

        await using (engine)
        {
            var received = new ConcurrentQueue<string>();
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(received), TestContext.Current.CancellationToken);

            // Feed a frame. VenueKeyProtocol.Classify returns "btcusdt@ticker" (venue-native).
            // The engine must route this frame to the subscription registered via RoutingKeyFor,
            // which also returns "btcusdt@ticker". If the engine used BuildRoutingKey instead,
            // the subscription would be under "BTCUSDT@TICKER" and the frame would be discarded.
            fake.EnqueueFrame("{\"stream\":\"btcusdt@ticker\",\"data\":{}}");

            var deadline = Task.Delay(2000, TestContext.Current.CancellationToken);
            while (received.IsEmpty && !deadline.IsCompleted)
                await Task.Delay(10, TestContext.Current.CancellationToken);

            received.Should().ContainSingle(
                "the pump must route the venue-native-keyed frame to the subscription " +
                "registered via IStreamProtocol.RoutingKeyFor (venue-native keyspace)");
        }
    }

    /// <summary>
    /// A minimal <see cref="IStreamProtocol"/> stub whose <see cref="RoutingKeyFor"/> and
    /// <see cref="Classify"/> both return the same venue-native (lowercase) key, mirroring
    /// how a real per-exchange protocol works (subscribe and classify share one keyspace).
    /// Used by the routing-keyspace regression test to prove frames reach their subscription.
    /// </summary>
    private sealed class VenueKeyProtocol : IStreamProtocol
    {
        /// <inheritdoc/>
        public Uri Endpoint { get; } = new Uri("wss://fake.test/ws");

        /// <inheritdoc/>
        public string RoutingKeyFor(StreamRequest request)
            => "btcusdt@ticker"; // venue-native key — lowercase, matches Classify

        /// <inheritdoc/>
        public string BuildSubscribe(StreamRequest request)
            => "{\"method\":\"SUBSCRIBE\",\"params\":[\"btcusdt@ticker\"],\"id\":1}";

        /// <inheritdoc/>
        public string BuildUnsubscribe(StreamRequest request)
            => "{\"method\":\"UNSUBSCRIBE\",\"params\":[\"btcusdt@ticker\"],\"id\":2}";

        /// <inheritdoc/>
        public StreamFrame Classify(ReadOnlySpan<byte> frame)
            => new(FrameKind.Data, "btcusdt@ticker"); // same venue-native key — frames must route

        /// <inheritdoc/>
        public HeartbeatPolicy Heartbeat { get; } = new HeartbeatPolicy(
            Direction: HeartbeatDirection.ServerPingClientPong,
            Interval: TimeSpan.FromSeconds(30),
            Timeout: TimeSpan.FromSeconds(60));
    }


    [Fact]
    public async Task Engine_Watchdog_DoesNotTriggerReconnect_WhenDataFramesArriveRegularly()
    {
        // Regression for Finding 2: the liveness flag was only reset on FrameKind.Pong.
        // Under ClientWebSocket auto-pong (ServerPingClientPong policy) the venue Ping is
        // auto-replied to and never surfaces, so the watchdog saw no liveness and would
        // trigger a reconnect even on a healthy socket actively delivering Data frames.
        // Fix: reset liveness on ANY received frame (before classification).
        var protocol = new FakeStreamProtocol
        {
            DefaultKind = FrameKind.Data, // Data frames, not Pong
            HeartbeatPolicy = new HeartbeatPolicy(
                Direction: HeartbeatDirection.ServerPingClientPong,
                Interval: TimeSpan.FromSeconds(30),
                Timeout: TimeSpan.FromMilliseconds(200)), // short watchdog
        };

        var options = new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromSeconds(5),
            BackoffInitial = TimeSpan.FromMilliseconds(20),
            BackoffMax = TimeSpan.FromMilliseconds(50),
        };
        var fake = new FakeWebSocketConnection();
        var registry = new StreamDecoderRegistry();
        registry.Register(StreamKind.Ticker, bytes => Encoding.UTF8.GetString(bytes.Span));

        var engine = new StreamEngine(protocol, registry, options, () => fake, NullLogger.Instance);

        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            await using var sub = await engine.SubscribeAsync(request, MakeHandlers(), TestContext.Current.CancellationToken);

            // Continuously send Data frames just before the watchdog timeout so it does
            // not fire. We run for 5 × Timeout (1s total) without a reconnect.
            var runUntil = DateTimeOffset.UtcNow.AddMilliseconds(1000);
            while (DateTimeOffset.UtcNow < runUntil)
            {
                fake.EnqueueFrame("{\"ping\":\"data\"}");
                await Task.Delay(60, TestContext.Current.CancellationToken); // < 200ms Timeout
            }

            // The engine must not have reconnected — Data frames must keep the watchdog alive.
            fake.ConnectCount.Should().Be(1,
                "Data frames must reset the liveness watchdog; watchdog must not trigger a " +
                "reconnect on a socket that is actively delivering frames (Finding 2 fix).");
            sub.State.Should().Be(StreamConnectionState.Live);
        }
    }


    [Fact]
    public async Task Engine_Unsubscribe_SendsUnsubscribeMessage()
    {
        var (engine, fake) = BuildEngine();
        await using (engine)
        {
            var request = new StreamRequest(StreamKind.Ticker, "BTCUSDT");
            var sub = await engine.SubscribeAsync(request, MakeHandlers(), TestContext.Current.CancellationToken);
            await sub.DisposeAsync();

            var allSent = fake.SentText.ToArray();
            allSent.Should().Contain(m => m.Contains("UNSUBSCRIBE"),
                "DisposeAsync must trigger an unsubscribe wire message.");
        }
    }
}
