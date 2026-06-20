using Xunit;
using AwesomeAssertions;

namespace CryptoExchanges.Net.Http.Tests.Unit;

public class ExchangeUrlTests
{
    [Fact]
    public void BuildQueryString_NullOrEmpty_ReturnsEmpty()
    {
        ExchangeUrl.BuildQueryString(null).Should().BeEmpty();
        ExchangeUrl.BuildQueryString(new Dictionary<string, string>()).Should().BeEmpty();
    }

    [Fact]
    public void BuildQueryString_JoinsAndEscapesInOrder()
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = "BTC/USDT",
            ["limit"] = "100"
        };

        // Insertion order preserved; key and value percent-escaped (the '/' becomes %2F).
        ExchangeUrl.BuildQueryString(parameters).Should().Be("symbol=BTC%2FUSDT&limit=100");
    }

    [Theory]
    [InlineData("https://api.bitget.com", "https://api.bitget.com")]
    [InlineData("https://api.bitget.com/", "https://api.bitget.com")]
    [InlineData("https://www.okx.com/", "https://www.okx.com")]
    public void NormalizeHostRoot_HostOnly_TrimsTrailingSlash(string input, string expected)
        => ExchangeUrl.NormalizeHostRoot(input).Should().Be(expected);

    [Theory]
    [InlineData("https://api.bitget.com/api/v2")]
    [InlineData("https://www.okx.com/api/v5")]
    public void NormalizeHostRoot_WithPathSegment_Throws(string input)
    {
        var act = () => ExchangeUrl.NormalizeHostRoot(input);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeHostRoot_NullOrBlank_Throws(string input)
    {
        var act = () => ExchangeUrl.NormalizeHostRoot(input);
        act.Should().Throw<ArgumentException>();
    }
}
