using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Okx;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Okx.Tests.Unit.Streaming;

/// <summary>
/// No-network DI wiring tests for <c>AddOkxStreams</c>.
/// Verifies that the service collection resolves a correctly-keyed <see cref="IStreamClient"/>
/// and exposes it through <see cref="IStreamClientFactory"/>.
/// </summary>
public class OkxStreamDiTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddOkxExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        services.AddOkxStreams();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddOkxStreams_ResolvesStreamClientFactory()
    {
        await using var sp = Build();
        var factory = sp.GetService<IStreamClientFactory>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public async Task AddOkxStreams_FactoryGetClient_ReturnsOkxClient()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        var client = factory.GetClient(ExchangeId.Okx);
        client.ExchangeId.Should().Be(ExchangeId.Okx);
    }

    [Fact]
    public async Task AddOkxStreams_AvailableExchanges_ContainsOkx()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        factory.Available.Should().Contain(ExchangeId.Okx);
    }
}
