using System.Net.Sockets;
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

    // The multi-symbol burst that pre-fix tripped Binance's 5-msg/sec cap.
    private static readonly Symbol[] MultiSymbolSet =
    [
        new(Asset.Btc, Asset.Usdt),  new(Asset.Eth, Asset.Usdt),  new(Asset.Bnb, Asset.Usdt),
        new(Asset.Sol, Asset.Usdt),  new(Asset.Xrp, Asset.Usdt),  new(Asset.Ada, Asset.Usdt),
        new(Asset.Doge, Asset.Usdt), new(Asset.Trx, Asset.Usdt),  new(Asset.Of("LINK"), Asset.Usdt),
        new(Asset.Of("AVAX"), Asset.Usdt), new(Asset.Of("DOT"), Asset.Usdt), new(Asset.Of("LTC"), Asset.Usdt),
        new(Asset.Btc, Asset.Usdc),  new(Asset.Eth, Asset.Usdc),  new(Asset.Sol, Asset.Usdc),
        new(Asset.Xrp, Asset.Usdc),  new(Asset.Bnb, Asset.Usdc),  new(Asset.Doge, Asset.Usdc),
    ];

    private static readonly TimeSpan MultiSymbolReceiveTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Returns <c>null</c> when the WebSocket endpoint is reachable, else a skip-reason string.
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
        // Skip ONLY on genuine connect failure (no socket / timeout); auth/TLS/protocol errors propagate.
        catch (Exception ex) when (ex is WebSocketException or SocketException or OperationCanceledException)
        {
            return "Binance WebSocket endpoint unreachable (connectivity) — skipping integration smoke tests.";
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
    /// Regression test for the multi-symbol burst bug: many L2 subscriptions on one client must
    /// deliver at least one book (pre-fix the burst was venue-closed before any data arrived).
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
