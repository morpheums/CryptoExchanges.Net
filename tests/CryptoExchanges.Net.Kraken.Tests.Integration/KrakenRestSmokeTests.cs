using System.Net.Http;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Kraken;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Kraken.Tests.Integration;

/// <summary>
/// Live integration smoke tests for the Kraken REST client. Tests self-skip when the REST
/// endpoint is unreachable. Credential-gated tests additionally skip when
/// <c>KRAKEN_API_KEY</c> is absent. All tests carry <c>[Trait("Category", "Integration")]</c>
/// and are excluded from the default CI gate (<c>dotnet test --filter 'Category!=Integration'</c>).
/// </summary>
[Trait("Category", "Integration")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed in DisposeAsync")]
public class KrakenRestSmokeTests : IAsyncLifetime
{
    private static readonly Symbol BtcUsd = new(Asset.Btc, Asset.Of("USD"));

    private KrakenExchangeClient _client = null!;
    private string? _skipReason;
    private bool _hasCredentials;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var apiKey = Environment.GetEnvironmentVariable("KRAKEN_API_KEY");
        _hasCredentials = !string.IsNullOrEmpty(apiKey);

        _client = _hasCredentials
            ? KrakenExchangeClient.CreateFromEnvironment()
            : KrakenExchangeClient.Create(new KrakenOptions());

        // Skip ONLY on genuine connectivity failure (no HTTP response / timeout). Real HTTP, auth
        // (e.g. 401 → ExchangeException) and protocol errors propagate and fail the run.
        try
        {
            var reachable = await _client.PingAsync().ConfigureAwait(false);
            if (!reachable)
                _skipReason = "Kraken REST endpoint unreachable (connectivity) — skipping integration smoke tests.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            _skipReason = "Kraken REST endpoint unreachable (connectivity) — skipping integration smoke tests.";
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private void SkipIfUnavailable()
        => Assert.SkipWhen(_skipReason is not null, _skipReason ?? string.Empty);

    private void SkipIfNoCredentials()
    {
        SkipIfUnavailable();
        Assert.SkipWhen(!_hasCredentials, "KRAKEN_API_KEY not set — skipping credential-required smoke test.");
    }

    [Fact]
    public async Task GetExchangeInfoAsync_ReturnsSymbols()
    {
        SkipIfUnavailable();
        var info = await _client.MarketData.GetExchangeInfoAsync(TestContext.Current.CancellationToken);
        info.Should().NotBeNull();
        info.Symbols.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTickerAsync_BtcUsd_ReturnsTicker()
    {
        SkipIfUnavailable();
        var tickers = await _client.MarketData.GetTickersAsync(BtcUsd, TestContext.Current.CancellationToken);
        tickers.Should().HaveCount(1);

        var ticker = tickers[0];
        ticker.LastPrice.Should().BeGreaterThan(0);
        ticker.Symbol.Should().Be(BtcUsd);
    }

    [Fact]
    public async Task GetOrderBookAsync_BtcUsd_ReturnsBidsAndAsks()
    {
        SkipIfUnavailable();
        var orderBook = await _client.MarketData.GetOrderBookAsync(BtcUsd, depth: 10, ct: TestContext.Current.CancellationToken);
        orderBook.Bids.Count.Should().BeGreaterThanOrEqualTo(1);
        orderBook.Asks.Count.Should().BeGreaterThanOrEqualTo(1);
        orderBook.Bids[0].Price.Should().BeGreaterThan(0);
        orderBook.Asks[0].Price.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetBalancesAsync_WithCredentials_ReturnsBalances()
    {
        SkipIfNoCredentials();
        var balances = await _client.Account.GetBalancesAsync(TestContext.Current.CancellationToken);
        balances.Should().NotBeNull();
    }

    [Fact]
    public async Task PlaceAndCancelOrder_LimitBuy_Roundtrip()
    {
        SkipIfNoCredentials();

        // Price far below market ($1) — will never fill.
        var request = PlaceOrderRequest.Create(
            symbol: BtcUsd,
            side: OrderSide.Buy,
            type: OrderType.Limit,
            quantity: 0.0001m,
            price: 1m,
            timeInForce: TimeInForce.Gtc);

        var placed = await _client.Trading.PlaceOrderAsync(request, TestContext.Current.CancellationToken);
        placed.Should().NotBeNull();
        placed.OrderId.Should().NotBeNullOrWhiteSpace();
        placed.Status.Should().NotBe(OrderStatus.Filled);

        var cancelled = await _client.Trading.CancelOrderAsync(BtcUsd, placed.OrderId, TestContext.Current.CancellationToken);
        cancelled.Should().NotBeNull();
        cancelled.OrderId.Should().Be(placed.OrderId);
    }
}
