using System.Text.Json;
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

public class AssetJsonTests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions();

    [Fact]
    public void Serialize_ValidAsset_ProducesQuotedTicker()
    {
        var json = JsonSerializer.Serialize(Asset.Of("BTC"), Options);
        json.Should().Be("\"BTC\"");
    }

    [Fact]
    public void Deserialize_LowercaseTicker_ProducesNormalizedAsset()
    {
        var asset = JsonSerializer.Deserialize<Asset>("\"usdt\"", Options);
        asset.Should().Be(Asset.Of("USDT"));
    }

    [Fact]
    public void Deserialize_InvalidTicker_ProducesNone()
    {
        // "BTC/USD" is rejected by TryOf (contains '/'), so converter returns Asset.None.
        var asset = JsonSerializer.Deserialize<Asset>("\"BTC/USD\"", Options);
        asset.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Deserialize_NumberToken_ThrowsJsonException()
    {
        var act = () => JsonSerializer.Deserialize<Asset>("123", Options);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Serialize_None_ProducesJsonNull()
    {
        var json = JsonSerializer.Serialize(Asset.None, Options);
        json.Should().Be("null");
    }

    [Fact]
    public void Deserialize_JsonNull_ProducesNone()
    {
        var asset = JsonSerializer.Deserialize<Asset>("null", Options);
        asset.IsNone.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_ValidAsset_PreservesValue()
    {
        var original = Asset.Of("ETH");
        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<Asset>(json, Options);
        restored.Should().Be(original);
    }

    [Fact]
    public void RoundTrip_None_PreservesNone()
    {
        var json = JsonSerializer.Serialize(Asset.None, Options);
        var restored = JsonSerializer.Deserialize<Asset>(json, Options);
        restored.IsNone.Should().BeTrue();
    }
}

public class AssetConstantsTests
{
    [Fact]
    public void Btc_HasExpectedTicker()
        => Asset.Btc.Ticker.Should().Be("BTC");

    [Fact]
    public void Usdt_HasExpectedTicker()
        => Asset.Usdt.Ticker.Should().Be("USDT");

    [Fact]
    public void Constant_EqualsOfEquivalent()
        => Asset.Eth.Should().Be(Asset.Of("ETH"));

    [Fact]
    public void Constants_AreNotNone()
    {
        Asset.Btc.IsNone.Should().BeFalse();
        Asset.Usdc.IsNone.Should().BeFalse();
        Asset.Bnb.IsNone.Should().BeFalse();
    }
}
