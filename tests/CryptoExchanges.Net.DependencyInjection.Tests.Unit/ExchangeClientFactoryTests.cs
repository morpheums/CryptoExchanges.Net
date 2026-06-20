using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.DependencyInjection;

namespace CryptoExchanges.Net.DependencyInjection.Tests.Unit;

public class ExchangeClientFactoryTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddBinanceExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Get_ReturnsRegisteredClient()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IExchangeClientFactory>();
        factory.GetClient(ExchangeId.Binance).ExchangeId.Should().Be(ExchangeId.Binance);
    }

    [Fact]
    public async Task Get_Unregistered_Throws()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IExchangeClientFactory>();
        var act = () => factory.GetClient(ExchangeId.Coinbase);
        act.Should().Throw<ExchangeNotRegisteredException>()
            .Which.ExchangeId.Should().Be(ExchangeId.Coinbase);
    }

    [Fact]
    public async Task TryGet_Registered_ReturnsTrue()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IExchangeClientFactory>();
        factory.TryGet(ExchangeId.Binance, out var client).Should().BeTrue();
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task TryGet_Unregistered_ReturnsFalse()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IExchangeClientFactory>();
        factory.TryGet(ExchangeId.Coinbase, out var client).Should().BeFalse();
        client.Should().BeNull();
    }

    [Fact]
    public async Task Available_ListsRegisteredExchanges()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IExchangeClientFactory>();
        factory.Available.Should().Contain(ExchangeId.Binance);
    }
}
