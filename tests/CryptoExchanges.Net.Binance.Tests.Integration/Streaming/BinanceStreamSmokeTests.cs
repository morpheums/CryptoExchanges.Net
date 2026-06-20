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

    /// <summary>
    /// Checks whether the WebSocket endpoint is reachable. Returns null when reachable,
    /// or a skip reason string when not.
    /// </summary>
    private static async Task<string?> CheckReachabilityAsync()
    {
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await ws.ConnectAsync(new Uri("wss://stream.binance.com:9443/stream"), cts.Token);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "ping", CancellationToken.None);
            return null;
        }
        catch
        {
            return "WebSocket endpoint unreachable — skipping integration smoke tests.";
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
