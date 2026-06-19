using Xunit;
using FluentAssertions;
using NSubstitute;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Mcp.Tools;

namespace CryptoExchanges.Net.Mcp.Tests.Unit;

public class AccountToolsTests
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
    public async Task GetBalances_ReturnsData()
    {
        var client = Substitute.For<IExchangeClient>();
        client.Account.GetBalancesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AssetBalance> { new(Asset.Btc, 1m, 0m) });
        var factory = FactoryReturning(client);

        var result = await AccountTools.GetBalances(factory, "binance");

        result.Ok.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBalances_MissingCredentials_MapsToAuthRequired()
    {
        var client = Substitute.For<IExchangeClient>();
        client.Account.GetBalancesAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<AssetBalance>>(_ => throw new AuthenticationException("no keys"));
        var factory = FactoryReturning(client);

        var result = await AccountTools.GetBalances(factory, "binance");

        result.Ok.Should().BeFalse();
        result.Error!.Category.Should().Be("AuthRequired");
    }

    [Fact]
    public async Task GetBalance_BadAsset_ReturnsError()
    {
        var client = Substitute.For<IExchangeClient>();
        var factory = FactoryReturning(client);
        var result = await AccountTools.GetBalance(factory, "binance", "");
        result.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task GetBalances_UnknownExchange_ReturnsExchangeUnavailable()
    {
        var factory = Substitute.For<IExchangeClientFactory>();
        var result = await AccountTools.GetBalances(factory, "kraken");
        result.Ok.Should().BeFalse();
        result.Error!.Category.Should().Be("ExchangeUnavailable");
    }

    [Fact]
    public async Task GetOpenOrders_ReturnsData()
    {
        var client = Substitute.For<IExchangeClient>();
        client.Trading.GetOpenOrdersAsync(Arg.Any<Symbol?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Order>());
        var factory = FactoryReturning(client);

        var result = await AccountTools.GetOpenOrders(factory, "binance");

        result.Ok.Should().BeTrue();
    }

    [Fact]
    public async Task GetOpenOrders_MissingCredentials_MapsToAuthRequired()
    {
        var client = Substitute.For<IExchangeClient>();
        client.Trading.GetOpenOrdersAsync(Arg.Any<Symbol?>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Order>>(_ => throw new AuthenticationException("no keys"));
        var factory = FactoryReturning(client);

        var result = await AccountTools.GetOpenOrders(factory, "binance");

        result.Ok.Should().BeFalse();
        result.Error!.Category.Should().Be("AuthRequired");
    }

    [Fact]
    public async Task GetTradeHistory_ReturnsData()
    {
        var client = Substitute.For<IExchangeClient>();
        client.Account.GetTradeHistoryAsync(
                Arg.Any<Symbol>(), Arg.Any<int>(), Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Trade>());
        var factory = FactoryReturning(client);

        var result = await AccountTools.GetTradeHistory(factory, "binance", "BTC/USDT");

        result.Ok.Should().BeTrue();
    }
}
