using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Core.Exceptions;

namespace CryptoExchanges.Net.Core.Tests.Unit;

public class ExchangeExceptionTests
{
    [Fact]
    public void ApiException_CarriesCodeAndIsExchangeException()
    {
        var ex = new ExchangeApiException("bad", code: -1100, rawBody: "{}");
        ex.Should().BeAssignableTo<ExchangeException>();
        ex.Code.Should().Be(-1100);
        ex.RawBody.Should().Be("{}");
        ex.Message.Should().Be("bad");
    }

    [Fact]
    public void RateLimit_CarriesRetryAfter_AndIsApiException()
    {
        var ex = new RateLimitExceededException("slow down", retryAfter: TimeSpan.FromSeconds(5));
        ex.Should().BeAssignableTo<ExchangeApiException>();
        ex.RetryAfter.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Auth_Invalid_Insufficient_AreApiExceptions()
    {
        new AuthenticationException("x").Should().BeAssignableTo<ExchangeApiException>();
        new InvalidOrderException("x").Should().BeAssignableTo<ExchangeApiException>();
        new InsufficientBalanceException("x").Should().BeAssignableTo<ExchangeApiException>();
    }

    [Fact]
    public void Connectivity_IsExchangeException_NotApiException_AndFlagsIndeterminate()
    {
        var ex = new ExchangeConnectivityException("timeout", operationOutcomeIndeterminate: true);
        ex.Should().BeAssignableTo<ExchangeException>();
        ex.Should().NotBeAssignableTo<ExchangeApiException>();
        ex.OperationOutcomeIndeterminate.Should().BeTrue();
    }
}
