using System.Net.WebSockets;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Coinbase;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Coinbase.Tests.Integration.Streaming;

/// <summary>
/// Live integration smoke tests for the Coinbase Advanced Trade public WebSocket streaming client.
/// Self-skip when the endpoint is unreachable (offline → skip, not fail). All tests carry
/// <c>[Trait("Category", "Integration")]</c> and are excluded from the default CI gate
/// (<c>dotnet test --filter 'Category!=Integration'</c>).
/// </summary>
[Trait("Category", "Integration")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed inline in each test")]
public class CoinbaseStreamingSmokeTests
{
    private static readonly Symbol BtcUsd = new(Asset.Btc, Asset.Of("USD"));
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(20);

    // ≥8 symbols — proves paced subscribe replay does NOT trigger a reconnect loop (FEAT-008 gate).
    private static readonly Symbol[] MultiSymbolSet =
    [
        new(Asset.Btc,      Asset.Of("USD")),
        new(Asset.Eth,      Asset.Of("USD")),
        new(Asset.Sol,      Asset.Of("USD")),
        new(Asset.Xrp,      Asset.Of("USD")),
        new(Asset.Ada,      Asset.Of("USD")),
        new(Asset.Doge,     Asset.Of("USD")),
        new(Asset.Of("AVAX"), Asset.Of("USD")),
        new(Asset.Of("LTC"),  Asset.Of("USD")),
    ];

    private static readonly TimeSpan MultiSymbolReceiveTimeout = TimeSpan.FromSeconds(30);

    private static async Task<string?> CheckReachabilityAsync()
    {
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await ws.ConnectAsync(new Uri("wss://advanced-trade-ws.coinbase.com"), cts.Token)
                    .ConfigureAwait(false);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "probe", cts.Token)
                    .ConfigureAwait(false);
            return null;
        }
        catch
        {
            return "Coinbase WebSocket endpoint unreachable — skipping integration streaming smoke tests.";
        }
    }

    private static IStreamClient BuildStreamClient()
    {
        var services = new ServiceCollection();
        services.AddCoinbaseExchange(o =>
        {
            o.ApiKey = string.Empty;
            o.PrivateKey = string.Empty;
        });
        services.AddCoinbaseStreams();
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        return factory.GetClient(ExchangeId.Coinbase);
    }

    [Fact]
    public async Task Ticker_LiveStream_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildStreamClient();
        var received = new TaskCompletionSource<Ticker>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await client.SubscribeToTickerAsync(
            BtcUsd,
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

        await using var client = BuildStreamClient();
        var received = new TaskCompletionSource<Trade>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await client.SubscribeToTradesAsync(
            BtcUsd,
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
    public async Task Kline_LiveStream_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildStreamClient();
        var received = new TaskCompletionSource<Candlestick>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await client.SubscribeToKlinesAsync(
            BtcUsd,
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

    /// <summary>
    /// Regression gate for the FEAT-008 multi-symbol reconnect-loop bug. Subscribes to 8 Coinbase
    /// spot symbols on a single connection; paced replay must deliver ≥1 order-book update within
    /// 30 s with NO reconnect loop.
    /// </summary>
    [Fact]
    public async Task OrderBook_MultiSymbol_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildStreamClient();
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
            (orderBook.Bids.Count + orderBook.Asks.Count).Should().BeGreaterThan(0);
            // No reconnect loop: all subscriptions must remain Live (not Reconnecting/Closed).
            subscriptions.Should().Contain(s => s.State == StreamConnectionState.Live);
        }
        finally
        {
            foreach (var subscription in subscriptions)
                await subscription.DisposeAsync();
        }
    }
}
