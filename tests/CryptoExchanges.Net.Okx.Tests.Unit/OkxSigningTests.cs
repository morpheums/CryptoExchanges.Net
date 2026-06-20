using System.Net;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Okx;
using CryptoExchanges.Net.Okx.Auth;
using CryptoExchanges.Net.Okx.Internal;
using CryptoExchanges.Net.Okx.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Auth;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Okx.Tests.Unit;

/// <summary>
/// No-network unit tests for the OKX signature service (base64 HMAC-SHA256 + prehash assembly),
/// ISO-8601 timestamp format, symbol round-trip, value parsers, request validation, error mapping,
/// and time-sync offset arithmetic.
/// </summary>
public class OkxSigningTests
{
    // ── base64 HMAC-SHA256 signature: FIXED known vectors ──

    [Fact]
    public void Sign_ProducesExpectedBase64ForFixedVector()
    {
        // HMAC-SHA256("hello", key="secret") rendered as base64 (the same hash Bybit renders as the
        // hex 88aab3ed...7c0b). Computed independently.
        var sig = new OkxSignatureService("secret").Sign("hello");
        sig.Should().Be("iKqz7ejTrflNJquQ07r9SiCDBww7zOnAFO4EpEOEfAs=");
    }

    [Fact]
    public void Sign_MatchesCoreHmacBase64()
    {
        // The service is exactly Core HmacSignature with base64 output — confirm they agree.
        var viaService = new OkxSignatureService("mysecret").Sign("some-prehash");
        var viaCore = HmacSignature.Compute("mysecret", "some-prehash", SignatureEncoding.Base64);
        viaService.Should().Be(viaCore);
    }

    [Fact]
    public void BuildPrehash_Get_AssemblesTimestampMethodPathQuery()
    {
        const string timestamp = "2026-06-17T12:00:00.000Z";
        // GET prehash: timestamp + METHOD + requestPath(+query) + emptyBody.
        var prehash = OkxSignatureService.BuildPrehash(timestamp, "GET", "/api/v5/market/tickers?instType=SPOT", string.Empty);
        prehash.Should().Be("2026-06-17T12:00:00.000ZGET/api/v5/market/tickers?instType=SPOT");

        var sig = new OkxSignatureService("mysecret").Sign(prehash);
        sig.Should().Be("CTQIPrggIiN7qCvHtwmBM6aqCInZbmHwkf0tH0XjyK4=");
    }

    [Fact]
    public void BuildPrehash_Post_AssemblesTimestampMethodPathBody()
    {
        const string timestamp = "2026-06-17T12:00:00.000Z";
        const string body = "{\"instId\":\"BTC-USDT\",\"tdMode\":\"cash\"}";
        // POST prehash: timestamp + METHOD + requestPath + jsonBody (no query string on the path).
        var prehash = OkxSignatureService.BuildPrehash(timestamp, "POST", "/api/v5/trade/order", body);
        prehash.Should().Be("2026-06-17T12:00:00.000ZPOST/api/v5/trade/order{\"instId\":\"BTC-USDT\",\"tdMode\":\"cash\"}");
    }

    [Fact]
    public void BuildPrehash_UpperCasesMethod()
        => OkxSignatureService.BuildPrehash("1", "get", "/p", string.Empty).Should().Be("1GET/p");

    [Fact]
    public void BuildPrehash_RejectsBlankRequiredFields()
    {
        var act = () => OkxSignatureService.BuildPrehash(" ", "GET", "/p", string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildPrehash_RejectsNullBody()
    {
        var act = () => OkxSignatureService.BuildPrehash("1", "GET", "/p", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FormatTimestamp_ProducesIso8601UtcMillisWithZ()
    {
        var instant = new DateTimeOffset(2026, 6, 17, 12, 0, 0, 123, TimeSpan.Zero);
        OkxSignatureService.FormatTimestamp(instant).Should().Be("2026-06-17T12:00:00.123Z");
    }

    [Fact]
    public void FormatTimestamp_ConvertsToUtc()
    {
        // A +05:00 instant must be rendered in UTC (07:00Z), milliseconds preserved.
        var instant = new DateTimeOffset(2026, 6, 17, 12, 0, 0, 0, TimeSpan.FromHours(5));
        OkxSignatureService.FormatTimestamp(instant).Should().Be("2026-06-17T07:00:00.000Z");
    }

    [Fact]
    public void SignatureService_RejectsBlankSecret()
    {
        var act = () => new OkxSignatureService(" ");
        act.Should().Throw<ArgumentException>();
    }

    // ── Symbol round-trip via SymbolMapper(OkxSymbolFormat.Instance) ──

    [Fact]
    public void Symbol_ToWire_UsesHyphenUpperCase()
    {
        var mapper = new SymbolMapper(OkxSymbolFormat.Instance);
        mapper.ToWire(new Symbol(Asset.Btc, Asset.Usdt)).Should().Be("BTC-USDT");
    }

    [Fact]
    public void Symbol_FromWire_RoundTrips()
    {
        var mapper = new SymbolMapper(OkxSymbolFormat.Instance);
        var btcusdt = new Symbol(Asset.Btc, Asset.Usdt);

        var wire = mapper.ToWire(btcusdt);
        wire.Should().Be("BTC-USDT");
        mapper.FromWire(wire).Should().Be(btcusdt);
    }

    // ── OkxValueParsers invariants + malformed-input rejection ──

    [Theory]
    [InlineData("", 0)]
    [InlineData("0", 0)]
    [InlineData("1.5", 1.5)]
    [InlineData("100.25", 100.25)]
    public void ParseDecimal_ParsesOrReturnsZero(string input, double expected)
        => OkxValueParsers.ParseDecimal(input).Should().Be((decimal)expected);

    [Fact]
    public void ParseDecimal_RejectsMalformedInput()
    {
        var act = () => OkxValueParsers.ParseDecimal("not-a-number");
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("0", null)]
    [InlineData("12.5", 12.5)]
    public void ParseOptionalDecimal_TreatsZeroAndEmptyAsNull(string input, double? expected)
    {
        var result = OkxValueParsers.ParseOptionalDecimal(input);
        if (expected is null)
            result.Should().BeNull();
        else
            result.Should().Be((decimal)expected.Value);
    }

    [Fact]
    public void ParseAssetOrNone_FallsBackToNoneForUnrepresentable()
    {
        OkxValueParsers.ParseAssetOrNone("BTC").Should().Be(Asset.Btc);
        OkxValueParsers.ParseAssetOrNone("bad-ticker!").Should().Be(Asset.None);
        OkxValueParsers.ParseAssetOrNone(null).Should().Be(Asset.None);
    }

    [Theory]
    [InlineData("buy", OrderSide.Buy)]
    [InlineData("sell", OrderSide.Sell)]
    public void ParseOrderSide_MapsKnownValues(string input, OrderSide expected)
        => OkxValueParsers.ParseOrderSide(input).Should().Be(expected);

    [Fact]
    public void ParseOrderSide_RejectsUnknown()
    {
        var act = () => OkxValueParsers.ParseOrderSide("hold");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("limit", OrderType.Limit)]
    [InlineData("post_only", OrderType.Limit)]
    [InlineData("fok", OrderType.Limit)]
    [InlineData("ioc", OrderType.Limit)]
    [InlineData("market", OrderType.Market)]
    public void ParseOrderType_MapsKnownValues(string input, OrderType expected)
        => OkxValueParsers.ParseOrderType(input).Should().Be(expected);

    [Fact]
    public void ParseOrderType_RejectsUnknown()
    {
        var act = () => OkxValueParsers.ParseOrderType("conditional");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("live", OrderStatus.New)]
    [InlineData("partially_filled", OrderStatus.PartiallyFilled)]
    [InlineData("filled", OrderStatus.Filled)]
    [InlineData("canceled", OrderStatus.Canceled)]
    [InlineData("mmp_canceled", OrderStatus.Canceled)]
    [InlineData("something_new", OrderStatus.Unknown)]
    public void ParseOrderStatus_MapsKnownAndUnknown(string input, OrderStatus expected)
        => OkxValueParsers.ParseOrderStatus(input).Should().Be(expected);

    [Theory]
    [InlineData("limit", TimeInForce.Gtc)]
    [InlineData("post_only", TimeInForce.Gtc)]
    [InlineData("ioc", TimeInForce.Ioc)]
    [InlineData("fok", TimeInForce.Fok)]
    [InlineData("market", TimeInForce.Ioc)]
    public void ParseTimeInForce_MapsKnownValues(string input, TimeInForce expected)
        => OkxValueParsers.ParseTimeInForce(input).Should().Be(expected);

    [Fact]
    public void ParseTimeInForce_RejectsUnknown()
    {
        var act = () => OkxValueParsers.ParseTimeInForce("whenever");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParseOrderTypeAndTimeInForce_BothAccept_Market()
    {
        // MARKET-ORDER regression (TASK-013 carry): both parsers must accept "market" so a market
        // order round-trips through the mapping profile without throwing.
        OkxValueParsers.ParseOrderType("market").Should().Be(OrderType.Market);
        OkxValueParsers.ParseTimeInForce("market").Should().Be(TimeInForce.Ioc);
    }

    // ── OkxRequestValidation ──

    [Fact]
    public void ValidateHistoryWindow_AcceptsInRangeLimit()
    {
        var act = () => OkxRequestValidation.ValidateHistoryWindow(100, null, null);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(500)]
    public void ValidateHistoryWindow_RejectsOutOfRangeLimit(int limit)
    {
        var act = () => OkxRequestValidation.ValidateHistoryWindow(limit, null, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidateHistoryWindow_RejectsInvertedWindow()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(-1);
        var act = () => OkxRequestValidation.ValidateHistoryWindow(10, start, end);
        act.Should().Throw<ArgumentException>();
    }

    // ── OkxErrorTranslator: code / HTTP-status → exception type ──

    private static ExchangeException Translate(HttpStatusCode status, string body)
    {
        using var resp = new HttpResponseMessage(status);
        return new OkxErrorTranslator().Translate(resp, body);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"50111\",\"msg\":\"invalid key\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"50113\",\"msg\":\"bad sign\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"50105\",\"msg\":\"wrong passphrase\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.Unauthorized, "{\"code\":\"1\",\"msg\":\"unauth\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"51008\",\"msg\":\"insufficient\"}", typeof(InsufficientBalanceException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"51131\",\"msg\":\"insufficient balance\"}", typeof(InsufficientBalanceException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"51001\",\"msg\":\"instrument not found\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"51020\",\"msg\":\"below min\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"999999\",\"msg\":\"weird\"}", typeof(ExchangeApiException))]
    public void ErrorTranslator_MapsCodes(HttpStatusCode status, string body, Type expected)
        => Translate(status, body).Should().BeOfType(expected);

    [Fact]
    public void ErrorTranslator_Maps429_ToRateLimitWithRetryAfter()
    {
        using var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        resp.Headers.Add("Retry-After", "7");
        var ex = new OkxErrorTranslator().Translate(resp, "{\"code\":\"50011\",\"msg\":\"too many\"}");
        ex.Should().BeOfType<RateLimitExceededException>();
        ((RateLimitExceededException)ex).RetryAfter.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void ErrorTranslator_RateLimitCode_MapsToRateLimit()
        => Translate(HttpStatusCode.OK, "{\"code\":\"50011\",\"msg\":\"rate limit\"}")
            .Should().BeOfType<RateLimitExceededException>();

    [Fact]
    public void ErrorTranslator_SuccessEnvelope_IsNotARateLimitOrAuthError()
    {
        // code == "0" (a STRING) is a success envelope, not an error: must NOT become a typed subtype.
        var ex = Translate(HttpStatusCode.OK, "{\"code\":\"0\",\"msg\":\"\",\"data\":[]}");
        ex.Should().BeOfType<ExchangeApiException>();
        ex.Should().NotBeOfType<RateLimitExceededException>();
        ex.Should().NotBeOfType<AuthenticationException>();
    }

    [Fact]
    public void ErrorTranslator_PerOrderSCode_IsClassifiedWhenTopLevelCodeIsSuccess()
    {
        // OKX order endpoints can return top-level code "0" yet reject an individual order via a
        // non-zero sCode in data[0]; the translator must classify by that per-order code.
        var ex = Translate(HttpStatusCode.OK,
            "{\"code\":\"0\",\"msg\":\"\",\"data\":[{\"ordId\":\"\",\"clOrdId\":\"\",\"sCode\":\"51008\",\"sMsg\":\"insufficient\"}]}");
        ex.Should().BeOfType<InsufficientBalanceException>();
    }

    [Fact]
    public void ErrorTranslator_PerOrderSCodeZero_IsNotAnError()
    {
        // A successful per-order ack (sCode "0") under a top-level "0" must stay a plain ApiException.
        var ex = Translate(HttpStatusCode.OK,
            "{\"code\":\"0\",\"msg\":\"\",\"data\":[{\"ordId\":\"1\",\"sCode\":\"0\",\"sMsg\":\"\"}]}");
        ex.Should().BeOfType<ExchangeApiException>();
        ex.Should().NotBeOfType<AuthenticationException>();
    }

    [Fact]
    public void ErrorTranslator_NonJsonBody_FallsBackToApiException()
        => Translate(HttpStatusCode.BadGateway, "<html>502</html>")
            .Should().BeOfType<ExchangeApiException>();

    [Theory]
    [InlineData("{\"code\":50111,\"msg\":\"num code\"}")] // code as a number (OKX uses strings) -> unparsed -> ApiException
    [InlineData("{\"code\":\"50111\",\"msg\":12345}")]      // msg as a number
    [InlineData("{\"code\":\"50111\",\"msg\":null}")]        // msg null
    public void ErrorTranslator_MalformedFields_DoNotThrow(string body)
    {
        var act = () => Translate(HttpStatusCode.OK, body);
        act.Should().NotThrow();
    }
}
