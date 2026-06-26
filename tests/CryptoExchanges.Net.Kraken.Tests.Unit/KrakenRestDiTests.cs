using Xunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using CryptoExchanges.Net.Kraken;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net;

namespace CryptoExchanges.Net.Kraken.Tests.Unit;

/// <summary>
/// DI integration tests for <c>AddKrakenExchange</c>: verifies correct ExchangeId, keyed resolution,
/// and factory availability.
/// </summary>
public class KrakenRestDiTests
{
    [Fact]
    public async Task Di_ResolvesIExchangeClient()
    {
        var services = new ServiceCollection();
        services.AddKrakenExchange();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kraken);
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task Di_ExchangeId_IsKraken()
    {
        var services = new ServiceCollection();
        services.AddKrakenExchange();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kraken);
        client.ExchangeId.Should().Be(ExchangeId.Kraken);
    }

    [Fact]
    public async Task Di_Factory_AvailableContainsKraken()
    {
        var services = new ServiceCollection();
        services.AddKrakenExchange();
        await using var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<IExchangeClientFactory>();
        factory.Available.Should().Contain(ExchangeId.Kraken);
    }

    [Fact]
    public async Task AddCryptoExchanges_IncludesKraken()
    {
        var services = new ServiceCollection();
        services.AddCryptoExchanges();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kraken);
        client.ExchangeId.Should().Be(ExchangeId.Kraken);
    }
}
