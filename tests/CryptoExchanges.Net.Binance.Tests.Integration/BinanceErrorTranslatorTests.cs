using System.Net;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Binance.Resilience;
using CryptoExchanges.Net.Core.Exceptions;

namespace CryptoExchanges.Net.Binance.Tests.Integration;

public class BinanceErrorTranslatorTests
{
    private static ExchangeException Translate(HttpStatusCode status, string body)
    {
        using var resp = new HttpResponseMessage(status);
        return new BinanceErrorTranslator().Translate(resp, body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "{\"code\":-2015,\"msg\":\"invalid key\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":-1022,\"msg\":\"bad sig\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":-1021,\"msg\":\"timestamp\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":-2010,\"msg\":\"Account has insufficient balance\"}", typeof(InsufficientBalanceException))]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":-2011,\"msg\":\"unknown order\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":-1100,\"msg\":\"illegal chars\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":-9999,\"msg\":\"weird\"}", typeof(ExchangeApiException))]
    public void MapsCodes(HttpStatusCode status, string body, Type expected)
        => Translate(status, body).Should().BeOfType(expected);

    [Fact]
    public void Maps429_ToRateLimit()
        => Translate(HttpStatusCode.TooManyRequests, "{\"code\":-1003,\"msg\":\"too many\"}")
            .Should().BeOfType<RateLimitExceededException>();

    [Fact]
    public void NonJsonBody_FallsBackToApiException()
        => Translate(HttpStatusCode.BadGateway, "<html>502</html>")
            .Should().BeOfType<ExchangeApiException>();
}
