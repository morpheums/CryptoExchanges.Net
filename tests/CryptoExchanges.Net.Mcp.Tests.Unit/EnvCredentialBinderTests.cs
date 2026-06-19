using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.DependencyInjection;
using CryptoExchanges.Net.Mcp;

namespace CryptoExchanges.Net.Mcp.Tests.Unit;

public class EnvCredentialBinderTests
{
    [Fact]
    public void Apply_PopulatesAllFourExchanges_FromEnv()
    {
        var env = new Dictionary<string, string?>
        {
            ["BINANCE_API_KEY"] = "bk", ["BINANCE_SECRET_KEY"] = "bs",
            ["BYBIT_API_KEY"] = "yk", ["BYBIT_SECRET_KEY"] = "ys",
            ["OKX_API_KEY"] = "ok", ["OKX_SECRET_KEY"] = "os", ["OKX_PASSPHRASE"] = "op",
            ["BITGET_API_KEY"] = "gk", ["BITGET_SECRET_KEY"] = "gs", ["BITGET_PASSPHRASE"] = "gp",
        };
        var options = new CryptoExchangesOptions();

        EnvCredentialBinder.Apply(options, k => env.GetValueOrDefault(k));

        options.BinanceApiKey.Should().Be("bk");
        options.BinanceSecretKey.Should().Be("bs");
        options.BybitApiKey.Should().Be("yk");
        options.OkxPassphrase.Should().Be("op");
        options.BitgetSecretKey.Should().Be("gs");
        options.BitgetPassphrase.Should().Be("gp");
    }

    [Fact]
    public void Apply_LeavesNullsForUnsetVars()
    {
        var options = new CryptoExchangesOptions();
        EnvCredentialBinder.Apply(options, _ => null);
        options.BinanceApiKey.Should().BeNull();
        options.OkxPassphrase.Should().BeNull();
    }
}
