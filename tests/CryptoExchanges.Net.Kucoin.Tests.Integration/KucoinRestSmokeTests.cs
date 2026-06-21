using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Kucoin;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Kucoin.Tests.Integration;

/// <summary>
/// Live integration smoke tests for the KuCoin REST client. Tests self-skip when
/// <c>KUCOIN_API_KEY</c> is not set or when the REST endpoint is unreachable, so the
/// default non-integration gate (<c>dotnet test --filter 'Category!=Integration'</c>)
/// is unaffected.
/// </summary>
[Trait("Category", "Integration")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed in DisposeAsync")]
public class KucoinRestSmokeTests : IAsyncLifetime
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private KucoinExchangeClient _client = null!;
    private string? _skipReason;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var apiKey = Environment.GetEnvironmentVariable("KUCOIN_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _skipReason = "KUCOIN_API_KEY not set — skipping KuCoin integration smoke tests.";
            // Still need a valid client instance for DisposeAsync, so use empty credentials.
            _client = KucoinExchangeClient.Create(new KucoinOptions());
            return;
        }

        _client = KucoinExchangeClient.CreateFromEnvironment();

        try
        {
            var reachable = await _client.PingAsync().ConfigureAwait(false);
            if (!reachable)
                _skipReason = "KuCoin REST endpoint unreachable — skipping integration smoke tests.";
        }
        catch
        {
            _skipReason = "KuCoin REST endpoint unreachable — skipping integration smoke tests.";
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    // Self-skip guard: reports the test as genuinely skipped (not failed) when env vars / network absent.
    private void SkipIfUnavailable()
        => Assert.SkipWhen(_skipReason is not null, _skipReason ?? string.Empty);

    // ── Public REST ──

    [Fact]
    public async Task GetServerTime_ReturnsTimestamp()
    {
        SkipIfUnavailable();
        // SyncServerTimeAsync exercises the /api/v1/timestamp public endpoint without requiring credentials.
        await _client.SyncServerTimeAsync(TestContext.Current.CancellationToken);
        // If we reach here without throwing, the endpoint returned a positive timestamp.
    }

    [Fact]
    public async Task GetTicker_BtcUsdt_ReturnsTicker()
    {
        SkipIfUnavailable();
        var tickers = await _client.MarketData.GetTickersAsync(BtcUsdt, TestContext.Current.CancellationToken);
        tickers.Should().HaveCount(1);

        var ticker = tickers[0];
        ticker.LastPrice.Should().BeGreaterThan(0);
        ticker.Symbol.Should().Be(BtcUsdt);
    }

    [Fact]
    public async Task GetOrderBook_BtcUsdt_ReturnsOrderBook()
    {
        SkipIfUnavailable();
        var orderBook = await _client.MarketData.GetOrderBookAsync(BtcUsdt, depth: 20, ct: TestContext.Current.CancellationToken);
        orderBook.Bids.Should().NotBeEmpty();
        orderBook.Asks.Should().NotBeEmpty();
        orderBook.Bids[0].Price.Should().BeGreaterThan(0);
        orderBook.Asks[0].Price.Should().BeGreaterThan(0);
    }

    // ── Signed REST (requires credentials) ──

    [Fact]
    public async Task GetBalances_WithCredentials_ReturnsBalances()
    {
        SkipIfUnavailable();
        var balances = await _client.Account.GetBalancesAsync(TestContext.Current.CancellationToken);
        // A valid signed request must return a non-null list (empty is acceptable for a fresh account).
        balances.Should().NotBeNull();
    }

    [Fact]
    public async Task PlaceAndCancelOrder_LimitBuy_Roundtrip()
    {
        SkipIfUnavailable();

        // Use a price far below market to avoid accidental fills.
        // KuCoin minimum order size for BTC-USDT spot is 0.00001 BTC.
        var request = PlaceOrderRequest.Create(
            symbol: BtcUsdt,
            side: OrderSide.Buy,
            type: OrderType.Limit,
            quantity: 0.00001m,
            price: 1m,         // $1 — far below market; will never fill
            timeInForce: TimeInForce.Gtc);

        var placed = await _client.Trading.PlaceOrderAsync(request, TestContext.Current.CancellationToken);
        placed.Should().NotBeNull();
        placed.OrderId.Should().NotBeNullOrWhiteSpace();
        placed.Status.Should().NotBe(OrderStatus.Filled);

        var cancelled = await _client.Trading.CancelOrderAsync(BtcUsdt, placed.OrderId, TestContext.Current.CancellationToken);
        cancelled.Should().NotBeNull();
        cancelled.OrderId.Should().Be(placed.OrderId);
    }
}
