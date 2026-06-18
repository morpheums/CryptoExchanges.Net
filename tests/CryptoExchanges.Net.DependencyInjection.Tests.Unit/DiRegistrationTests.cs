using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Bybit;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.DependencyInjection;

namespace CryptoExchanges.Net.DependencyInjection.Tests.Unit;

public class DiRegistrationTests
{
    private static ServiceProvider Build(Action<CryptoExchanges.Net.Binance.BinanceOptions>? cfg = null)
    {
        var services = new ServiceCollection();
        services.AddBinanceExchange(cfg ?? (o => { o.ApiKey = "k"; o.SecretKey = "s"; }));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Resolves_KeyedExchangeClient()
        => Build().GetRequiredKeyedService<IExchangeClient>(ExchangeId.Binance).ExchangeId.Should().Be(ExchangeId.Binance);

    [Fact]
    public void Resolves_Mapper_AsSingleton()
    {
        using var sp = Build();
        var m1 = sp.GetRequiredKeyedService<DeltaMapper.IMapper>(ExchangeId.Binance);
        var m2 = sp.GetRequiredKeyedService<DeltaMapper.IMapper>(ExchangeId.Binance);
        m1.Should().BeSameAs(m2);
    }

    [Fact]
    public void NoUnkeyed_ExchangeClient_Registered()
        => Build().GetService<IExchangeClient>().Should().BeNull();

    [Fact]
    public void InvalidOptions_FailFast_OnValidateOnStart()
    {
        var services = new ServiceCollection();
        services.AddBinanceExchange(o => { o.TimeoutSeconds = 0; });
        var act = () => services.BuildServiceProvider().GetRequiredKeyedService<IExchangeClient>(ExchangeId.Binance);
        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public async Task BybitOnly_Registration_ResolvesBybitClient()
    {
        // Demonstrates the ADR-001 dependency direction: AddBybitExchange ships from the Bybit
        // assembly and registers a working Bybit client WITHOUT going through AddCryptoExchanges
        // (i.e. a Bybit-only consumer needs only the Bybit assembly, not the aggregator or Binance).
        var services = new ServiceCollection();
        services.AddBybitExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; });
        // await using: the resolved IExchangeClient is IAsyncDisposable-only.
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Bybit).ExchangeId
            .Should().Be(ExchangeId.Bybit);
        sp.GetService<IExchangeClient>().Should().BeNull();
    }

    [Fact]
    public async Task Registration_IsScopeClean()
    {
        var services = new ServiceCollection();
        services.AddBinanceExchange(o => { o.ApiKey = "k"; o.SecretKey = "s"; });
        // ValidateScopes + ValidateOnBuild assert the graph has no captive/scope-violating dependencies.
        // await using: the resolved IExchangeClient is IAsyncDisposable-only, so the container graph
        // must be disposed asynchronously (a synchronous Dispose would throw on that async-only disposable).
        await using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        sp.GetRequiredKeyedService<IExchangeClient>(ExchangeId.Binance).Should().NotBeNull();
    }
}
