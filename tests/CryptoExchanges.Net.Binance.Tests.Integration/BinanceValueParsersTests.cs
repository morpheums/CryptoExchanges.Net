using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Binance.Internal;

namespace CryptoExchanges.Net.Binance.Tests.Integration;

/// <summary>
/// No-network unit tests for <see cref="BinanceValueParsers"/> value conversions.
/// </summary>
public class BinanceValueParsersTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("0.0")]
    [InlineData("0.00000000")]
    public void ParseOptionalDecimal_ZeroOrEmpty_ReturnsNull(string? value)
        => BinanceValueParsers.ParseOptionalDecimal(value!).Should().BeNull();

    [Theory]
    [InlineData("0.00010000", 0.0001)]
    [InlineData("123.45", 123.45)]
    public void ParseOptionalDecimal_NonZero_ReturnsValue(string value, double expected)
        => BinanceValueParsers.ParseOptionalDecimal(value).Should().Be((decimal)expected);

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("0.00000000", 0)]
    [InlineData("42.5", 42.5)]
    public void ParseDecimal_ParsesOrDefaultsToZero(string? value, double expected)
        => BinanceValueParsers.ParseDecimal(value!).Should().Be((decimal)expected);
}
