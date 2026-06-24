using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Bybit;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Bybit.Tests.Unit.Streaming;

/// <summary>
/// No-network DI wiring tests for <c>AddBybitStreams</c>.
/// Verifies that the service collection resolves a correctly-keyed <see cref="IStreamClient"/>
/// and exposes it through <see cref="IStreamClientFactory"/>.
/// </summary>
public class BybitStreamDiTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddBybitExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; });
        services.AddBybitStreams();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddBybitStreams_ResolvesStreamClientFactory()
    {
        await using var sp = Build();
        var factory = sp.GetService<IStreamClientFactory>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public async Task AddBybitStreams_FactoryGetClient_ReturnsBybitClient()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        var client = factory.GetClient(ExchangeId.Bybit);
        client.ExchangeId.Should().Be(ExchangeId.Bybit);
    }

    [Fact]
    public async Task AddBybitStreams_AvailableExchanges_ContainsBybit()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        factory.Available.Should().Contain(ExchangeId.Bybit);
    }
}
