using System.Net.WebSockets;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Binance.Tests.Integration.Streaming;

/// <summary>
/// Live integration smoke test: connects to the real public WebSocket endpoint and
/// asserts that at least one mapped <see cref="Core.Models"/> update arrives for each
/// of the four stream kinds. Self-skips when the endpoint is unreachable.
/// </summary>
/// <remarks>
/// These tests require an active internet connection and a reachable WebSocket endpoint.
/// They are excluded from the standard unit-test run via the
/// <c>[Trait("Category", "Integration")]</c> attribute.
/// </remarks>
[Trait("Category", "Integration")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed inline in each test")]
public class BinanceStreamSmokeTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(20);

    // ≥17 liquid pairs: subscribing to all at once on one client is the unpaced burst that
    // pre-FEAT-008 tripped Binance's 5-msg/sec inbound cap (PolicyViolation → zero data).
    private static readonly Symbol[] MultiSymbolSet =
    [
        new(Asset.Btc, Asset.Usdt),  new(Asset.Eth, Asset.Usdt),  new(Asset.Bnb, Asset.Usdt),
        new(Asset.Sol, Asset.Usdt),  new(Asset.Xrp, Asset.Usdt),  new(Asset.Ada, Asset.Usdt),
        new(Asset.Doge, Asset.Usdt), new(Asset.Trx, Asset.Usdt),  new(Asset.Of("LINK"), Asset.Usdt),
        new(Asset.Of("AVAX"), Asset.Usdt), new(Asset.Of("DOT"), Asset.Usdt), new(Asset.Of("LTC"), Asset.Usdt),
        new(Asset.Btc, Asset.Usdc),  new(Asset.Eth, Asset.Usdc),  new(Asset.Sol, Asset.Usdc),
        new(Asset.Xrp, Asset.Usdc),  new(Asset.Bnb, Asset.Usdc),  new(Asset.Doge, Asset.Usdc),
    ];

    // Generous: ~18 throttled subscribes at 200 ms each ≈ 3.6 s before the last frame is even placed.
    private static readonly TimeSpan MultiSymbolReceiveTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Verifies the Binance combined-stream WebSocket endpoint is reachable. Returns <c>null</c>
    /// when reachable, or a skip-reason string when not. A successful TLS handshake proves the host
    /// is reachable; the actual decode/delivery is asserted by the tests themselves.
    /// </summary>
    private static async Task<string?> CheckReachabilityAsync()
    {
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await ws.ConnectAsync(new Uri("wss://stream.binance.com:9443/stream"), cts.Token)
                    .ConfigureAwait(false);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "probe", CancellationToken.None)
                    .ConfigureAwait(false);
            return null;
        }
        catch
        {
            return "Binance WebSocket endpoint unreachable — skipping integration smoke tests.";
        }
    }

    private static IStreamClient BuildClient()
    {
        var services = new ServiceCollection();
        services.AddBinanceExchange(o => { o.ApiKey = string.Empty; o.SecretKey = string.Empty; });
        services.AddBinanceStreams();
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        return factory.GetClient(ExchangeId.Binance);
    }

    [Fact]
    public async Task Ticker_LiveStream_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildClient();
        var received = new TaskCompletionSource<Ticker>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await client.SubscribeToTickerAsync(
            BtcUsdt,
            new StreamHandlers<Ticker>(t =>
            {
                received.TrySetResult(t);
                return ValueTask.CompletedTask;
            }),
            TestContext.Current.CancellationToken);

        await using (subscription)
        {
            var ticker = await received.Task.WaitAsync(ReceiveTimeout, TestContext.Current.CancellationToken);
            ticker.Should().NotBeNull();
            subscription.State.Should().Be(StreamConnectionState.Live);
        }
    }

    [Fact]
    public async Task Trade_LiveStream_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildClient();
        var received = new TaskCompletionSource<Trade>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await client.SubscribeToTradesAsync(
            BtcUsdt,
            new StreamHandlers<Trade>(t =>
            {
                received.TrySetResult(t);
                return ValueTask.CompletedTask;
            }),
            TestContext.Current.CancellationToken);

        await using (subscription)
        {
            var trade = await received.Task.WaitAsync(ReceiveTimeout, TestContext.Current.CancellationToken);
            trade.Should().NotBeNull();
            trade.Price.Should().BeGreaterThan(0);
            subscription.State.Should().Be(StreamConnectionState.Live);
        }
    }

    [Fact]
    public async Task OrderBook_LiveStream_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildClient();
        var received = new TaskCompletionSource<OrderBook>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await client.SubscribeToOrderBookAsync(
            BtcUsdt,
            depth: 5,
            new StreamHandlers<OrderBook>(ob =>
            {
                received.TrySetResult(ob);
                return ValueTask.CompletedTask;
            }),
            TestContext.Current.CancellationToken);

        await using (subscription)
        {
            var orderBook = await received.Task.WaitAsync(ReceiveTimeout, TestContext.Current.CancellationToken);
            orderBook.Should().NotBeNull();
            orderBook.Bids.Should().NotBeEmpty();
            orderBook.Asks.Should().NotBeEmpty();
            subscription.State.Should().Be(StreamConnectionState.Live);
        }
    }

    /// <summary>
    /// Regression test for the FEAT-008 multi-symbol burst bug: subscribing to many L2 order books
    /// at once on a single client previously fired an unpaced control-frame burst that Binance
    /// closed with PolicyViolation before any data arrived, then replayed the same burst on every
    /// reconnect (infinite loop, zero updates). With the TASK-071 throttle + TASK-072 batched replay
    /// in place, at least one book is delivered and at least one subscription reaches Live.
    /// </summary>
    [Fact]
    public async Task OrderBook_MultiSymbol_LiveStream_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildClient();
        var received = new TaskCompletionSource<OrderBook>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriptions = new List<IStreamSubscription>(MultiSymbolSet.Length);

        try
        {
            foreach (var symbol in MultiSymbolSet)
            {
                var subscription = await client.SubscribeToOrderBookAsync(
                    symbol,
                    depth: 20,
                    new StreamHandlers<OrderBook>(ob =>
                    {
                        received.TrySetResult(ob);
                        return ValueTask.CompletedTask;
                    }),
                    TestContext.Current.CancellationToken);
                subscriptions.Add(subscription);
            }

            var orderBook = await received.Task.WaitAsync(MultiSymbolReceiveTimeout, TestContext.Current.CancellationToken);
            orderBook.Should().NotBeNull();
            orderBook.Bids.Should().NotBeEmpty();
            orderBook.Asks.Should().NotBeEmpty();
            subscriptions.Should().Contain(s => s.State == StreamConnectionState.Live);
        }
        finally
        {
            foreach (var subscription in subscriptions)
                await subscription.DisposeAsync();
        }
    }

    [Fact]
    public async Task Kline_LiveStream_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildClient();
        var received = new TaskCompletionSource<Candlestick>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await client.SubscribeToKlinesAsync(
            BtcUsdt,
            KlineInterval.OneMinute,
            new StreamHandlers<Candlestick>(c =>
            {
                received.TrySetResult(c);
                return ValueTask.CompletedTask;
            }),
            TestContext.Current.CancellationToken);

        await using (subscription)
        {
            var candle = await received.Task.WaitAsync(ReceiveTimeout, TestContext.Current.CancellationToken);
            candle.Should().NotBeNull();
            candle.Open.Should().BeGreaterThan(0);
            subscription.State.Should().Be(StreamConnectionState.Live);
        }
    }
}
