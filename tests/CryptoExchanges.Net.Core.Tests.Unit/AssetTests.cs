using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Core.Tests.Unit;

public class AssetTests
{
    [Fact]
    public void Of_StoresTicker()
        => Asset.Of("BTC").Ticker.Should().Be("BTC");

    [Theory]
    [InlineData(" btc ", "BTC")]
    [InlineData("usdt", "USDT")]
    [InlineData("1inch", "1INCH")]
    public void Of_TrimsAndUpperCases(string input, string expected)
        => Asset.Of(input).Ticker.Should().Be(expected);

    [Fact]
    public void Of_EqualityIsCaseInsensitiveViaNormalization()
        => Asset.Of("btc").Should().Be(Asset.Of("BTC"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("BTC-USD")]
    [InlineData("BTC/USD")]
    [InlineData("BTC.B")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890")]
    public void Of_Throws_OnInvalidTicker(string input)
    {
        var act = () => Asset.Of(input);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryOf_ReturnsFalse_OnInvalid()
    {
        Asset.TryOf("BTC/USD", out _).Should().BeFalse();
    }

    [Fact]
    public void TryOf_ReturnsTrueAndNormalizes_OnValid()
    {
        Asset.TryOf(" eth ", out var asset).Should().BeTrue();
        asset.Ticker.Should().Be("ETH");
    }

    [Fact]
    public void None_IsDefault_AndIsNone()
    {
        Asset.None.IsNone.Should().BeTrue();
        default(Asset).IsNone.Should().BeTrue();
        Asset.Of("BTC").IsNone.Should().BeFalse();
    }

    [Fact]
    public void None_TickerIsEmptyString()
        => Asset.None.Ticker.Should().BeEmpty();

    [Fact]
    public void ToString_ReturnsTicker()
        => Asset.Of("BTC").ToString().Should().Be("BTC");

    [Fact]
    public void Of_Accepts_MaxLengthTicker()
        => Asset.Of(new string('A', 32)).Ticker.Length.Should().Be(32);

    [Fact]
    public void Of_Throws_OnTooLongTicker()
    {
        var act = () => Asset.Of(new string('A', 33));
        act.Should().Throw<ArgumentException>();
    }
}
