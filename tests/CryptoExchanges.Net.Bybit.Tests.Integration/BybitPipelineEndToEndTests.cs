using System.Net;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Bybit;
using CryptoExchanges.Net.Bybit.Auth;
using CryptoExchanges.Net.Bybit.Resilience;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Bybit.Tests.Integration;

/// <summary>
/// End-to-end pipeline + signing-handler tests for Bybit, exercised over stub primary handlers
/// (no network). The signing handler is carried in headers (X-BAPI-*) rather than the query/body,
/// and is re-applied per attempt so a retried request re-signs with a fresh timestamp.
/// </summary>
[Trait("Category", "Integration")]
public class BybitPipelineEndToEndTests
{
    private const string RecvWindow = "5000";

    /// <summary>Captures each attempt's headers + body, returning 500 then 200 to force one retry.</summary>
    private sealed class SeqStub : HttpMessageHandler
    {
        private int _n;
        public List<Dictionary<string, string?>> Headers { get; } = [];
        public List<string?> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var snapshot = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in request.Headers)
                snapshot[h.Key] = string.Join(",", h.Value);
            Headers.Add(snapshot);
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false));

            var status = _n++ == 0 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
            return new HttpResponseMessage(status) { RequestMessage = request };
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<Dictionary<string, string?>> Headers { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var snapshot = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in request.Headers)
                snapshot[h.Key] = string.Join(",", h.Value);
            Headers.Add(snapshot);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request });
        }
    }

    private static BybitSigningHandler Signing(HttpMessageHandler inner)
        => new("key", new BybitSignatureService("secret"), RecvWindow, () => 0) { InnerHandler = inner };

    [Fact]
    public async Task SignedGet_SetsBapiHeaders()
    {
        var capture = new CaptureHandler();
        using var c = new HttpClient(Signing(capture)) { BaseAddress = new Uri("https://api.bybit.com") };

        using var req = new HttpRequestMessage(HttpMethod.Get, "/v5/order/realtime?category=spot&symbol=BTCUSDT");
        BybitSigningRequest.MarkSigned(req);
        await c.SendAsync(req, TestContext.Current.CancellationToken);

        var h = capture.Headers[0];
        h.Should().ContainKey("X-BAPI-API-KEY").WhoseValue.Should().Be("key");
        h.Should().ContainKey("X-BAPI-TIMESTAMP");
        h.Should().ContainKey("X-BAPI-RECV-WINDOW").WhoseValue.Should().Be(RecvWindow);
        h.Should().ContainKey("X-BAPI-SIGN");
        h["X-BAPI-SIGN"].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SignedPost_SetsBapiHeadersAndSignsBody()
    {
        var capture = new CaptureHandler();
        using var c = new HttpClient(Signing(capture)) { BaseAddress = new Uri("https://api.bybit.com") };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v5/order/create")
        {
            Content = new StringContent("{\"category\":\"spot\",\"symbol\":\"BTCUSDT\"}", System.Text.Encoding.UTF8, "application/json")
        };
        BybitSigningRequest.MarkSigned(req);
        await c.SendAsync(req, TestContext.Current.CancellationToken);

        var h = capture.Headers[0];
        h.Should().ContainKey("X-BAPI-API-KEY");
        h.Should().ContainKey("X-BAPI-TIMESTAMP");
        h.Should().ContainKey("X-BAPI-SIGN");
        h["X-BAPI-SIGN"].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnsignedRequest_GetsApiKeyHeaderOnly()
    {
        var capture = new CaptureHandler();
        using var c = new HttpClient(Signing(capture)) { BaseAddress = new Uri("https://api.bybit.com") };

        using var req = new HttpRequestMessage(HttpMethod.Get, "/v5/market/tickers?category=spot");
        await c.SendAsync(req, TestContext.Current.CancellationToken);

        var h = capture.Headers[0];
        h.Should().ContainKey("X-BAPI-API-KEY");
        h.Should().NotContainKey("X-BAPI-SIGN");
        h.Should().NotContainKey("X-BAPI-TIMESTAMP");
    }

    [Fact]
    public async Task SignedGet_Retried_ReSignsWithSingleHeaderSet()
    {
        var stub = new SeqStub();
        var signing = new BybitSigningHandler("key", new BybitSignatureService("secret"), RecvWindow, () => 0);
        using var client = HttpClientPipelineBuilder.Build(
            innerHandler: stub,
            options: new ResilienceOptions { MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(1) },
            translator: new BybitErrorTranslator(),
            gate: new ReactiveRateLimitGate(),
            requestFinalizer: signing);
        client.BaseAddress = new Uri("https://api.bybit.com");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/v5/order/realtime?category=spot&symbol=BTCUSDT");
        BybitSigningRequest.MarkSigned(req);
        using var resp = await client.SendAsync(req, TestContext.Current.CancellationToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        // 1 failure (500) + 1 retry (200): two attempts, each independently signed with a SINGLE header set.
        stub.Headers.Should().HaveCount(2);
        foreach (var h in stub.Headers)
        {
            h.Should().ContainKey("X-BAPI-SIGN");
            h.Should().ContainKey("X-BAPI-TIMESTAMP");
            // A single value per header (StripSigning removes prior-attempt headers before re-signing).
            h["X-BAPI-SIGN"].Should().NotContain(",");
            h["X-BAPI-TIMESTAMP"].Should().NotContain(",");
            h["X-BAPI-RECV-WINDOW"].Should().Be(RecvWindow);
        }
    }

    [Fact]
    public async Task Secretless_BuildResilientHttpClient_DoesNotSign()
    {
        // Mirrors the DI secretless path: an empty SecretKey yields a PassThroughHandler finalizer,
        // so even a signed-marked request carries NO X-BAPI-SIGN header.
        var capture = new CaptureHandler();
        var options = new BybitOptions { BaseUrl = "https://api.bybit.com" };
        var finalizer = string.IsNullOrEmpty(options.SecretKey)
            ? (DelegatingHandler)new PassThroughHandler()
            : new BybitSigningHandler(options.ApiKey, new BybitSignatureService(options.SecretKey), RecvWindow, () => 0);

        using var client = HttpClientPipelineBuilder.Build(
            innerHandler: capture,
            options: new ResilienceOptions(),
            translator: new BybitErrorTranslator(),
            gate: new ReactiveRateLimitGate(),
            requestFinalizer: finalizer);
        client.BaseAddress = new Uri("https://api.bybit.com");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/v5/order/realtime?category=spot");
        BybitSigningRequest.MarkSigned(req);
        await client.SendAsync(req, TestContext.Current.CancellationToken);

        capture.Headers[0].Should().NotContainKey("X-BAPI-SIGN");
    }
}
