using AwesomeAssertions;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.DependencyInjection;
using CryptoExchanges.Net.Kucoin;

namespace CryptoExchanges.Net.Kucoin.Tests.Unit;

/// <summary>
/// No-network unit tests for KuCoin DI registration, keyed service resolution, ValidateOnStart
/// fail-fast, and AddCryptoExchanges aggregator coverage.
/// </summary>
[Trait("Category", "Unit")]
public class KucoinDiTests
{
    // ── AddKucoinExchange keyed resolution ──

    [Fact]
    public async Task AddKucoinExchange_ResolvesKeyedClient()
    {
        var services = new ServiceCollection();
        services.AddKucoinExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kucoin);
        client.ExchangeId.Should().Be(ExchangeId.Kucoin);
    }

    [Fact]
    public async Task AddKucoinExchange_Secretless_StillResolvesWorkingClient()
    {
        // A secretless registration must resolve (public market data needs no credentials); the
        // finalizer is a PassThroughHandler rather than a signing handler in this path.
        var services = new ServiceCollection();
        services.AddKucoinExchange();
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kucoin).ExchangeId
            .Should().Be(ExchangeId.Kucoin);
    }

    [Fact]
    public async Task AddKucoinExchange_PassphraseMissing_StillResolves()
    {
        // Secret present but passphrase missing: signing is gated OFF (PassThrough), so the client
        // must still resolve without tripping KucoinOptions.ToCredentials() (which throws on an
        // empty passphrase).
        var services = new ServiceCollection();
        services.AddKucoinExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; /* no passphrase */ });
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kucoin).ExchangeId
            .Should().Be(ExchangeId.Kucoin);
    }

    // ── ValidateOnStart fail-fast ──

    [Fact]
    public void AddKucoinExchange_InvalidOptions_FailFast_TimeoutZero()
    {
        var services = new ServiceCollection();
        services.AddKucoinExchange(o => o.TimeoutSeconds = 0);
        var act = () => services.BuildServiceProvider()
            .GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kucoin);
        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public void AddKucoinExchange_BaseUrlWithPath_FailFast()
    {
        // KuCoin reassembles its signed prehash from RequestUri.PathAndQuery, so a BaseUrl carrying
        // a path segment would break sign-consistency. ExchangeUrl.NormalizeHostRoot must fail fast
        // rather than silently produce rejected signatures.
        var services = new ServiceCollection();
        services.AddKucoinExchange(o => o.BaseUrl = "https://api.kucoin.com/api/v1");
        var act = () => services.BuildServiceProvider()
            .GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kucoin);
        act.Should().Throw<Exception>();
    }

    // ── Singleton semantics ──

    [Fact]
    public void AddKucoinExchange_MapperIsKeyedSingleton()
    {
        var services = new ServiceCollection();
        services.AddKucoinExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        using var sp = services.BuildServiceProvider();

        var m1 = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Kucoin);
        var m2 = sp.GetRequiredKeyedService<IMapper>(ExchangeId.Kucoin);
        m1.Should().BeSameAs(m2);
    }

    [Fact]
    public void AddKucoinExchange_NoUnkeyed_ExchangeClient_Registered()
    {
        var services = new ServiceCollection();
        services.AddKucoinExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        services.BuildServiceProvider().GetService<IExchangeClient>().Should().BeNull();
    }

    // ── Scope graph validity ──

    [Fact]
    public async Task AddKucoinExchange_IsScopeClean()
    {
        var services = new ServiceCollection();
        services.AddKucoinExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; o.Passphrase = "p"; });
        // ValidateScopes + ValidateOnBuild assert the graph has no captive/scope-violating dependencies.
        await using var sp = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kucoin).Should().NotBeNull();
    }

    // ── AddCryptoExchanges aggregator ──

    [Fact]
    public async Task AddCryptoExchanges_ResolvesKucoinClient()
    {
        var services = new ServiceCollection();
        services.AddCryptoExchanges();
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Kucoin).ExchangeId
            .Should().Be(ExchangeId.Kucoin);
    }

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

    [Fact]
    public async Task AddCryptoExchanges_KucoinOptions_AppliesViaAggregator()
    {
        // Validates the delegation path: CryptoExchangesOptions.KucoinApiKey flows through
        // AddCryptoExchanges → AddKucoinExchange configure delegate → KucoinOptions.ApiKey.
        var services = new ServiceCollection();
        services.AddCryptoExchanges(o =>
        {
            o.KucoinApiKey = "test-api-key";
            o.KucoinSecretKey = "test-secret";
            o.KucoinPassphrase = "test-passphrase";
        });
        await using var sp = services.BuildServiceProvider();

        var resolvedOptions = sp.GetRequiredService<KucoinOptions>();
        resolvedOptions.ApiKey.Should().Be("test-api-key");
        resolvedOptions.SecretKey.Should().Be("test-secret");
        resolvedOptions.Passphrase.Should().Be("test-passphrase");
    }
}
