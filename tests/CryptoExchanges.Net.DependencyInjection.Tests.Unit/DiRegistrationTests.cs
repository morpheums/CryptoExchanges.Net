using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
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
}
