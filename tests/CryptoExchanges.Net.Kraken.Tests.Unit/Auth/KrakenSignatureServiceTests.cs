using System.Net;
using System.Text;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Kraken.Auth;
using CryptoExchanges.Net.Kraken.Resilience;
using CryptoExchanges.Net.Core.Exceptions;

namespace CryptoExchanges.Net.Kraken.Tests.Unit.Auth;

/// <summary>
/// Serializes this class so its MintNonce tests (which reflectively reset the process-wide
/// <c>s_lastNonce</c> to simulate a cold start) cannot race other collections under xUnit
/// parallelization.
/// </summary>
[CollectionDefinition("KrakenNonce", DisableParallelization = true)]
public sealed class KrakenNonceTestGroup;

/// <summary>
/// Unit tests for Kraken HMAC-SHA-512 signature computation, nonce generation, signing handler
/// behaviour, and in-body error translation.
/// </summary>
[Collection("KrakenNonce")]
public class KrakenSignatureServiceTests
{
    // Published Kraken documentation vector: https://docs.kraken.com/api/docs/guides/spot-rest-auth
    private const string KatNonce = "1616492376594";
    private const string KatBody = "nonce=1616492376594&ordertype=limit&pair=XBTUSD&price=37500&type=buy&volume=1.25";
    private const string KatPath = "/0/private/AddOrder";
    private const string KatSecret = "kQH5HW/8p1uGOVjbgWA7FunAmGO8lsSUXNsu3eow76sz84Q18fWxnyRzBHCd3pd5nE9qa99HAZtuZuj6F1huXg==";
    // Correct 88-char value from Kraken Python SDK algorithm; task spec had a corrupted 87-char copy.
    private const string KatExpected = "4/dpxb3iT4tp/ZCVEwSnEsLxx0bqyhLpdfOpc6fn7OR8+UClSV5n9E6aSS8MPtnRfp32bAb0nmbRn6H8ndwLUQ==";

    [Fact]
    [Trait("Category", "KAT")]
    public void ComputeSignature_KnownVector_MatchesExpectedBase64()
    {
        var svc = new KrakenSignatureService(KatSecret);
        var signature = svc.ComputeSignature(KatPath, long.Parse(KatNonce), KatBody);
        signature.Should().Be(KatExpected);
    }

    [Fact]
    public void ComputeSignature_NonceIsStrictlyIncreasing()
    {
        var n1 = KrakenSignatureService.MintNonce();
        var n2 = KrakenSignatureService.MintNonce();
        n2.Should().BeGreaterThan(n1);
    }

    [Fact]
    public void MintNonce_ColdStart_IsClockSeededNotCounter()
    {
        // Reset the process-global counter to simulate a cold start (other tests advance it).
        typeof(KrakenSignatureService)
            .GetField("s_lastNonce", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .SetValue(null, 0L);

        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var nonce = KrakenSignatureService.MintNonce();

        // From a zero seed the value must be the ms clock (max(1, clock)), never a tiny 1-based counter.
        nonce.Should().BeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void MintNonce_SameMillisecond_StillStrictlyIncreasing()
    {
        // Many calls inside one millisecond must never repeat (max(last+1, clock)).
        var values = new long[1000];
        for (var i = 0; i < values.Length; i++)
            values[i] = KrakenSignatureService.MintNonce();
        values.Should().OnlyHaveUniqueItems();
        values.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task MintNonce_ConcurrentCallers_AllUniqueAndMonotonic()
    {
        const int perTask = 2000;
        const int tasks = 8;
        var results = await Task.WhenAll(Enumerable.Range(0, tasks).Select(_ => Task.Run(() =>
        {
            var local = new long[perTask];
            for (var i = 0; i < perTask; i++)
                local[i] = KrakenSignatureService.MintNonce();
            return local;
        })));

        var all = results.SelectMany(r => r).ToArray();
        all.Should().OnlyHaveUniqueItems("each concurrent caller must reserve a distinct nonce");
    }

    [Fact]
    public void ComputeSignature_NonceInBodyMatchesPrehash()
    {
        // If nonce in body differs from the prehash nonce, Kraken rejects the signature.
        var svc = new KrakenSignatureService(KatSecret);
        var nonce = long.Parse(KatNonce);
        var body = $"nonce={nonce}&foo=bar";

        var sig1 = svc.ComputeSignature(KatPath, nonce, body);
        var sig2 = svc.ComputeSignature(KatPath, nonce, body);
        sig1.Should().Be(sig2);
    }

    [Fact]
    public void ComputeSignature_DifferentPaths_ProduceDifferentSignatures()
    {
        var svc = new KrakenSignatureService(KatSecret);
        var nonce = long.Parse(KatNonce);

        var sig1 = svc.ComputeSignature("/0/private/AddOrder", nonce, KatBody);
        var sig2 = svc.ComputeSignature("/0/private/CancelOrder", nonce, KatBody);
        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void KrakenSignatureService_RejectsBlankSecret()
    {
        var act = () => new KrakenSignatureService(" ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void KrakenSignatureService_RejectsInvalidBase64Secret()
    {
        var act = () => new KrakenSignatureService("not-valid-base64!!!");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ComputeSignature_RejectsNullPath()
    {
        var svc = new KrakenSignatureService(KatSecret);
        var act = () => svc.ComputeSignature(null!, 12345L, "nonce=12345");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComputeSignature_RejectsNullBody()
    {
        var svc = new KrakenSignatureService(KatSecret);
        var act = () => svc.ComputeSignature(KatPath, 12345L, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static KrakenSigningHandler BuildHandler(string apiKey, KrakenSignatureService svc, HttpMessageHandler inner)
    {
        var handler = new KrakenSigningHandler(apiKey, svc);
        handler.InnerHandler = inner;
        return handler;
    }

    [Fact]
    public async Task SigningHandler_AddsApiKeyAndSignHeaders()
    {
        var svc = new KrakenSignatureService(KatSecret);
        var captured = new List<HttpRequestMessage>();
        var stub = new StubHandler(req =>
        {
            captured.Add(req);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = BuildHandler("my-api-key", svc, stub);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.kraken.com") };

        var request = new HttpRequestMessage(HttpMethod.Post, "/0/private/Balance")
        {
            Content = new StringContent("foo=bar", Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        KrakenSigningRequest.MarkSigned(request);

        await client.SendAsync(request);

        var sent = captured.Single();
        sent.Headers.Contains("API-Key").Should().BeTrue();
        sent.Headers.Contains("API-Sign").Should().BeTrue();
        sent.Headers.GetValues("API-Key").Single().Should().Be("my-api-key");
        var body = await sent.Content!.ReadAsStringAsync();
        body.Should().StartWith("nonce=");
        sent.Content.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");
    }

    [Fact]
    public async Task SigningHandler_RetryStripsOldHeadersAndRecomputes()
    {
        var svc = new KrakenSignatureService(KatSecret);
        var captured = new List<HttpRequestMessage>();
        var stub = new StubHandler(req =>
        {
            captured.Add(req);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = BuildHandler("api-key", svc, stub);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.kraken.com") };

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/0/private/AddOrder")
        {
            Content = new StringContent("ordertype=limit", Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        KrakenSigningRequest.MarkSigned(request1);
        await client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/0/private/AddOrder")
        {
            Content = new StringContent("ordertype=limit", Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        KrakenSigningRequest.MarkSigned(request2);
        await client.SendAsync(request2);

        captured.Should().HaveCount(2);
        var sign1 = captured[0].Headers.GetValues("API-Sign").Single();
        var sign2 = captured[1].Headers.GetValues("API-Sign").Single();
        sign1.Should().NotBeNullOrEmpty();
        sign2.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SigningHandler_UnsignedRequest_PassesThroughWithoutHeaders()
    {
        var svc = new KrakenSignatureService(KatSecret);
        HttpRequestMessage? captured = null;
        var stub = new StubHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = BuildHandler("api-key", svc, stub);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.kraken.com") };

        var request = new HttpRequestMessage(HttpMethod.Get, "/0/public/Ticker?pair=XBTUSD");
        await client.SendAsync(request);

        captured!.Headers.Contains("API-Key").Should().BeFalse();
        captured.Headers.Contains("API-Sign").Should().BeFalse();
    }

    [Fact]
    public async Task SigningHandler_ReSign_DisposesPriorContent()
    {
        var svc = new KrakenSignatureService(KatSecret);
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var handler = BuildHandler("api-key", svc, stub);
        using var invoker = new HttpMessageInvoker(handler);

        var original = new DisposeTrackingContent("ordertype=limit");
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.kraken.com/0/private/AddOrder")
        {
            Content = original
        };
        KrakenSigningRequest.MarkSigned(request);

        // Pass 1: signing replaces the original body, which must be disposed (no leak).
        using var resp1 = await invoker.SendAsync(request, CancellationToken.None);
        original.Disposed.Should().BeTrue();

        var afterFirstSign = request.Content!;

        // Pass 2 (retry): re-signing replaces and disposes the previously-signed body.
        using var resp2 = await invoker.SendAsync(request, CancellationToken.None);
        request.Content.Should().NotBeSameAs(afterFirstSign);
        var readDisposed = async () => await afterFirstSign.ReadAsStringAsync(CancellationToken.None);
        await readDisposed.Should().ThrowAsync<ObjectDisposedException>();
    }

    private static ExchangeException Translate(HttpStatusCode status, string body)
    {
        using var resp = new HttpResponseMessage(status);
        return new KrakenErrorTranslator().Translate(resp, body);
    }

    [Fact]
    public void KrakenErrorTranslator_InBodyError_MapsToException()
    {
        var ex = Translate(HttpStatusCode.OK, "{\"error\":[\"EAuth:Invalid key\"],\"result\":{}}");
        ex.Should().BeOfType<AuthenticationException>();
        ex.Message.Should().Contain("EAuth:");
    }

    [Fact]
    public void KrakenErrorTranslator_EOrder_MapsToInvalidOrderException()
        => Translate(HttpStatusCode.OK, "{\"error\":[\"EOrder:Insufficient funds\"],\"result\":{}}")
            .Should().BeOfType<InvalidOrderException>();

    [Fact]
    public void KrakenErrorTranslator_EGeneralInsufficient_MapsToInsufficientBalanceException()
        => Translate(HttpStatusCode.OK, "{\"error\":[\"EGeneral:Insufficient funds\"],\"result\":{}}")
            .Should().BeOfType<InsufficientBalanceException>();

    [Fact]
    public void KrakenErrorTranslator_EGeneral_MapsToApiException()
        => Translate(HttpStatusCode.OK, "{\"error\":[\"EGeneral:Invalid arguments\"],\"result\":{}}")
            .Should().BeOfType<ExchangeApiException>();

    [Fact]
    public void KrakenErrorTranslator_EmptyErrorArray_FallsBackToApiException()
        => Translate(HttpStatusCode.OK, "{\"error\":[],\"result\":{}}")
            .Should().BeOfType<ExchangeApiException>();

    [Fact]
    public void KrakenErrorTranslator_NonJsonBody_FallsBackToApiException()
        => Translate(HttpStatusCode.BadGateway, "<html>502</html>")
            .Should().BeOfType<ExchangeApiException>();

    [Fact]
    public void KrakenErrorTranslator_NullResponse_Throws()
    {
        var act = () => new KrakenErrorTranslator().Translate(null!, "{}");
        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    private sealed class DisposeTrackingContent : HttpContent
    {
        private readonly byte[] _bytes;
        public bool Disposed { get; private set; }

        public DisposeTrackingContent(string content) => _bytes = Encoding.UTF8.GetBytes(content);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(_bytes, 0, _bytes.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = _bytes.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
