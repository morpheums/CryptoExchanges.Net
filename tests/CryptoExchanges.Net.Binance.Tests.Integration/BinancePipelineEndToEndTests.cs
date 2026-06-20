using System.Net;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Binance.Auth;
using CryptoExchanges.Net.Binance.Resilience;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Binance.Tests.Integration;

public class BinancePipelineEndToEndTests
{
    private sealed class SeqStub : HttpMessageHandler
    {
        private int _n;
        public List<string> Queries { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Queries.Add(request.RequestUri!.Query);
            var status = _n++ == 0 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request });
        }
    }

    [Fact]
    public async Task SignedGet_Retried_ReSignsEachAttempt()
    {
        var stub = new SeqStub();
        var signing = new BinanceSigningHandler("key", new BinanceSignatureService("secret"), () => 0);
        using var client = HttpClientPipelineBuilder.Build(
            innerHandler: stub,
            options: new ResilienceOptions { MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(1) },
            translator: new BinanceErrorTranslator(),
            gate: new ReactiveRateLimitGate(),
            requestFinalizer: signing);
        client.BaseAddress = new Uri("https://api.binance.com");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v3/account?recvWindow=5000");
        BinanceSigningRequest.MarkSigned(req);
        using var resp = await client.SendAsync(req, TestContext.Current.CancellationToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        // 1 failure (500) + 1 retry (200): two send attempts, each independently signed.
        stub.Queries.Should().HaveCount(2);
        foreach (var q in stub.Queries)
        {
            q.Should().Contain("recvWindow=5000").And.Contain("timestamp=").And.Contain("signature=");
            // exactly one timestamp and one signature per attempt (StripSigning prevents duplication on retry)
            q.Split("timestamp=").Length.Should().Be(2);
            q.Split("signature=").Length.Should().Be(2);
        }
    }
}
