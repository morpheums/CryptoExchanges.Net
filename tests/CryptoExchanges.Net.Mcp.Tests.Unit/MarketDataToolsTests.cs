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
    public async Task GetKlines_BadInterval_ReturnsSymbolNotSupported_OrValidationError()
    {
        var client = Substitute.For<IExchangeClient>();
        var factory = FactoryReturning(client);
        var result = await MarketDataTools.GetKlines(factory, "binance", "BTC/USDT", "13h");
        result.Ok.Should().BeFalse();
        result.Error!.Category.Should().Be("BadInterval");
    }
}
