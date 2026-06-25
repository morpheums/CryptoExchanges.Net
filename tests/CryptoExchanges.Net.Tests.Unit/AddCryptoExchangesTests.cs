using AwesomeAssertions;
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CryptoExchanges.Net.Tests.Unit;

/// <summary>Consolidated aggregator resolution tests for <see cref="ServiceCollectionExtensions.AddCryptoExchanges"/>.</summary>
[Trait("Category", "Unit")]
public class AddCryptoExchangesTests
{
    /// <summary>Asserts that <c>AddCryptoExchanges()</c> registers a resolvable keyed <see cref="IExchangeClient"/> for all five exchanges.</summary>
    [Fact]
    public async Task AddCryptoExchanges_ResolvesAllFiveExchanges()
    {
        var services = new ServiceCollection();
        services.AddCryptoExchanges();
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Binance).ExchangeId
            .Should().Be(ExchangeId.Binance);
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bybit).ExchangeId
            .Should().Be(ExchangeId.Bybit);
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Okx).ExchangeId
            .Should().Be(ExchangeId.Okx);
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bitget).ExchangeId
            .Should().Be(ExchangeId.Bitget);
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kucoin).ExchangeId
            .Should().Be(ExchangeId.Kucoin);
    }

    /// <summary>Asserts that <c>AddCryptoExchanges()</c> registers a resolvable keyed <see cref="IExchangeClient"/> for Coinbase.</summary>
    [Fact]
    public async Task AddCryptoExchanges_IncludesCoinbase()
    {
        var services = new ServiceCollection();
        services.AddCryptoExchanges();
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Coinbase);
        client.ExchangeId.Should().Be(ExchangeId.Coinbase);
    }

    /// <summary>Validates that <c>CryptoExchangesOptions.BinanceApiKey</c> flows through <c>AddCryptoExchanges</c> into the resolved <see cref="BinanceOptions"/>.</summary>
    [Fact]
    public async Task AddCryptoExchanges_OptionsFlow_ReachesExchangeOptions()
    {
        // Validates the delegation path: CryptoExchangesOptions.BinanceApiKey flows through
        // AddCryptoExchanges → AddBinanceExchange configure delegate → BinanceOptions.ApiKey.
        var services = new ServiceCollection();
        services.AddCryptoExchanges(o => o.BinanceApiKey = "test-key");
        await using var sp = services.BuildServiceProvider();

        var resolvedOptions = sp.GetRequiredService<BinanceOptions>();
        resolvedOptions.ApiKey.Should().Be("test-key");
    }
}
