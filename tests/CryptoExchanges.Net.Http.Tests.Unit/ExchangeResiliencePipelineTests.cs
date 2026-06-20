using System.Net;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;

namespace CryptoExchanges.Net.Http.Tests.Unit;

public class ExchangeResiliencePipelineTests
{
    private sealed class StubTranslator : IExchangeErrorTranslator
    {
        public ExchangeException Translate(HttpResponseMessage response, string body)
            => new InvalidOrderException($"biz:{(int)response.StatusCode}");
    }

    private static (HttpClient client, StubHandler stub) Build(
        Func<HttpRequestMessage, int, HttpResponseMessage> responder, ResilienceOptions? opts = null)
    {
        var stub = new StubHandler(responder);
        var client = HttpClientPipelineBuilder.Build(
            innerHandler: stub,
            options: opts ?? new ResilienceOptions { MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(1) },
            translator: new StubTranslator(),
            gate: new ReactiveRateLimitGate(),
            requestFinalizer: null);
        client.BaseAddress = new Uri("https://x");
        return (client, stub);
    }

    [Fact]
    public async Task Get_5xx_Retried_ThenExhausted_Throws_Connectivity()
    {
        var (c, stub) = Build((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var act = async () => await c.GetAsync("/p", TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ExchangeConnectivityException>();
        stub.Calls.Should().Be(3);
    }

    [Fact]
    public async Task Get_5xx_ThenSuccess_Recovers()
    {
        var (c, stub) = Build((_, n) => new HttpResponseMessage(
            n == 0 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK));
        var resp = await c.GetAsync("/p", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        stub.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Get_NetworkException_Retried_ThenExhausted_Throws_Connectivity()
    {
        var (c, stub) = Build((_, _) => throw new HttpRequestException("boom"));
        var act = async () => await c.GetAsync("/p", TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ExchangeConnectivityException>();
        stub.Requests.Count.Should().Be(3);
    }

    [Fact]
    public async Task Post_5xx_NotRetried_Throws_Connectivity()
    {
        var (c, stub) = Build((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var act = async () => await c.PostAsync("/p", new StringContent("x"), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ExchangeConnectivityException>();
        stub.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Get_429_Exhausted_Throws_RateLimit()
    {
        var (c, stub) = Build((_, _) => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        await ((Func<Task>)(async () =>
            await c.GetAsync("/p", TestContext.Current.CancellationToken)))
            .Should().ThrowAsync<RateLimitExceededException>();
        stub.Calls.Should().Be(3);
    }

    [Fact]
    public async Task Get_BusinessError_400_NotRetried_Throws_Typed()
    {
        var (c, stub) = Build((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
        { Content = new StringContent("{}") });
        await ((Func<Task>)(async () =>
            await c.GetAsync("/p", TestContext.Current.CancellationToken)))
            .Should().ThrowAsync<InvalidOrderException>();
        stub.Calls.Should().Be(1);
    }
}
