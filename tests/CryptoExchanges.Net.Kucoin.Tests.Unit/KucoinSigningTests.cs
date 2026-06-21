using System.Net;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Kucoin.Auth;
using CryptoExchanges.Net.Kucoin.Resilience;
using CryptoExchanges.Net.Core.Auth;
using CryptoExchanges.Net.Core.Exceptions;

namespace CryptoExchanges.Net.Kucoin.Tests.Unit;

/// <summary>
/// No-network unit tests for the KuCoin KC-API passphrase-v2 signature service (base64
/// HMAC-SHA256 + prehash assembly + passphrase signing), Unix-ms timestamp format, signing
/// handler mark-and-strip behavior, and error mapping.
/// </summary>
public class KucoinSigningTests
{
    // ── base64 HMAC-SHA256 signature: FIXED known vectors ──

    [Fact]
    public void Sign_ProducesExpectedBase64ForFixedVector()
    {
        // HMAC-SHA256("hello", key="secret") rendered as base64. Same underlying primitive as OKX.
        // Computed independently: base64(HMAC-SHA256(b"secret", b"hello")).
        var sig = new KucoinSignatureService("secret").Sign("hello");
        sig.Should().Be("iKqz7ejTrflNJquQ07r9SiCDBww7zOnAFO4EpEOEfAs=");
    }

    [Fact]
    public void Sign_MatchesCoreHmacBase64()
    {
        // KucoinSignatureService.Sign is exactly Core HmacSignature with base64 output.
        var viaService = new KucoinSignatureService("mysecret").Sign("some-prehash");
        var viaCore = HmacSignature.Compute("mysecret", "some-prehash", SignatureEncoding.Base64);
        viaService.Should().Be(viaCore);
    }

    [Fact]
    public void SignPassphrase_ProducesExpectedBase64ForFixedVector()
    {
        // KuCoin passphrase-v2: KC-API-PASSPHRASE = base64(HMAC-SHA256(secret, passphrase)).
        // Computed independently: base64(HMAC-SHA256(b"secret", b"mypassphrase")).
        var signed = new KucoinSignatureService("secret").SignPassphrase("mypassphrase");
        signed.Should().Be("jrrTuwAqT2eYfpU/Rg8OhXN4674gVXo5b7JJOivIAP0=");
    }

    [Fact]
    public void SignPassphrase_DiffersFromRawPassphrase()
    {
        // The signed passphrase must NEVER equal the raw passphrase — it is always HMAC-SHA256 + base64.
        var signed = new KucoinSignatureService("secret").SignPassphrase("mypassphrase");
        signed.Should().NotBe("mypassphrase");
    }

    [Fact]
    public void SignPassphrase_RejectsBlankPassphrase()
    {
        var act = () => new KucoinSignatureService("secret").SignPassphrase(" ");
        act.Should().Throw<ArgumentException>();
    }

    // ── BuildPrehash ──

    [Fact]
    public void BuildPrehash_Get_AssemblesTimestampMethodPathQuery()
    {
        // Unix-ms timestamp + METHOD + requestPath (with query) + empty body.
        // Known instant: 2026-06-17T12:00:00Z → 1781697600000 ms.
        const string timestamp = "1781697600000";
        var prehash = KucoinSignatureService.BuildPrehash(timestamp, "GET", "/api/v1/market/allTickers", string.Empty);
        prehash.Should().Be("1781697600000GET/api/v1/market/allTickers");

        // Verify the signature over this prehash is stable (golden value computed independently).
        var sig = new KucoinSignatureService("secret").Sign(prehash);
        sig.Should().Be("VytcCBIRyebyHbVlPizpbkRvLymxUb19cMRy1WAmZpw=");
    }

    [Fact]
    public void BuildPrehash_Post_AssemblesTimestampMethodPathBody()
    {
        const string timestamp = "1781697600000";
        const string body = "{\"symbol\":\"BTC-USDT\"}";
        // POST prehash: timestamp + METHOD + requestPath + jsonBody (no query string on the path).
        var prehash = KucoinSignatureService.BuildPrehash(timestamp, "POST", "/api/v1/orders", body);
        prehash.Should().Be("1781697600000POST/api/v1/orders{\"symbol\":\"BTC-USDT\"}");

        var sig = new KucoinSignatureService("secret").Sign(prehash);
        sig.Should().Be("MsBAr9T4phZdosux5u9t3rILTNkahjP5/MoV7UrnHU0=");
    }

    [Fact]
    public void BuildPrehash_UpperCasesMethod()
        => KucoinSignatureService.BuildPrehash("1", "get", "/p", string.Empty).Should().Be("1GET/p");

    [Fact]
    public void BuildPrehash_RejectsBlankRequiredFields()
    {
        var act = () => KucoinSignatureService.BuildPrehash(" ", "GET", "/p", string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildPrehash_RejectsNullBody()
    {
        var act = () => KucoinSignatureService.BuildPrehash("1", "GET", "/p", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── FormatTimestamp: Unix epoch milliseconds (NOT ISO-8601) ──

    [Fact]
    public void FormatTimestamp_ProducesUnixEpochMillisecondsNotIso8601()
    {
        var instant = new DateTimeOffset(2026, 6, 17, 12, 0, 0, 0, TimeSpan.Zero);
        var result = KucoinSignatureService.FormatTimestamp(instant);

        // Must be the exact Unix epoch ms value — NOT an ISO-8601 string.
        result.Should().Be("1781697600000");
        result.Should().NotContain("T");
        result.Should().NotContain("Z");
        result.Should().NotContain("-");
    }

    [Fact]
    public void FormatTimestamp_ConvertsToUtc()
    {
        // A +05:00 instant must be converted to UTC before computing Unix ms.
        var instant = new DateTimeOffset(2026, 6, 17, 17, 0, 0, 0, TimeSpan.FromHours(5));
        // 2026-06-17T12:00:00Z in UTC → same epoch ms.
        KucoinSignatureService.FormatTimestamp(instant).Should().Be("1781697600000");
    }

    [Fact]
    public void FormatTimestamp_ReturnsNumericString()
    {
        var result = KucoinSignatureService.FormatTimestamp(DateTimeOffset.UtcNow);
        long.TryParse(result, out var parsed).Should().BeTrue("FormatTimestamp must return a parseable long string");
        parsed.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SignatureService_RejectsBlankSecret()
    {
        var act = () => new KucoinSignatureService(" ");
        act.Should().Throw<ArgumentException>();
    }

    // ── KucoinSigningRequest mark-and-strip marker ──

    [Fact]
    public void SigningRequest_MarkSigned_SetsFlag()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.kucoin.com/api/v1/market/tickers");
        KucoinSigningRequest.MarkSigned(request);
        KucoinSigningRequest.IsSigned(request).Should().BeTrue();
    }

    [Fact]
    public void SigningRequest_UnmarkedRequest_IsNotSigned()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.kucoin.com/api/v1/market/tickers");
        KucoinSigningRequest.IsSigned(request).Should().BeFalse();
    }

    // ── KucoinSigningHandler behavior ──

    private static KucoinSigningHandler BuildHandler(
        string apiKey = "my-api-key",
        string passphrase = "my-passphrase",
        string secret = "my-secret",
        Func<long>? timeOffset = null)
    {
        var svc = new KucoinSignatureService(secret);
        var handler = new KucoinSigningHandler(apiKey, passphrase, svc, timeOffset ?? (() => 0L));
        handler.InnerHandler = new NoOpHandler();
        return handler;
    }

    [Fact]
    public async Task Handler_UnsignedRequest_PassesThroughWithNoKcApiHeaders()
    {
        using var handler = BuildHandler();
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.kucoin.com/api/v1/time");
        // NOT marked as signed.

        using var _ = await client.SendAsync(request);

        request.Headers.Contains("KC-API-KEY").Should().BeFalse();
        request.Headers.Contains("KC-API-SIGN").Should().BeFalse();
        request.Headers.Contains("KC-API-TIMESTAMP").Should().BeFalse();
        request.Headers.Contains("KC-API-PASSPHRASE").Should().BeFalse();
        request.Headers.Contains("KC-API-KEY-VERSION").Should().BeFalse();
    }

    [Fact]
    public async Task Handler_SignedRequest_SetsAllFiveKcApiHeaders()
    {
        using var handler = BuildHandler();
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.kucoin.com/api/v1/accounts");
        KucoinSigningRequest.MarkSigned(request);

        using var _ = await client.SendAsync(request);

        request.Headers.Contains("KC-API-KEY").Should().BeTrue();
        request.Headers.Contains("KC-API-SIGN").Should().BeTrue();
        request.Headers.Contains("KC-API-TIMESTAMP").Should().BeTrue();
        request.Headers.Contains("KC-API-PASSPHRASE").Should().BeTrue();
        request.Headers.Contains("KC-API-KEY-VERSION").Should().BeTrue();

        request.Headers.GetValues("KC-API-KEY").Single().Should().Be("my-api-key");
        request.Headers.GetValues("KC-API-KEY-VERSION").Single().Should().Be("2");
    }

    [Fact]
    public async Task Handler_SignedRequest_PassphraseHeaderIsSignedNotRaw()
    {
        using var handler = BuildHandler(passphrase: "testpassphrase", secret: "testsecret");
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.kucoin.com/api/v1/accounts");
        KucoinSigningRequest.MarkSigned(request);

        using var _ = await client.SendAsync(request);

        var passphraseHeader = request.Headers.GetValues("KC-API-PASSPHRASE").Single();
        // The passphrase header must NOT be the raw passphrase.
        passphraseHeader.Should().NotBe("testpassphrase");
        // It should be the base64 HMAC-SHA256 of the passphrase under the secret.
        var expected = HmacSignature.Compute("testsecret", "testpassphrase", SignatureEncoding.Base64);
        passphraseHeader.Should().Be(expected);
    }

    [Fact]
    public async Task Handler_RetrySimulation_YieldsDifferentTimestampsAndNoDuplicateHeaders()
    {
        // Simulate two separate attempts with different time offsets to verify: (a) timestamps differ,
        // (b) no duplicate KC-API-* headers (mark-and-strip ensures clean re-sign).
        long offsetMs = 0L;
        using var handler = BuildHandler(timeOffset: () => offsetMs);
        using var client = new HttpClient(handler);

        // First attempt — offset 0
        using var request1 = new HttpRequestMessage(HttpMethod.Get, "https://api.kucoin.com/api/v1/accounts");
        KucoinSigningRequest.MarkSigned(request1);
        using var _1 = await client.SendAsync(request1);
        var ts1 = request1.Headers.GetValues("KC-API-TIMESTAMP").Single();

        // Second attempt — offset 60,000 ms (simulated delay)
        offsetMs = 60_000L;
        using var request2 = new HttpRequestMessage(HttpMethod.Get, "https://api.kucoin.com/api/v1/accounts");
        KucoinSigningRequest.MarkSigned(request2);
        using var _2 = await client.SendAsync(request2);
        var ts2 = request2.Headers.GetValues("KC-API-TIMESTAMP").Single();

        // Timestamps must differ.
        ts1.Should().NotBe(ts2);

        // No header should appear more than once (mark-and-strip).
        request2.Headers.GetValues("KC-API-KEY").Should().HaveCount(1);
        request2.Headers.GetValues("KC-API-SIGN").Should().HaveCount(1);
        request2.Headers.GetValues("KC-API-TIMESTAMP").Should().HaveCount(1);
        request2.Headers.GetValues("KC-API-PASSPHRASE").Should().HaveCount(1);
        request2.Headers.GetValues("KC-API-KEY-VERSION").Should().HaveCount(1);
    }

    [Fact]
    public async Task Handler_MissingApiKey_Throws()
    {
        using var handler = BuildHandler(apiKey: "");
        using var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.kucoin.com/api/v1/accounts");
        KucoinSigningRequest.MarkSigned(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));
        request.Dispose();
    }

    [Fact]
    public async Task Handler_MissingPassphrase_Throws()
    {
        using var handler = BuildHandler(passphrase: "");
        using var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.kucoin.com/api/v1/accounts");
        KucoinSigningRequest.MarkSigned(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));
        request.Dispose();
    }

    // ── KucoinErrorTranslator: code / HTTP-status → exception type ──

    private static ExchangeException Translate(HttpStatusCode status, string body)
    {
        using var resp = new HttpResponseMessage(status);
        return new KucoinErrorTranslator().Translate(resp, body);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"400003\",\"msg\":\"invalid key\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"400005\",\"msg\":\"invalid signature\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"400004\",\"msg\":\"invalid passphrase\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.Unauthorized, "{\"code\":\"1\",\"msg\":\"unauth\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.Forbidden, "{\"code\":\"1\",\"msg\":\"forbidden\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"900014\",\"msg\":\"insufficient\"}", typeof(InsufficientBalanceException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"900001\",\"msg\":\"invalid symbol\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"900006\",\"msg\":\"amount too small\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"999999\",\"msg\":\"weird\"}", typeof(ExchangeApiException))]
    public void ErrorTranslator_MapsCodes(HttpStatusCode status, string body, Type expected)
        => Translate(status, body).Should().BeOfType(expected);

    [Fact]
    public void ErrorTranslator_Maps429_ToRateLimitWithRetryAfter()
    {
        using var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        resp.Headers.Add("Retry-After", "7");
        var ex = new KucoinErrorTranslator().Translate(resp, "{\"code\":\"429000\",\"msg\":\"too many\"}");
        ex.Should().BeOfType<RateLimitExceededException>();
        ((RateLimitExceededException)ex).RetryAfter.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void ErrorTranslator_SuccessEnvelope_IsNotATypedError()
    {
        // code == "200000" (a STRING) is a success envelope: must NOT become a typed subtype.
        var ex = Translate(HttpStatusCode.OK, "{\"code\":\"200000\",\"msg\":\"\"}");
        ex.Should().BeOfType<ExchangeApiException>();
        ex.Should().NotBeOfType<RateLimitExceededException>();
        ex.Should().NotBeOfType<AuthenticationException>();
    }

    [Fact]
    public void ErrorTranslator_NonJsonBody_FallsBackToApiException()
        => Translate(HttpStatusCode.BadGateway, "<html>502</html>")
            .Should().BeOfType<ExchangeApiException>();

    [Theory]
    [InlineData("{\"code\":400003,\"msg\":\"num code\"}")] // code as a number -> unparsed -> ApiException
    [InlineData("{\"code\":\"400003\",\"msg\":12345}")]      // msg as a number
    [InlineData("{\"code\":\"400003\",\"msg\":null}")]        // msg null
    public void ErrorTranslator_MalformedFields_DoNotThrow(string body)
    {
        var act = () => Translate(HttpStatusCode.OK, body);
        act.Should().NotThrow();
    }

    /// <summary>Minimal inner handler that returns a 200 OK without touching the network.</summary>
    private sealed class NoOpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
