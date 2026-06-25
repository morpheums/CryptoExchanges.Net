using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Coinbase;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Coinbase.Tests.Unit.Streaming;

/// <summary>
/// No-network DI wiring tests for <c>AddCoinbaseStreams</c>.
/// Verifies that the service collection resolves a correctly-keyed <see cref="IStreamClient"/>
/// and exposes it through <see cref="IStreamClientFactory"/>.
/// </summary>
public class CoinbaseStreamDiTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddCoinbaseExchange();
        services.AddCoinbaseStreams();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddCoinbaseStreams_ResolvesStreamClientFactory()
    {
        await using var sp = Build();
        var factory = sp.GetService<IStreamClientFactory>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public async Task AddCoinbaseStreams_FactoryGetClient_ReturnsCoinbaseClient()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        var client = factory.GetClient(ExchangeId.Coinbase);
        client.ExchangeId.Should().Be(ExchangeId.Coinbase);
    }

    [Fact]
    public async Task AddCoinbaseStreams_AvailableExchanges_ContainsCoinbase()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        factory.Available.Should().Contain(ExchangeId.Coinbase);
    }
}
