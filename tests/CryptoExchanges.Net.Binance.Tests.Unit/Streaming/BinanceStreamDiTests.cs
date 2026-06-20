using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Binance.Tests.Unit.Streaming;

/// <summary>
/// No-network DI wiring tests for <c>AddBinanceStreams</c>.
/// Verifies that the service collection resolves a correctly-keyed <see cref="IStreamClient"/>
/// and exposes it through <see cref="IStreamClientFactory"/>.
/// </summary>
public class BinanceStreamDiTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddBinanceExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; });
        services.AddBinanceStreams();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddBinanceStreams_ResolvesStreamClientFactory()
    {
        await using var sp = Build();
        var factory = sp.GetService<IStreamClientFactory>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public async Task AddBinanceStreams_FactoryGetClient_ReturnsBinanceClient()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        var client = factory.GetClient(ExchangeId.Binance);
        client.ExchangeId.Should().Be(ExchangeId.Binance);
    }

    [Fact]
    public async Task AddBinanceStreams_AvailableExchanges_ContainsBinance()
    {
        await using var sp = Build();
        var factory = sp.GetRequiredService<IStreamClientFactory>();
        factory.Available.Should().Contain(ExchangeId.Binance);
    }
}
