using Xunit;
using FluentAssertions;
using NSubstitute;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Mcp.Tools;

namespace CryptoExchanges.Net.Mcp.Tests.Unit;

public class MarketDataToolsTests
{
    private static IExchangeClientFactory FactoryReturning(IExchangeClient client, ExchangeId id = ExchangeId.Binance)
    {
        var factory = Substitute.For<IExchangeClientFactory>();
        factory.GetClient(id).Returns(client);
        factory.TryGet(id, out Arg.Any<IExchangeClient?>())
            .Returns(ci => { ci[1] = client; return true; });
        return factory;
    }

    [Fact]
    public async Task GetPrice_RoutesToExchange_AndReturnsPrice()
    {
        var client = Substitute.For<IExchangeClient>();
        client.MarketData.GetPriceAsync(Arg.Any<Symbol>(), Arg.Any<CancellationToken>()).Returns(42000m);
        var factory = FactoryReturning(client);

        var result = await MarketDataTools.GetPrice(factory, "binance", "BTC/USDT");

        result.Ok.Should().BeTrue();
        result.Data.Should().Be(42000m);
    }

    [Fact]
    public async Task GetPrice_UnknownExchange_ReturnsExchangeUnavailable()
    {
        var factory = Substitute.For<IExchangeClientFactory>();
        var result = await MarketDataTools.GetPrice(factory, "kraken", "BTC/USDT");
        result.Ok.Should().BeFalse();
        result.Error!.Category.Should().Be("ExchangeUnavailable");
    }

    [Fact]
    public async Task GetPrice_BadSymbol_ReturnsSymbolNotSupported()
    {
        var client = Substitute.For<IExchangeClient>();
        var factory = FactoryReturning(client);
        var result = await MarketDataTools.GetPrice(factory, "binance", "BTCUSDT");
        result.Ok.Should().BeFalse();
        result.Error!.Category.Should().Be("SymbolNotSupported");
    }

    [Fact]
    public async Task GetKlines_BadInterval_ReturnsBadInterval()
    {
        var client = Substitute.For<IExchangeClient>();
        var factory = FactoryReturning(client);
        var result = await MarketDataTools.GetKlines(factory, "binance", "BTC/USDT", "13h");
        result.Ok.Should().BeFalse();
        result.Error!.Category.Should().Be("BadInterval");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetOrderBook_NonPositiveDepth_ReturnsBadRequest(int depth)
    {
        var factory = Substitute.For<IExchangeClientFactory>();
        var result = await MarketDataTools.GetOrderBook(factory, "binance", "BTC/USDT", depth);
        result.Ok.Should().BeFalse();
        result.Error!.Category.Should().Be("BadRequest");
    }

    [Fact]
    public async Task GetRecentTrades_NonPositiveLimit_ReturnsBadRequest()
    {
        var factory = Substitute.For<IExchangeClientFactory>();
        var result = await MarketDataTools.GetRecentTrades(factory, "binance", "BTC/USDT", 0);
        result.Ok.Should().BeFalse();
        result.Error!.Category.Should().Be("BadRequest");
    }

    [Fact]
    public async Task GetKlines_NonPositiveLimit_ReturnsBadRequest()
    {
        var factory = Substitute.For<IExchangeClientFactory>();
        var result = await MarketDataTools.GetKlines(factory, "binance", "BTC/USDT", "1h", 0);
        result.Ok.Should().BeFalse();
        result.Error!.Category.Should().Be("BadRequest");
    }

    [Theory]
    [InlineData("8h", KlineInterval.EightHours)]
    [InlineData("3d", KlineInterval.ThreeDays)]
    public async Task GetKlines_SupportsAllCoreIntervals(string interval, KlineInterval expected)
    {
        var client = Substitute.For<IExchangeClient>();
        client.MarketData.GetCandlesticksAsync(
                Arg.Any<Symbol>(), Arg.Any<KlineInterval>(), Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Candlestick>());
        var factory = FactoryReturning(client);

        var result = await MarketDataTools.GetKlines(factory, "binance", "BTC/USDT", interval);

        result.Ok.Should().BeTrue();
        await client.MarketData.Received(1).GetCandlesticksAsync(
            Arg.Any<Symbol>(), expected, Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
