using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using FluentAssertions;
using Xunit;

namespace CryptoExchanges.Net.Core.Tests.Unit;

public class CoreTests
{
    [Theory]
    [InlineData("BTC", "USDT")]
    [InlineData("ETH", "BTC")]
    [InlineData("SOL", "USDC")]
    [InlineData("DOGE", "USDT")]
    [InlineData("BNB", "BUSD")]
    [InlineData("XRP", "USD")]
    public void Symbol_ShouldExposeTypedLegs(string expectedBase, string expectedQuote)
    {
        var symbol = new Symbol(Asset.Of(expectedBase), Asset.Of(expectedQuote));

        symbol.Base.Ticker.Should().Be(expectedBase);
        symbol.Quote.Ticker.Should().Be(expectedQuote);
    }

    [Fact]
    public void Symbol_ToString_ShouldRenderHumanReadableForm()
    {
        var symbol = new Symbol(Asset.Btc, Asset.Usdt);
        symbol.ToString().Should().Be("BTC/USDT");
    }

    [Fact]
    public void PlaceOrderRequest_Validate_LimitBuy_ShouldPass()
    {
        var request = new PlaceOrderRequest
        {
            Symbol = new Symbol(Asset.Btc, Asset.Usdt),
            Side = Enums.OrderSide.Buy,
            Type = Enums.OrderType.Limit,
            Quantity = 0.1m,
            Price = 50000m
        };

        var act = () => request.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void PlaceOrderRequest_Validate_MarketSell_ShouldPass()
    {
        var request = new PlaceOrderRequest
        {
            Symbol = new Symbol(Asset.Eth, Asset.Usdt),
            Side = Enums.OrderSide.Sell,
            Type = Enums.OrderType.Market,
            Quantity = 1.5m
        };

        var act = () => request.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void PlaceOrderRequest_Validate_MissingQuantity_ShouldThrow()
    {
        var request = new PlaceOrderRequest
        {
            Symbol = new Symbol(Asset.Btc, Asset.Usdt),
            Side = Enums.OrderSide.Buy,
            Type = Enums.OrderType.Limit,
            Price = 50000m
            // No Quantity set
        };

        var act = () => request.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Quantity*");
    }

    [Fact]
    public void PlaceOrderRequest_Validate_LimitMissingPrice_ShouldThrow()
    {
        var request = new PlaceOrderRequest
        {
            Symbol = new Symbol(Asset.Btc, Asset.Usdt),
            Side = Enums.OrderSide.Buy,
            Type = Enums.OrderType.Limit,
            Quantity = 0.1m
            // No Price set
        };

        var act = () => request.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Price*");
    }

    [Fact]
    public void PlaceOrderRequest_Validate_StopLoss_RequiresStopPrice()
    {
        var request = new PlaceOrderRequest
        {
            Symbol = new Symbol(Asset.Btc, Asset.Usdt),
            Side = Enums.OrderSide.Sell,
            Type = Enums.OrderType.StopLoss,
            Quantity = 0.1m
            // No StopPrice
        };

        var act = () => request.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*StopPrice*");
    }

    [Fact]
    public void PlaceOrderRequest_Validate_MarketWithQuoteQuantity_ShouldPass()
    {
        var request = new PlaceOrderRequest
        {
            Symbol = new Symbol(Asset.Btc, Asset.Usdt),
            Side = Enums.OrderSide.Buy,
            Type = Enums.OrderType.Market,
            QuoteOrderQuantity = 100m // Buy $100 worth of BTC
        };

        var act = () => request.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void AssetBalance_Total_ShouldBeFreePlusLocked()
    {
        var balance = new AssetBalance("BTC", 1.5m, 0.3m);

        balance.Total.Should().Be(1.8m);
    }

    [Fact]
    public void AssetBalance_Total_ZeroWhenEmpty()
    {
        var balance = new AssetBalance("USDT", 0m, 0m);

        balance.Total.Should().Be(0m);
    }

    [Fact]
    public void AssetBalance_Total_LockedOnly()
    {
        var balance = new AssetBalance("ETH", 0m, 2.0m);

        balance.Total.Should().Be(2.0m);
    }

    [Fact]
    public void OrderBook_ShouldStoreBidsAndAsks()
    {
        var symbol = new Symbol(Asset.Btc, Asset.Usdt);
        var bids = new List<OrderBookEntry>
        {
            new(50000m, 1.0m),
            new(49990m, 2.5m)
        };
        var asks = new List<OrderBookEntry>
        {
            new(50010m, 0.5m),
            new(50020m, 3.0m)
        };

        var orderBook = new OrderBook(symbol, bids, asks, LastUpdateId: 12345);

        orderBook.Symbol.Should().Be(symbol);
        orderBook.Bids.Should().HaveCount(2);
        orderBook.Asks.Should().HaveCount(2);
        orderBook.LastUpdateId.Should().Be(12345);
        orderBook.Bids[0].Price.Should().Be(50000m);
        orderBook.Asks[0].Price.Should().Be(50010m);
    }

    [Fact]
    public void Order_CumulativeQuoteQuantity_ShouldBeSetViaInit()
    {
        var order = new Order(
            new Symbol(Asset.Btc, Asset.Usdt),
            "12345",
            Price: 50000m,
            OriginalQuantity: 1.0m,
            ExecutedQuantity: 0.5m)
        {
            CumulativeQuoteQuantity = 25000m
        };

        order.CumulativeQuoteQuantity.Should().Be(25000m);
    }

    [Fact]
    public void Ticker_Creation_ShouldStoreAllFields()
    {
        var ticker = new Ticker(
            new Symbol(Asset.Btc, Asset.Usdt),
            50000m,
            49000m,
            51000m,
            48500m,
            1000m,
            50000000m,
            1000m,
            2.04m,
            DateTimeOffset.UtcNow);

        ticker.Symbol.ToString().Should().Be("BTC/USDT");
        ticker.LastPrice.Should().Be(50000m);
        ticker.OpenPrice.Should().Be(49000m);
        ticker.HighPrice.Should().Be(51000m);
        ticker.LowPrice.Should().Be(48500m);
        ticker.Volume.Should().Be(1000m);
        ticker.QuoteVolume.Should().Be(50000000m);
        ticker.PriceChangePercent.Should().Be(2.04m);
    }

    [Fact]
    public void Candlestick_Creation_ShouldStoreFields()
    {
        var now = DateTimeOffset.UtcNow;
        var candle = new Candlestick(
            now,
            now.AddHours(1),
            50000m, 51000m, 49500m, 50500m,
            100m, 5050000m, 500,
            Enums.KlineInterval.OneHour,
            new Symbol(Asset.Btc, Asset.Usdt));

        candle.OpenTime.Should().Be(now);
        candle.Open.Should().Be(50000m);
        candle.High.Should().Be(51000m);
        candle.Low.Should().Be(49500m);
        candle.Close.Should().Be(50500m);
        candle.Volume.Should().Be(100m);
        candle.Interval.Should().Be(Enums.KlineInterval.OneHour);
    }

    // ── Additional tests ──

    [Fact]
    public void Symbol_ToString_ShouldRecombine()
    {
        var symbol = new Symbol(Asset.Xrp, Asset.Usdt);
        symbol.ToString().Should().Be("XRP/USDT");
    }

    [Fact]
    public void PlaceOrderRequest_Validate_MarketWithoutQty_ShouldThrow()
    {
        var request = new PlaceOrderRequest
        {
            Symbol = new Symbol(Asset.Btc, Asset.Usdt),
            Side = Enums.OrderSide.Buy,
            Type = Enums.OrderType.Market
            // No Quantity, no QuoteOrderQuantity
        };

        var act = () => request.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Quantity*");
    }
}
