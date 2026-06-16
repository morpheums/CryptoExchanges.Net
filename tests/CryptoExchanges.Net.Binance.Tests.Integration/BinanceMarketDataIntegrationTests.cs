using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Binance.Tests.Integration;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed via IAsyncLifetime")]
public class BinanceMarketDataIntegrationTests : IAsyncLifetime
{
    private BinanceExchangeClient _exchange = null!;
    private string? _skipReason;

    public async Task InitializeAsync()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://api.binance.com") };
        _exchange = new BinanceExchangeClient(http, "", null);

        // Verify connectivity before running tests
        try
        {
            var reachable = await _exchange.PingAsync().ConfigureAwait(false);
            if (!reachable)
                _skipReason = "Binance API is not reachable — skipping integration tests.";
        }
        catch
        {
            _skipReason = "Binance API is unreachable — skipping integration tests.";
        }
    }

    public Task DisposeAsync()
    {
        _exchange.Dispose();
        return Task.CompletedTask;
    }

    private void SkipIfUnreachable()
    {
        if (_skipReason is not null)
            throw new SkipException(_skipReason);
    }

    // ── Infrastructure ──

    [Fact]
    public async Task Ping_ShouldReturnTrue()
    {
        SkipIfUnreachable();
        var result = await _exchange.PingAsync();
        result.Should().BeTrue();
    }

    // ── Market Data ──

    [Fact]
    public async Task GetPrice_BTCUSDT_ShouldReturnPositivePrice()
    {
        SkipIfUnreachable();
        var btcusdt = new Symbol("BTC", "USDT");
        var price = await _exchange.MarketData.GetPriceAsync(btcusdt);
        price.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTickers_WithSymbol_ShouldReturnSingleTicker()
    {
        SkipIfUnreachable();
        var ethusdt = new Symbol("ETH", "USDT");
        var tickers = await _exchange.MarketData.GetTickersAsync(ethusdt);
        tickers.Should().HaveCount(1);

        var ticker = tickers[0];
        ticker.LastPrice.Should().BeGreaterThan(0);
        ticker.OpenPrice.Should().HaveValue();
        ticker.HighPrice.Should().HaveValue();
        ticker.LowPrice.Should().HaveValue();
        ticker.Volume.Should().HaveValue();
    }

    [Fact]
    public async Task GetTickers_All_ShouldReturnManySymbols()
    {
        SkipIfUnreachable();
        var tickers = await _exchange.MarketData.GetTickersAsync();
        tickers.Should().HaveCountGreaterThan(100);
    }

    [Fact]
    public async Task GetOrderBook_Top5_ShouldReturnBidsAndAsks()
    {
        SkipIfUnreachable();
        var btcusdt = new Symbol("BTC", "USDT");
        var orderBook = await _exchange.MarketData.GetOrderBookAsync(btcusdt, depth: 5);
        orderBook.Bids.Should().NotBeEmpty();
        orderBook.Asks.Should().NotBeEmpty();
        orderBook.Bids[0].Price.Should().BeGreaterThan(0);
        orderBook.LastUpdateId.Should().HaveValue();
    }

    [Fact]
    public async Task GetOrderBook_Depth100_ShouldReturnMoreLevels()
    {
        SkipIfUnreachable();
        var btcusdt = new Symbol("BTC", "USDT");
        var ob100 = await _exchange.MarketData.GetOrderBookAsync(btcusdt, depth: 100);
        var ob5 = await _exchange.MarketData.GetOrderBookAsync(btcusdt, depth: 5);
        ob100.Bids.Count.Should().BeGreaterThan(ob5.Bids.Count);
    }

    [Fact]
    public async Task GetCandlesticks_1h_ShouldReturnValidCandles()
    {
        SkipIfUnreachable();
        var btcusdt = new Symbol("BTC", "USDT");
        var candles = await _exchange.MarketData.GetCandlesticksAsync(
            btcusdt, KlineInterval.OneHour, limit: 3);

        candles.Should().HaveCount(3);
        foreach (var c in candles)
        {
            c.Open.Should().BeGreaterThan(0);
            c.High.Should().BeGreaterOrEqualTo(c.Low);
            c.Close.Should().BeGreaterThan(0);
            c.Volume.Should().BeGreaterThan(0);
        }

        // Verify OpenTime is in ascending order
        for (int i = 1; i < candles.Count; i++)
            candles[i].OpenTime.Should().BeAfter(candles[i - 1].OpenTime);
    }

    [Fact]
    public async Task GetCandlesticks_1d_ShouldWork()
    {
        SkipIfUnreachable();
        var btcusdt = new Symbol("BTC", "USDT");
        var candles = await _exchange.MarketData.GetCandlesticksAsync(
            btcusdt, KlineInterval.OneDay, limit: 3);

        candles.Should().HaveCount(3);
        foreach (var c in candles)
        {
            c.Open.Should().BeGreaterThan(0);
            c.Close.Should().BeGreaterThan(0);
            c.Interval.Should().Be(KlineInterval.OneDay);
            c.Symbol.Should().Be(btcusdt);
        }
    }

    [Fact]
    public async Task GetExchangeInfo_ShouldContainBTCUSDT()
    {
        SkipIfUnreachable();
        var info = await _exchange.MarketData.GetExchangeInfoAsync();
        info.Symbols.Should().HaveCountGreaterThan(100);

        var btcusdt = info.Symbols.FirstOrDefault(s => s.Symbol.ToString() == "BTCUSDT");
        btcusdt.Should().NotBeNull();
        btcusdt!.AllowedOrderTypes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRecentTrades_ShouldReturnRecentTrades()
    {
        SkipIfUnreachable();
        var btcusdt = new Symbol("BTC", "USDT");
        var trades = await _exchange.MarketData.GetRecentTradesAsync(btcusdt, limit: 5);

        trades.Should().NotBeEmpty();
        foreach (var t in trades)
        {
            t.Price.Should().BeGreaterThan(0);
            t.Quantity.Should().BeGreaterThan(0);
            t.Timestamp.Should().HaveValue();
            t.Timestamp!.Value.Should().BeAfter(DateTimeOffset.UtcNow.AddHours(-1));
        }
    }
}

/// <summary>
/// Custom skip exception for xunit when Binance is unreachable.
/// </summary>
internal sealed class SkipException : Xunit.Sdk.XunitException
{
    public SkipException(string message) : base(message) { }
}
