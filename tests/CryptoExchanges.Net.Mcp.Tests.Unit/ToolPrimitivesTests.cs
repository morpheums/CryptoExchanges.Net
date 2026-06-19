using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Mcp;

namespace CryptoExchanges.Net.Mcp.Tests.Unit;

public class ToolPrimitivesTests
{
    [Theory]
    [InlineData("binance", ExchangeId.Binance)]
    [InlineData("BYBIT", ExchangeId.Bybit)]
    [InlineData("okx", ExchangeId.Okx)]
    [InlineData("Bitget", ExchangeId.Bitget)]
    public void TryParseExchange_AcceptsKnown_CaseInsensitive(string input, ExchangeId expected)
    {
        ToolInputs.TryParseExchange(input, out var id).Should().BeTrue();
        id.Should().Be(expected);
    }

    [Theory]
    [InlineData("kraken")]   // not registered in our 4
    [InlineData("nope")]
    [InlineData("")]
    public void TryParseExchange_RejectsUnknown(string input)
        => ToolInputs.TryParseExchange(input, out _).Should().BeFalse();

    [Fact]
    public void ParseSymbol_ParsesSlashForm()
    {
        var s = ToolInputs.ParseSymbol("BTC/USDT");
        s.Base.ToString().Should().Be("BTC");
        s.Quote.ToString().Should().Be("USDT");
    }

    [Theory]
    [InlineData("BTCUSDT")]      // no separator
    [InlineData("BTC/")]         // missing quote
    [InlineData("/USDT")]        // missing base
    public void ParseSymbol_ThrowsFormatException_OnBadInput(string input)
    {
        Action act = () => ToolInputs.ParseSymbol(input);
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("1m", KlineInterval.OneMinute)]
    [InlineData("1h", KlineInterval.OneHour)]
    [InlineData("1d", KlineInterval.OneDay)]
    public void TryParseInterval_AcceptsCommonForms(string input, KlineInterval expected)
    {
        ToolInputs.TryParseInterval(input, out var iv).Should().BeTrue();
        iv.Should().Be(expected);
    }

    [Fact]
    public void TryParseInterval_RejectsUnknown()
        => ToolInputs.TryParseInterval("13h", out _).Should().BeFalse();

    [Fact]
    public async Task RunAsync_WrapsSuccess()
    {
        var r = await ToolRunner.RunAsync(() => Task.FromResult(42));
        r.Ok.Should().BeTrue();
        r.Data.Should().Be(42);
        r.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(AuthenticationException), "AuthRequired")]
    [InlineData(typeof(RateLimitExceededException), "RateLimited")]
    [InlineData(typeof(ExchangeConnectivityException), "Connectivity")]
    [InlineData(typeof(FormatException), "SymbolNotSupported")]
    [InlineData(typeof(ExchangeNotRegisteredException), "ExchangeUnavailable")]
    [InlineData(typeof(ExchangeApiException), "ExchangeError")]
    [InlineData(typeof(InvalidOperationException), "Unknown")]
    public async Task RunAsync_MapsExceptionsToCategories(Type exType, string expectedCategory)
    {
        var ex = CreateException(exType);
        var r = await ToolRunner.RunAsync<int>(() => throw ex);
        r.Ok.Should().BeFalse();
        r.Error!.Category.Should().Be(expectedCategory);
    }

    // NOTE: ExchangeNotRegisteredException takes ExchangeId (not string) per its public ctor.
    private static Exception CreateException(Type t) => t switch
    {
        _ when t == typeof(AuthenticationException) => new AuthenticationException("auth"),
        _ when t == typeof(RateLimitExceededException) => new RateLimitExceededException("rate"),
        _ when t == typeof(ExchangeConnectivityException) => new ExchangeConnectivityException("conn"),
        _ when t == typeof(FormatException) => new FormatException("sym"),
        _ when t == typeof(ExchangeNotRegisteredException) => new ExchangeNotRegisteredException(ExchangeId.Binance),
        _ when t == typeof(ExchangeApiException) => new ExchangeApiException("api err"),
        _ => new InvalidOperationException("boom"),
    };
}
