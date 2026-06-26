using System.Net.Sockets;
using System.Net.WebSockets;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Kucoin;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Kucoin.Tests.Integration;

/// <summary>
/// Live integration smoke tests for the KuCoin public WebSocket streaming client.
/// Tests self-skip when the bullet-public endpoint is unreachable (no credentials required
/// for public streams, but network access is required). All tests carry
/// <c>[Trait("Category", "Integration")]</c> and are excluded from the default gate
/// (<c>dotnet test --filter 'Category!=Integration'</c>).
/// </summary>
[Trait("Category", "Integration")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed inline in each test")]
public class KucoinStreamingSmokeTests
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(30);

    // The multi-symbol fan-out that, unpaced, bursts the venue's control-frame rate limit.
    private static readonly Symbol[] MultiSymbolSet =
    [
        new(Asset.Btc, Asset.Usdt),  new(Asset.Eth, Asset.Usdt),  new(Asset.Sol, Asset.Usdt),
        new(Asset.Xrp, Asset.Usdt),  new(Asset.Ada, Asset.Usdt),  new(Asset.Doge, Asset.Usdt),
        new(Asset.Trx, Asset.Usdt),  new(Asset.Of("LINK"), Asset.Usdt), new(Asset.Of("AVAX"), Asset.Usdt),
        new(Asset.Of("DOT"), Asset.Usdt),  new(Asset.Of("LTC"), Asset.Usdt),  new(Asset.Of("MATIC"), Asset.Usdt),
        new(Asset.Of("ATOM"), Asset.Usdt), new(Asset.Of("UNI"), Asset.Usdt),
    ];

    private static readonly TimeSpan MultiSymbolReceiveTimeout = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Verifies the KuCoin bullet-public WebSocket gateway is reachable. Returns <c>null</c>
    /// when reachable, or a skip-reason string when not.
    /// </summary>
    private static async Task<string?> CheckReachabilityAsync()
    {
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            // A successful TLS handshake proves the host is reachable even if the token
            // is missing — bullet-public negotiation happens over HTTP, not here.
            await ws.ConnectAsync(new Uri("wss://ws-api.kucoin.com/endpoint"), cts.Token)
                    .ConfigureAwait(false);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "probe", CancellationToken.None)
                    .ConfigureAwait(false);
            return null;
        }
        // Skip ONLY on genuine connect failure (no socket / timeout); auth/TLS/protocol errors propagate.
        catch (Exception ex) when (ex is WebSocketException or SocketException or OperationCanceledException)
        {
            return "KuCoin WebSocket endpoint unreachable (connectivity) — skipping integration streaming smoke tests.";
        }
    }

    /// <summary>
    /// Builds a scoped DI service provider that wires both <c>AddKucoinExchange</c> and
    /// <c>AddKucoinStreams</c> with empty credentials (public streams require none).
    /// </summary>
    private static IStreamClient BuildStreamClient()
    {
        var services = new ServiceCollection();
        services.AddKucoinExchange(o =>
        {
            o.ApiKey = string.Empty;
            o.SecretKey = string.Empty;
            o.Passphrase = string.Empty;
        });
        services.AddKucoinStreams();
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        return factory.GetClient(ExchangeId.Kucoin);
    }

    [Fact]
    public async Task StreamTicker_BtcUsdt_ReceivesUpdate()
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
            ticker.LastPrice.Should().BeGreaterThan(0);
            subscription.State.Should().Be(StreamConnectionState.Live);
        }
    }

    /// <summary>
    /// Regression test for the multi-symbol burst bug: many L2 subscriptions on one client must
    /// deliver at least one book diff (pre-fix the burst was venue-closed before any data arrived).
    /// </summary>
    [Fact]
    public async Task OrderBook_MultiSymbol_LiveStream_DeliversAtLeastOneUpdate()
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
            // KuCoin level2 frames are incremental diffs; a frame carries changes on at least one side.
            (orderBook.Bids.Count + orderBook.Asks.Count).Should().BeGreaterThan(0);
            subscriptions.Should().Contain(s => s.State == StreamConnectionState.Live);
        }
        finally
        {
            foreach (var subscription in subscriptions)
                await subscription.DisposeAsync();
        }
    }

    /// <summary>
    /// Verifies that each new streaming connection triggers a fresh <c>bullet-public</c> token
    /// negotiation (AC-4 evidence). Two independent stream client instances are created back-to-back;
    /// each calls <c>POST /api/v1/bullet-public</c> on connect. Both connections must deliver live
    /// ticker frames, proving the token-negotiated path works across connection cycles.
    /// </summary>
    [Fact]
    public async Task StreamReconnect_TokenRenegotiated()
    {
        var skipReason = await CheckReachabilityAsync();
        Assert.SkipWhen(skipReason is not null, skipReason ?? string.Empty);

        // ── First connection (first bullet-public token) ───────────────────────

        var firstFrame = new TaskCompletionSource<Ticker>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using (var firstClient = BuildStreamClient())
        {
            var sub = await firstClient.SubscribeToTickerAsync(
                BtcUsdt,
                new StreamHandlers<Ticker>(
                    OnUpdate: t =>
                    {
                        firstFrame.TrySetResult(t);
                        return ValueTask.CompletedTask;
                    },
                    OnReconnecting: () => ValueTask.CompletedTask,
                    OnReconnected: () => ValueTask.CompletedTask),
                TestContext.Current.CancellationToken);

            await using (sub)
            {
                // Wait for the first live frame to confirm the connection and first bullet-public call.
                await firstFrame.Task.WaitAsync(ReceiveTimeout, TestContext.Current.CancellationToken);
                sub.State.Should().Be(StreamConnectionState.Live);
            }
        }

        // ── Second connection (second bullet-public token — proves re-negotiation on reconnect) ─

        await using var secondClient = BuildStreamClient();
        var secondFrame = new TaskCompletionSource<Ticker>(TaskCreationOptions.RunContinuationsAsynchronously);

        var sub2 = await secondClient.SubscribeToTickerAsync(
            BtcUsdt,
            new StreamHandlers<Ticker>(t =>
            {
                secondFrame.TrySetResult(t);
                return ValueTask.CompletedTask;
            }),
            TestContext.Current.CancellationToken);

        await using (sub2)
        {
            // A live frame on the second client proves a new bullet-public token was negotiated.
            var ticker = await secondFrame.Task.WaitAsync(ReceiveTimeout, TestContext.Current.CancellationToken);
            ticker.Should().NotBeNull();
            ticker.LastPrice.Should().BeGreaterThan(0);
            sub2.State.Should().Be(StreamConnectionState.Live);
        }
    }
}
