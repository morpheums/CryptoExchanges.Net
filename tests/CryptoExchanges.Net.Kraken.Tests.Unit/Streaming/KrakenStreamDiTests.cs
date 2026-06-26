using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Kraken;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Kraken.Tests.Unit.Streaming;

/// <summary>
/// No-network DI wiring tests for <c>AddKrakenStreams</c>.
/// Verifies that the service collection resolves a correctly-keyed <see cref="IStreamClient"/>
/// and exposes it through <see cref="IStreamClientFactory"/>.
/// </summary>
public class KrakenStreamDiTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        // No-network DI wiring test: credentials are never used, so none are configured.
        services.AddKrakenExchange();
        services.AddKrakenStreams();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddKrakenStreams_ResolvesStreamClientFactory()
    {
        await using var sp = Build();
        var factory = sp.GetService<IStreamClientFactory>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public async Task AddKrakenStreams_FactoryGetClient_ReturnsKrakenClient()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        var client = factory.GetClient(ExchangeId.Kraken);
        client.ExchangeId.Should().Be(ExchangeId.Kraken);
    }

    [Fact]
    public async Task AddKrakenStreams_AvailableExchanges_ContainsKraken()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        factory.Available.Should().Contain(ExchangeId.Kraken);
    }
}
