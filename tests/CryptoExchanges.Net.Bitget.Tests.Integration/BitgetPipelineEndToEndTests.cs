using System.Net;
using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Bitget;
using CryptoExchanges.Net.Bitget.Auth;
using CryptoExchanges.Net.Bitget.Resilience;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;

namespace CryptoExchanges.Net.Bitget.Tests.Integration;

/// <summary>
/// End-to-end pipeline + signing-handler tests for Bitget, exercised over stub primary handlers (no
/// network). The signing handler carries the four ACCESS-* headers and re-signs per attempt so a
/// retried request gets a single fresh header set with a fresh epoch-ms timestamp + base64 signature.
/// </summary>
[Trait("Category", "Integration")]
public class BitgetPipelineEndToEndTests
{
    private const string ApiKey = "key";
    private const string Passphrase = "pass";

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

    private static BitgetSigningHandler Signing(HttpMessageHandler inner)
        => new(ApiKey, Passphrase, new BitgetSignatureService("secret"), () => 0) { InnerHandler = inner };

    [Fact]
    public async Task SignedGet_SetsFourAccessHeaders()
    {
        var capture = new CaptureHandler();
        using var c = new HttpClient(Signing(capture)) { BaseAddress = new Uri("https://api.bitget.com") };

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v2/spot/trade/unfilled-orders?symbol=BTCUSDT");
        BitgetSigningRequest.MarkSigned(req);
        await c.SendAsync(req, TestContext.Current.CancellationToken);

        var h = capture.Headers[0];
        h.Should().ContainKey("ACCESS-KEY").WhoseValue.Should().Be(ApiKey);
        h.Should().ContainKey("ACCESS-PASSPHRASE").WhoseValue.Should().Be(Passphrase);
        h.Should().ContainKey("ACCESS-TIMESTAMP");
        h.Should().ContainKey("ACCESS-SIGN");
        h["ACCESS-SIGN"].Should().NotBeNullOrWhiteSpace();
        // Bitget renders the signature as base64; it must NOT be lowercase hex.
        h["ACCESS-SIGN"].Should().MatchRegex("^[A-Za-z0-9+/]+={0,2}$");
    }

    [Fact]
    public async Task SignedPost_SetsHeadersAndSignsBody()
    {
        var capture = new CaptureHandler();
        using var c = new HttpClient(Signing(capture)) { BaseAddress = new Uri("https://api.bitget.com") };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/spot/trade/place-order")
        {
            Content = new StringContent("{\"symbol\":\"BTCUSDT\",\"side\":\"buy\"}", System.Text.Encoding.UTF8, "application/json")
        };
        BitgetSigningRequest.MarkSigned(req);
        await c.SendAsync(req, TestContext.Current.CancellationToken);

        var h = capture.Headers[0];
        h.Should().ContainKey("ACCESS-KEY");
        h.Should().ContainKey("ACCESS-PASSPHRASE");
        h.Should().ContainKey("ACCESS-TIMESTAMP");
        h.Should().ContainKey("ACCESS-SIGN");
        h["ACCESS-SIGN"].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnsignedRequest_AddsNoAuthHeaders()
    {
        var capture = new CaptureHandler();
        using var c = new HttpClient(Signing(capture)) { BaseAddress = new Uri("https://api.bitget.com") };

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v2/spot/market/tickers?symbol=BTCUSDT");
        await c.SendAsync(req, TestContext.Current.CancellationToken);

        var h = capture.Headers[0];
        h.Should().NotContainKey("ACCESS-KEY");
        h.Should().NotContainKey("ACCESS-SIGN");
        h.Should().NotContainKey("ACCESS-TIMESTAMP");
    }

    [Fact]
    public async Task SignedGet_Retried_ReSignsWithSingleHeaderSet()
    {
        var stub = new SeqStub();
        var signing = new BitgetSigningHandler(ApiKey, Passphrase, new BitgetSignatureService("secret"), () => 0);
        using var client = HttpClientPipelineBuilder.Build(
            innerHandler: stub,
            options: new ResilienceOptions { MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(1) },
            translator: new BitgetErrorTranslator(),
            gate: new ReactiveRateLimitGate(),
            requestFinalizer: signing);
        client.BaseAddress = new Uri("https://api.bitget.com");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v2/spot/trade/unfilled-orders?symbol=BTCUSDT");
        BitgetSigningRequest.MarkSigned(req);
        using var resp = await client.SendAsync(req, TestContext.Current.CancellationToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        // 1 failure (500) + 1 retry (200): two attempts, each independently signed with a SINGLE header set.
        stub.Headers.Should().HaveCount(2);
        foreach (var h in stub.Headers)
        {
            h.Should().ContainKey("ACCESS-SIGN");
            h.Should().ContainKey("ACCESS-TIMESTAMP");
            h.Should().ContainKey("ACCESS-KEY");
            h.Should().ContainKey("ACCESS-PASSPHRASE");
            // A single value per header (prior-attempt headers are stripped before re-signing).
            h["ACCESS-SIGN"].Should().NotContain(",");
            h["ACCESS-TIMESTAMP"].Should().NotContain(",");
        }
    }

    [Fact]
    public async Task PassphraseMissing_SignedRequest_FastFails()
    {
        // A signing handler constructed without a passphrase must throw on a signed request rather than
        // sending an unsigned/partially-signed request to Bitget.
        var capture = new CaptureHandler();
        var signing = new BitgetSigningHandler(ApiKey, passphrase: string.Empty, new BitgetSignatureService("secret"), () => 0)
        {
            InnerHandler = capture
        };
        using var c = new HttpClient(signing) { BaseAddress = new Uri("https://api.bitget.com") };

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v2/spot/account/assets");
        BitgetSigningRequest.MarkSigned(req);

        var act = async () => await c.SendAsync(req, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Secretless_OrPassphraseless_BuildResilientHttpClient_DoesNotSign()
    {
        // Mirrors the DI gate: a missing secret OR passphrase yields a PassThroughHandler finalizer, so
        // even a signed-marked request carries NO ACCESS-SIGN header.
        var capture = new CaptureHandler();
        var options = new BitgetOptions { BaseUrl = "https://api.bitget.com", ApiKey = "k", SecretKey = "s" /* no passphrase */ };
        var finalizer = (string.IsNullOrEmpty(options.SecretKey) || string.IsNullOrEmpty(options.Passphrase))
            ? (DelegatingHandler)new PassThroughHandler()
            : new BitgetSigningHandler(options.ApiKey, options.Passphrase, new BitgetSignatureService(options.SecretKey), () => 0);

        using var client = HttpClientPipelineBuilder.Build(
            innerHandler: capture,
            options: new ResilienceOptions(),
            translator: new BitgetErrorTranslator(),
            gate: new ReactiveRateLimitGate(),
            requestFinalizer: finalizer);
        client.BaseAddress = new Uri("https://api.bitget.com");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v2/spot/account/assets");
        BitgetSigningRequest.MarkSigned(req);
        await client.SendAsync(req, TestContext.Current.CancellationToken);

        capture.Headers[0].Should().NotContainKey("ACCESS-SIGN");
    }
}
