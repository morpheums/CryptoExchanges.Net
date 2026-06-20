using System.Net;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Binance.Auth;
using CryptoExchanges.Net.Binance.Resilience;

namespace CryptoExchanges.Net.Binance.Tests.Integration;

public class BinanceSigningHandlerTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<Uri> Uris { get; } = [];
        public List<string?> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uris.Add(request.RequestUri!);
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
        }
    }

    [Fact]
    public async Task SignedGet_AppendsTimestampAndSignature()
    {
        var capture = new CaptureHandler();
        var handler = new BinanceSigningHandler("key", new BinanceSignatureService("secret"), () => 0)
        { InnerHandler = capture };
        using var c = new HttpClient(handler) { BaseAddress = new Uri("https://api.binance.com") };

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v3/account?recvWindow=5000");
        BinanceSigningRequest.MarkSigned(req);
        await c.SendAsync(req, TestContext.Current.CancellationToken);

        capture.Uris[0].Query.Should().Contain("recvWindow=5000")
            .And.Contain("timestamp=").And.Contain("signature=");
    }

    [Fact]
    public async Task UnsignedRequest_NotSigned()
    {
        var capture = new CaptureHandler();
        var handler = new BinanceSigningHandler("key", new BinanceSignatureService("secret"), () => 0)
        { InnerHandler = capture };
        using var c = new HttpClient(handler) { BaseAddress = new Uri("https://api.binance.com") };
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v3/ping");
        await c.SendAsync(req, TestContext.Current.CancellationToken);
        capture.Uris[0].Query.Should().NotContain("signature=");
    }

    [Fact]
    public async Task SignedPost_SignsBody()
    {
        var capture = new CaptureHandler();
        var handler = new BinanceSigningHandler("key", new BinanceSignatureService("secret"), () => 0)
        { InnerHandler = capture };
        using var c = new HttpClient(handler) { BaseAddress = new Uri("https://api.binance.com") };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v3/order")
        { Content = new StringContent("symbol=BTCUSDT&side=BUY", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded") };
        BinanceSigningRequest.MarkSigned(req);
        await c.SendAsync(req, TestContext.Current.CancellationToken);
        capture.Bodies[0].Should().Contain("symbol=BTCUSDT").And.Contain("timestamp=").And.Contain("signature=");
    }
}
