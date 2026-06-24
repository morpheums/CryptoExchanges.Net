using System.Collections.Concurrent;
using System.Net.WebSockets;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Bitget;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Bitget.Tests.Integration.Streaming;

/// <summary>
/// Live integration smoke tests for the Bitget public WebSocket streaming client.
/// Self-skip when <c>wss://ws.bitget.com/v2/ws/public</c> is unreachable (offline → skip, not fail).
/// All tests carry <c>[Trait("Category", "Integration")]</c> and are excluded from the default CI
/// gate (<c>dotnet test --filter 'Category!=Integration'</c>).
/// </summary>
[Trait("Category", "Integration")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed inline in each test")]
public class BitgetStreamingSmokeTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(20);

    // ≥8 symbols: proves paced subscribe replay (K2/K3) does NOT trigger a rate-limit
    // reconnect loop on Bitget (200 ms MinOutboundInterval).
    private static readonly Symbol[] MultiSymbolSet =
    [
        new(Asset.Btc, Asset.Usdt),      new(Asset.Eth, Asset.Usdt),
        new(Asset.Sol, Asset.Usdt),      new(Asset.Xrp, Asset.Usdt),
        new(Asset.Ada, Asset.Usdt),      new(Asset.Doge, Asset.Usdt),
        new(Asset.Of("AVAX"), Asset.Usdt), new(Asset.Of("LTC"), Asset.Usdt),
    ];

    private static readonly TimeSpan MultiSymbolReceiveTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Verifies the Bitget public WebSocket gateway is reachable. Returns <c>null</c> when
    /// reachable, or a skip-reason string when not.
    /// </summary>
    private static async Task<string?> CheckReachabilityAsync()
    {
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await ws.ConnectAsync(new Uri("wss://ws.bitget.com/v2/ws/public"), cts.Token)
                    .ConfigureAwait(false);
            // Bound by probe's CancellationToken — CloseAsync can hang indefinitely under
            // CancellationToken.None if the peer never replies.
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "probe", cts.Token)
                    .ConfigureAwait(false);
            return null;
        }
        catch
        {
            return "Bitget WebSocket endpoint unreachable — skipping integration streaming smoke tests.";
        }
    }

    /// <summary>
    /// Builds a scoped DI service provider wiring <c>AddBitgetExchange</c> and
    /// <c>AddBitgetStreams</c> with empty credentials (public streams require none).
    /// </summary>
    private static IStreamClient BuildStreamClient()
    {
        var services = new ServiceCollection();
        services.AddBitgetExchange(o =>
        {
            o.ApiKey = string.Empty;
            o.SecretKey = string.Empty;
            o.Passphrase = string.Empty;
        });
        services.AddBitgetStreams();
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        return factory.GetClient(ExchangeId.Bitget);
    }

    [Fact]
    public async Task Ticker_LiveStream_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildStreamClient();
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

        await using var client = BuildStreamClient();
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
    public async Task Kline_LiveStream_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildStreamClient();
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

    /// <summary>
    /// Regression gate for the FEAT-008 multi-symbol reconnect-loop bug. Subscribes to ≥8 Bitget
    /// spot symbols; the paced subscribe (200 ms MinOutboundInterval) must deliver ≥1 order-book
    /// update within 30 s with NO reconnect loop.
    /// </summary>
    [Fact]
    public async Task OrderBook_MultiSymbol_DeliversAtLeastOneUpdate()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        await using var client = BuildStreamClient();

        // Track per-symbol delivery: a replay/rate-limit regression on later symbols would still
        // let the first through, so assert a MAJORITY of the subscribed set.
        var delivered = new ConcurrentDictionary<Symbol, byte>();
        var threshold = (MultiSymbolSet.Length + 1) / 2;
        var enoughDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstPopulated = new TaskCompletionSource<OrderBook>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriptions = new List<IStreamSubscription>(MultiSymbolSet.Length);

        try
        {
            foreach (var symbol in MultiSymbolSet)
            {
                var subscription = await client.SubscribeToOrderBookAsync(
                    symbol,
                    depth: 5,
                    new StreamHandlers<OrderBook>(ob =>
                    {
                        if (ob.Bids.Count + ob.Asks.Count > 0)
                            firstPopulated.TrySetResult(ob);
                        delivered.TryAdd(ob.Symbol, 0);
                        if (delivered.Count >= threshold)
                            enoughDelivered.TrySetResult();
                        return ValueTask.CompletedTask;
                    }),
                    TestContext.Current.CancellationToken);
                subscriptions.Add(subscription);
            }

            await enoughDelivered.Task.WaitAsync(MultiSymbolReceiveTimeout, TestContext.Current.CancellationToken);

            delivered.Count.Should().BeGreaterThanOrEqualTo(threshold,
                "a majority of subscribed symbols must stream — a replay/rate-limit regression would starve later symbols in the batch");
            firstPopulated.Task.IsCompletedSuccessfully.Should().BeTrue(
                "Bitget books5 sends a snapshot on subscribe; at least one must arrive with populated bids/asks");
            var orderBook = await firstPopulated.Task;
            (orderBook.Bids.Count + orderBook.Asks.Count).Should().BeGreaterThan(0);
            subscriptions.Should().OnlyContain(s => s.State == StreamConnectionState.Live);
        }
        finally
        {
            foreach (var subscription in subscriptions)
                await subscription.DisposeAsync();
        }
    }
}
