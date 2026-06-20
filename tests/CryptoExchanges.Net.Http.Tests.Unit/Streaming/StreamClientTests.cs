using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Streaming;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Http.Tests.Unit.Streaming;

/// <summary>
/// Unit tests for <see cref="StreamClient"/>, <see cref="StreamClientFactory"/>,
/// and <see cref="StreamServiceRegistration"/> using <see cref="FakeWebSocketConnection"/>
/// and <see cref="FakeStreamProtocol"/> — no network required.
/// </summary>
[Collection("StreamClientTests")]
public sealed class StreamClientTests
{
    // ── Shared sentinel models ────────────────────────────────────────────────

    private static readonly Symbol TestSymbol = new Symbol(Asset.Of("BTC"), Asset.Of("USDT"));
    private static readonly Ticker SentinelTicker = new Ticker(TestSymbol, LastPrice: 99m);
    private static readonly Trade SentinelTrade = new Trade(TestSymbol);
    private static readonly OrderBook SentinelOrderBook = new OrderBook(TestSymbol, [], []);
    private static readonly Candlestick SentinelCandlestick = new Candlestick(
        OpenTime: DateTimeOffset.UnixEpoch,
        CloseTime: DateTimeOffset.UnixEpoch.AddMinutes(1),
        Interval: KlineInterval.OneMinute,
        TradingSymbol: TestSymbol);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Wire symbol produced by <see cref="FakeSymbolMapper"/> for <see cref="TestSymbol"/>.
    /// </summary>
    private static string WireSymbol => FakeSymbolMapper.ToWireStatic(TestSymbol);

    /// <summary>
    /// Builds a wired <see cref="StreamClient"/> backed by a <see cref="FakeWebSocketConnection"/>
    /// with all four stream kinds registered to return their respective sentinel models.
    /// </summary>
    private static (StreamClient client, FakeWebSocketConnection fake, FakeStreamProtocol protocol)
        BuildClient(
            ExchangeId exchangeId = ExchangeId.Binance,
            FakeStreamProtocol? protocol = null,
            StreamEngineOptions? options = null)
    {
        var fake = new FakeWebSocketConnection();
        protocol ??= new FakeStreamProtocol();
        options ??= new StreamEngineOptions
        {
            IdleCloseDelay = TimeSpan.FromMilliseconds(200),
            BackoffInitial = TimeSpan.FromMilliseconds(10),
            BackoffMax = TimeSpan.FromMilliseconds(50),
            BackoffMultiplier = 2.0,
        };

        var registry = BuildAllDecoders();

        var capturedFake = fake;
        var engine = new StreamEngine(
            protocol,
            registry,
            options,
            () => capturedFake,
            NullLogger.Instance);

        var symbolMapper = new FakeSymbolMapper();
        var client = new StreamClient(engine, symbolMapper, exchangeId);
        return (client, fake, protocol);
    }

    // ── StreamClient.ExchangeId ───────────────────────────────────────────────

    [Fact]
    public async Task StreamClient_ExchangeId_ReturnsConstructedValue()
    {
        var (client, fake, _) = BuildClient(ExchangeId.Binance);
        await using (client)
        {
            client.ExchangeId.Should().Be(ExchangeId.Binance);
        }
        await fake.DisposeAsync();
    }

    // ── SubscribeToTickerAsync — subscribe → route → decode → deliver ─────────

    [Fact]
    public async Task SubscribeToTickerAsync_DeliversDecodedValueToHandler()
    {
        var (client, fake, _) = BuildClient();

        // FakeStreamProtocol.RoutingKeyFor and Classify both return NextRoutingKey (the
        // default "btcusdt@ticker"), so subscribe-time registration and receive-time
        // dispatch agree without any additional setup.
        var received = new ConcurrentQueue<Ticker>();
        var handlers = new StreamHandlers<Ticker>(
            OnUpdate: v =>
            {
                received.Enqueue(v);
                return ValueTask.CompletedTask;
            });

        await using (client)
        {
            await using var sub = await client.SubscribeToTickerAsync(TestSymbol, handlers);

            fake.EnqueueFrame("ignored-bytes");

            // Allow the pump loop to process the frame.
            await WaitAsync(() => !received.IsEmpty);

            received.TryDequeue(out var value).Should().BeTrue();
            value.Should().BeSameAs(SentinelTicker);
        }
        await fake.DisposeAsync();
    }

    // ── SubscribeToTradesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task SubscribeToTradesAsync_DeliversDecodedValueToHandler()
    {
        var (client, fake, _) = BuildClient();

        var received = new ConcurrentQueue<Trade>();
        var handlers = new StreamHandlers<Trade>(
            OnUpdate: v =>
            {
                received.Enqueue(v);
                return ValueTask.CompletedTask;
            });

        await using (client)
        {
            await using var sub = await client.SubscribeToTradesAsync(TestSymbol, handlers);

            fake.EnqueueFrame("trade-bytes");

            await WaitAsync(() => !received.IsEmpty);

            received.TryDequeue(out var value).Should().BeTrue();
            value.Should().BeSameAs(SentinelTrade);
        }
        await fake.DisposeAsync();
    }

    // ── SubscribeToOrderBookAsync ─────────────────────────────────────────────

    [Fact]
    public async Task SubscribeToOrderBookAsync_DeliversDecodedValueToHandler()
    {
        var (client, fake, _) = BuildClient();

        var received = new ConcurrentQueue<OrderBook>();
        var handlers = new StreamHandlers<OrderBook>(
            OnUpdate: v =>
            {
                received.Enqueue(v);
                return ValueTask.CompletedTask;
            });

        await using (client)
        {
            await using var sub = await client.SubscribeToOrderBookAsync(TestSymbol, depth: 20, handlers);

            fake.EnqueueFrame("book-bytes");

            await WaitAsync(() => !received.IsEmpty);

            received.TryDequeue(out var value).Should().BeTrue();
            value.Should().BeSameAs(SentinelOrderBook);
        }
        await fake.DisposeAsync();
    }

    // ── SubscribeToKlinesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task SubscribeToKlinesAsync_DeliversDecodedValueToHandler()
    {
        var (client, fake, _) = BuildClient();

        var received = new ConcurrentQueue<Candlestick>();
        var handlers = new StreamHandlers<Candlestick>(
            OnUpdate: v =>
            {
                received.Enqueue(v);
                return ValueTask.CompletedTask;
            });

        await using (client)
        {
            await using var sub = await client.SubscribeToKlinesAsync(TestSymbol, KlineInterval.OneMinute, handlers);

            fake.EnqueueFrame("kline-bytes");

            await WaitAsync(() => !received.IsEmpty);

            received.TryDequeue(out var value).Should().BeTrue();
            value.Should().BeSameAs(SentinelCandlestick);
        }
        await fake.DisposeAsync();
    }

    // ── Subscription State / IsConnected ─────────────────────────────────────

    [Fact]
    public async Task Subscription_AfterSubscribe_IsLiveAndConnected()
    {
        var (client, fake, _) = BuildClient();

        await using (client)
        {
            var handlers = new StreamHandlers<Ticker>(OnUpdate: _ => ValueTask.CompletedTask);
            await using var sub = await client.SubscribeToTickerAsync(TestSymbol, handlers);

            sub.State.Should().Be(StreamConnectionState.Live);
            sub.IsConnected.Should().BeTrue();
        }
        await fake.DisposeAsync();
    }

    // ── Dispose unsubscribes (K2) ─────────────────────────────────────────────

    [Fact]
    public async Task Subscription_DisposeAsync_TransitionsStateToClosed()
    {
        var (client, fake, _) = BuildClient();

        await using (client)
        {
            var handlers = new StreamHandlers<Ticker>(OnUpdate: _ => ValueTask.CompletedTask);
            var sub = await client.SubscribeToTickerAsync(TestSymbol, handlers);

            sub.State.Should().Be(StreamConnectionState.Live);

            await sub.DisposeAsync();

            sub.State.Should().Be(StreamConnectionState.Closed);
            sub.IsConnected.Should().BeFalse();
        }
        await fake.DisposeAsync();
    }

    // ── Convenience overloads (bare Func<T,ValueTask>) ─────────────────────────

    [Fact]
    public async Task SubscribeToTickerAsync_BareFunc_WrapsIntoHandlers_AndDelivers()
    {
        var (client, fake, _) = BuildClient();

        var received = new ConcurrentQueue<Ticker>();
        await using (client)
        {
            await using var sub = await client.SubscribeToTickerAsync(
                TestSymbol,
                onUpdate: v =>
                {
                    received.Enqueue(v);
                    return ValueTask.CompletedTask;
                });

            fake.EnqueueFrame("ignored");

            await WaitAsync(() => !received.IsEmpty);
            received.TryDequeue(out var value).Should().BeTrue();
            value.Should().BeSameAs(SentinelTicker);
        }
        await fake.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeToTradesAsync_BareFunc_WrapsIntoHandlers_AndDelivers()
    {
        var (client, fake, _) = BuildClient();

        var received = new ConcurrentQueue<Trade>();
        await using (client)
        {
            await using var sub = await client.SubscribeToTradesAsync(
                TestSymbol,
                onUpdate: v =>
                {
                    received.Enqueue(v);
                    return ValueTask.CompletedTask;
                });

            fake.EnqueueFrame("trade");
            await WaitAsync(() => !received.IsEmpty);
            received.TryDequeue(out var value).Should().BeTrue();
            value.Should().BeSameAs(SentinelTrade);
        }
        await fake.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeToOrderBookAsync_BareFunc_WrapsIntoHandlers_AndDelivers()
    {
        var (client, fake, _) = BuildClient();

        var received = new ConcurrentQueue<OrderBook>();
        await using (client)
        {
            await using var sub = await client.SubscribeToOrderBookAsync(
                TestSymbol,
                depth: 5,
                onUpdate: v =>
                {
                    received.Enqueue(v);
                    return ValueTask.CompletedTask;
                });

            fake.EnqueueFrame("book");
            await WaitAsync(() => !received.IsEmpty);
            received.TryDequeue(out var value).Should().BeTrue();
            value.Should().BeSameAs(SentinelOrderBook);
        }
        await fake.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeToKlinesAsync_BareFunc_WrapsIntoHandlers_AndDelivers()
    {
        var (client, fake, _) = BuildClient();

        var received = new ConcurrentQueue<Candlestick>();
        await using (client)
        {
            await using var sub = await client.SubscribeToKlinesAsync(
                TestSymbol,
                KlineInterval.OneMinute,
                onUpdate: v =>
                {
                    received.Enqueue(v);
                    return ValueTask.CompletedTask;
                });

            fake.EnqueueFrame("kline");
            await WaitAsync(() => !received.IsEmpty);
            received.TryDequeue(out var value).Should().BeTrue();
            value.Should().BeSameAs(SentinelCandlestick);
        }
        await fake.DisposeAsync();
    }

    // ── StreamClientFactory ───────────────────────────────────────────────────

    [Fact]
    public async Task StreamClientFactory_Available_ReturnsRegisteredExchangeIds()
    {
        await using var sp = BuildStreamServices();
        var factory = sp.GetRequiredService<IStreamClientFactory>();

        factory.Available.Should().Contain(ExchangeId.Binance);
    }

    [Fact]
    public async Task StreamClientFactory_GetClient_ReturnsRegisteredClient()
    {
        await using var sp = BuildStreamServices();
        var factory = sp.GetRequiredService<IStreamClientFactory>();

        var client = factory.GetClient(ExchangeId.Binance);
        client.Should().NotBeNull();
        client.ExchangeId.Should().Be(ExchangeId.Binance);
    }

    [Fact]
    public async Task StreamClientFactory_TryGet_ReturnsTrueAndClient_WhenRegistered()
    {
        await using var sp = BuildStreamServices();
        var factory = sp.GetRequiredService<IStreamClientFactory>();

        var found = factory.TryGet(ExchangeId.Binance, out var client);
        found.Should().BeTrue();
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task StreamClientFactory_TryGet_ReturnsFalse_WhenNotRegistered()
    {
        await using var sp = BuildStreamServices();
        var factory = sp.GetRequiredService<IStreamClientFactory>();

        var found = factory.TryGet(ExchangeId.Bybit, out var client);
        found.Should().BeFalse();
        client.Should().BeNull();
    }

    [Fact]
    public async Task StreamClientFactory_GetClient_Throws_WhenNotRegistered()
    {
        await using var sp = BuildStreamServices();
        var factory = sp.GetRequiredService<IStreamClientFactory>();

        var act = () => factory.GetClient(ExchangeId.Bybit);
        act.Should().Throw<CryptoExchanges.Net.Core.Exceptions.ExchangeNotRegisteredException>();
    }

    // ── AddStreams<TOptions> keyed singleton + ValidateOnStart ────────────────

    [Fact]
    public async Task AddStreams_RegistersKeyedSingleton_ResolvableViaKeyedDI()
    {
        await using var sp = BuildStreamServices();
        var client = sp.GetRequiredKeyedService<IStreamClient>(ExchangeId.Binance);

        client.Should().NotBeNull();
        client.ExchangeId.Should().Be(ExchangeId.Binance);
    }

    [Fact]
    public async Task AddStreams_IsSingleton_ReturnsSameInstanceOnMultipleResolves()
    {
        await using var sp = BuildStreamServices();
        var a = sp.GetRequiredKeyedService<IStreamClient>(ExchangeId.Binance);
        var b = sp.GetRequiredKeyedService<IStreamClient>(ExchangeId.Binance);

        a.Should().BeSameAs(b);
    }

    [Fact]
    public async Task AddStreams_OptionsConfigure_AppliesConfiguration()
    {
        await using var sp = BuildStreamServices(configure: o => o.TestTag = "configured");
        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FakeStreamOptions>>().Value;

        opts.TestTag.Should().Be("configured");
    }

    [Fact]
    public async Task AddStreams_ReusesKeyedSymbolMapper_WhenAlreadyRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Pre-register a keyed ISymbolMapper (simulating AddXxxExchange having run first).
        var preRegisteredMapper = new FakeSymbolMapper();
        services.AddKeyedSingleton<ISymbolMapper>(ExchangeId.Binance, preRegisteredMapper);

        StreamServiceRegistration.AddStreams<FakeStreamOptions>(
            services,
            ExchangeId.Binance,
            _ => new FakeStreamProtocol(),
            _ => BuildAllDecoders(),
            null);

        await using var sp = services.BuildServiceProvider();

        // The client should have been built with the pre-registered mapper (TryAddKeyedSingleton reuses it).
        var client = sp.GetRequiredKeyedService<IStreamClient>(ExchangeId.Binance);
        client.Should().NotBeNull();
    }

    // ── StreamClientFactory.Create — container-free path ─────────────────────

    [Fact]
    public async Task StreamClientFactory_Create_BuildsClientWithoutContainer()
    {
        var fake = new FakeWebSocketConnection();
        var protocol = new FakeStreamProtocol();
        var registry = BuildAllDecoders();
        var options = new StreamEngineOptions();
        var symbolMapper = new FakeSymbolMapper();

        var client = StreamClientFactory.Create(
            ExchangeId.Binance,
            protocol,
            registry,
            options,
            () => fake,
            NullLogger.Instance,
            symbolMapper);

        await using (client)
        {
            client.ExchangeId.Should().Be(ExchangeId.Binance);
        }
        await fake.DisposeAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="ServiceProvider"/> with a keyed <see cref="ISymbolMapper"/> and
    /// <see cref="IStreamClient"/> registered for <see cref="ExchangeId.Binance"/>, using
    /// the <see cref="FakeStreamProtocol"/> and decode closures that return sentinel models.
    /// Pre-registering the mapper simulates <c>AddXxxExchange</c> having run first.
    /// </summary>
    private static ServiceProvider BuildStreamServices(
        ExchangeId exchangeId = ExchangeId.Binance,
        Action<FakeStreamOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Pre-register the keyed ISymbolMapper as AddXxxExchange would.
        services.AddKeyedSingleton<ISymbolMapper>(exchangeId, new FakeSymbolMapper());

        StreamServiceRegistration.AddStreams<FakeStreamOptions>(
            services,
            exchangeId,
            _ => new FakeStreamProtocol(),
            _ => BuildAllDecoders(),
            configure);

        return services.BuildServiceProvider();
    }

    private static StreamDecoderRegistry BuildAllDecoders()
    {
        var registry = new StreamDecoderRegistry();
        registry.Register(StreamKind.Ticker, _ => (object)SentinelTicker);
        registry.Register(StreamKind.Trade, _ => (object)SentinelTrade);
        registry.Register(StreamKind.OrderBook, _ => (object)SentinelOrderBook);
        registry.Register(StreamKind.Kline, _ => (object)SentinelCandlestick);
        return registry;
    }

    /// <summary>
    /// Polls <paramref name="condition"/> until true or 5 seconds elapse.
    /// Used to await async side-effects on the pump loop without a fixed sleep.
    /// </summary>
    private static async Task WaitAsync(Func<bool> condition, int timeoutMs = 5_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);
        condition().Should().BeTrue("condition should be met within the timeout");
    }
}

