using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Kucoin;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Kucoin.Tests.Unit;

/// <summary>
/// Scaffold-level smoke tests proving that <see cref="KucoinSymbolFormat"/> and
/// <see cref="KucoinOptions"/> compile, instantiate, and expose the expected property values.
/// These are placeholder probes; richer behavioural coverage lands in later tasks.
/// </summary>
public class ScaffoldSmokeTests
{
    [Fact]
    public void KucoinSymbolFormat_HasHyphenDelimiter()
    {
        KucoinSymbolFormat.Instance.Delimiter.Should().Be("-");
    }

    [Fact]
    public void KucoinSymbolFormat_HasUpperCasing()
    {
        KucoinSymbolFormat.Instance.Casing.Should().Be(SymbolCasing.Upper);
    }

    [Fact]
    public void KucoinOptions_DefaultBaseUrl_IsKucoinApi()
    {
        var opts = new KucoinOptions();
        opts.BaseUrl.Should().Be("https://api.kucoin.com");
    }

    [Fact]
    public void KucoinOptions_DefaultApiKey_IsEmpty()
    {
        var opts = new KucoinOptions();
        opts.ApiKey.Should().BeEmpty();
    }

    [Fact]
    public void KucoinOptions_DefaultSecretKey_IsEmpty()
    {
        var opts = new KucoinOptions();
        opts.SecretKey.Should().BeEmpty();
    }

    [Fact]
    public void KucoinOptions_DefaultPassphrase_IsEmpty()
    {
        var opts = new KucoinOptions();
        opts.Passphrase.Should().BeEmpty();
    }

    [Fact]
    public void KucoinOptions_DefaultTimeoutSeconds_IsThirty()
    {
        var opts = new KucoinOptions();
        opts.TimeoutSeconds.Should().Be(30);
    }
}
