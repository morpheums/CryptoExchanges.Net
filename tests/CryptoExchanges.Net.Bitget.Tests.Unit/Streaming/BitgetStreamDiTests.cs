using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Bitget;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Bitget.Tests.Unit.Streaming;

/// <summary>
/// No-network DI wiring tests for <c>AddBitgetStreams</c>.
/// Verifies that the service collection resolves a correctly-keyed <see cref="IStreamClient"/>
/// and exposes it through <see cref="IStreamClientFactory"/>.
/// </summary>
public class BitgetStreamDiTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddBitgetExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        services.AddBitgetStreams();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddBitgetStreams_ResolvesStreamClientFactory()
    {
        await using var sp = Build();
        var factory = sp.GetService<IStreamClientFactory>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public async Task AddBitgetStreams_FactoryGetClient_ReturnsBitgetClient()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        var client = factory.GetClient(ExchangeId.Bitget);
        client.ExchangeId.Should().Be(ExchangeId.Bitget);
    }

    [Fact]
    public async Task AddBitgetStreams_AvailableExchanges_ContainsBitget()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        factory.Available.Should().Contain(ExchangeId.Bitget);
    }
}
