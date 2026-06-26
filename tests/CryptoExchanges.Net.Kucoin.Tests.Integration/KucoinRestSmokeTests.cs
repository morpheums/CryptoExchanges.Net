using System.Net.Http;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Kucoin;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Kucoin.Tests.Integration;

/// <summary>
/// Live integration smoke tests for the KuCoin REST client. Public market-data tests self-skip only
/// when the REST endpoint is unreachable; credentialed tests additionally self-skip when
/// <c>KUCOIN_API_KEY</c> is absent, so the default non-integration gate
/// (<c>dotnet test --filter 'Category!=Integration'</c>) is unaffected.
/// </summary>
[Trait("Category", "Integration")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed in DisposeAsync")]
public class KucoinRestSmokeTests : IAsyncLifetime
{
    private static readonly Symbol BtcUsdt = new(Asset.Btc, Asset.Usdt);

    private KucoinExchangeClient _client = null!;
    private string? _skipReason;
    private bool _hasCredentials;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // KuCoin signs with key + secret + passphrase; partial creds → public-only so credentialed
        // tests skip rather than running through a broken CreateFromEnvironment.
        _hasCredentials =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUCOIN_API_KEY"))
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUCOIN_SECRET_KEY"))
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUCOIN_PASSPHRASE"));

        _client = _hasCredentials
            ? KucoinExchangeClient.CreateFromEnvironment()
            : KucoinExchangeClient.Create(new KucoinOptions());

        // Skip ONLY when no HTTP response was received (null StatusCode — DNS/refused/socket) or timeout.
        // A real HTTP error response (non-null StatusCode) and any ExchangeException propagate and fail.
        try
        {
            var reachable = await _client.PingAsync().ConfigureAwait(false);
            if (!reachable)
                _skipReason = "KuCoin REST endpoint unreachable (connectivity) — skipping integration smoke tests.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            _skipReason = "KuCoin REST endpoint unreachable (connectivity) — skipping integration smoke tests.";
        }
        catch (OperationCanceledException)
        {
            _skipReason = "KuCoin REST endpoint unreachable (timeout) — skipping integration smoke tests.";
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    // Self-skip guard: reports the test as genuinely skipped (not failed) when the endpoint is unreachable.
    private void SkipIfUnavailable()
        => Assert.SkipWhen(_skipReason is not null, _skipReason ?? string.Empty);

    private void SkipIfNoCredentials()
    {
        SkipIfUnavailable();
        Assert.SkipWhen(!_hasCredentials, "KuCoin credentials (KUCOIN_API_KEY/KUCOIN_SECRET_KEY/KUCOIN_PASSPHRASE) not fully set — skipping credential-required smoke test.");
    }

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
        SkipIfNoCredentials();
        var balances = await _client.Account.GetBalancesAsync(TestContext.Current.CancellationToken);
        // A valid signed request must return a non-null list (empty is acceptable for a fresh account).
        balances.Should().NotBeNull();
    }

    [Fact]
    public async Task PlaceAndCancelOrder_LimitBuy_Roundtrip()
    {
        SkipIfNoCredentials();

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
