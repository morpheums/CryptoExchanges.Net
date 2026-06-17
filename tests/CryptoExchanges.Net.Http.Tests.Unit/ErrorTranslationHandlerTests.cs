using System.Net;
using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Interfaces;

namespace CryptoExchanges.Net.Http.Tests.Unit;

public class ErrorTranslationHandlerTests
{
    private sealed class StubTranslator : IExchangeErrorTranslator
    {
        public ExchangeException Translate(HttpResponseMessage response, string body)
            => new InvalidOrderException($"translated:{(int)response.StatusCode}:{body}");
    }

    private static HttpClient Client(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
    {
        var handler = new ErrorTranslationHandler(new StubTranslator())
        {
            InnerHandler = new StubHandler(responder)
        };
        return new HttpClient(handler) { BaseAddress = new Uri("https://x") };
    }

    [Fact]
    public async Task Success_PassesThrough()
    {
        using var c = Client((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var resp = await c.GetAsync("/p", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BusinessError_4xx_Throws_Translated()
    {
        using var c = Client((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"code\":-1100,\"msg\":\"bad\"}")
        });
        var act = async () => await c.GetAsync("/p", TestContext.Current.CancellationToken);
        (await act.Should().ThrowAsync<InvalidOrderException>())
            .Which.Message.Should().StartWith("translated:400:");
    }

    [Fact]
    public async Task Transient_5xx_PassesThrough_NotTranslated()
    {
        using var c = Client((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var resp = await c.GetAsync("/p", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task RateLimit_429_PassesThrough_NotTranslated()
    {
        using var c = Client((_, _) => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var resp = await c.GetAsync("/p", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
