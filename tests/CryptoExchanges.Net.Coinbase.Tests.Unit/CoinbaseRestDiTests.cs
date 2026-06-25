using Xunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using CryptoExchanges.Net.Coinbase;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Coinbase.Tests.Unit;

/// <summary>DI resolution tests for <see cref="ServiceCollectionExtensions.AddCoinbaseExchange"/>.</summary>
public class CoinbaseRestDiTests
{
    [Fact]
    public async Task AddCoinbaseExchange_ResolvesIExchangeClient()
    {
        var services = new ServiceCollection();
        services.AddCoinbaseExchange(o => { o.ApiKey = "k"; o.PrivateKey = "-----BEGIN EC PRIVATE KEY-----\nMHQCAQEEIDummy\n-----END EC PRIVATE KEY-----"; });
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Coinbase);
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task AddCoinbaseExchange_ExchangeId_IsCoinbase()
    {
        var services = new ServiceCollection();
        services.AddCoinbaseExchange();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Coinbase);
        client.ExchangeId.Should().Be(ExchangeId.Coinbase);
    }

    [Fact]
    public async Task AddCoinbaseExchange_AvailableContainsCoinbase()
    {
        var services = new ServiceCollection();
        services.AddCoinbaseExchange();
        await using var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<IExchangeClientFactory>();
        factory.Available.Should().Contain(ExchangeId.Coinbase);
    }

    [Fact]
    public async Task AddCoinbaseExchange_NoCredentials_StillResolves()
    {
        // Public market-data path — no credentials needed; the finalizer is a PassThroughHandler.
        var services = new ServiceCollection();
        services.AddCoinbaseExchange();
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Coinbase).ExchangeId.Should().Be(ExchangeId.Coinbase);
    }

    [Fact]
    public void AddCoinbaseExchange_BaseUrlWithPath_FailFast()
    {
        // BaseAddress must be host-only; a path segment would break the JWT uri claim.
        var services = new ServiceCollection();
        services.AddCoinbaseExchange(o => o.BaseUrl = "https://api.coinbase.com/api/v3");
        var act = () => services.BuildServiceProvider().GetRequiredKeyedService<IExchangeClient>(ExchangeId.Coinbase);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void AddCoinbaseExchange_InvalidOptions_FailFast()
    {
        var services = new ServiceCollection();
        services.AddCoinbaseExchange(o => o.TimeoutSeconds = 0);
        var act = () => services.BuildServiceProvider().GetRequiredKeyedService<IExchangeClient>(ExchangeId.Coinbase);
        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }
}
