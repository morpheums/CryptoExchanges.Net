using System.Net;
using Xunit;
using FluentAssertions;
using CryptoExchanges.Net.Bybit;
using CryptoExchanges.Net.Bybit.Auth;
using CryptoExchanges.Net.Bybit.Internal;
using CryptoExchanges.Net.Bybit.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Bybit.Tests.Unit;

/// <summary>
/// No-network unit tests for the Bybit signature service (HMAC-SHA256 + sign-string assembly),
/// symbol round-trip, value parsers, request validation, and time-sync offset arithmetic.
/// </summary>
public class BybitSigningTests
{
    // ── HMAC-SHA256 signature: FIXED known vectors ──

    [Fact]
    public void Sign_ProducesExpectedHexForFixedVector()
    {
        // HMAC-SHA256("hello", key="secret") computed independently; lower-case hex, no separators.
        var sig = new BybitSignatureService("secret").Sign("hello");
        sig.Should().Be("88aab3ede8d3adf94d26ab90d3bafd4a2083070c3bcce9c014ee04a443847c0b");
    }

    [Fact]
    public void Sign_GetSignString_MatchesFixedVector()
    {
        const string timestamp = "1700000000000";
        const string apiKey = "myapikey";
        const string recvWindow = "5000";
        const string query = "category=spot&symbol=BTCUSDT";

        var signString = BybitSignatureService.BuildGetSignString(timestamp, apiKey, recvWindow, query);
        // GET sign-string is timestamp + apiKey + recvWindow + queryString, in that exact order.
        signString.Should().Be("1700000000000myapikey5000category=spot&symbol=BTCUSDT");

        var sig = new BybitSignatureService("mysecret").Sign(signString);
        sig.Should().Be("2bf0bded0ba0a93f5c35dae25d2569c4858f97c779ce8dfa2b808104d26a8596");
    }

    [Fact]
    public void Sign_PostSignString_MatchesFixedVector()
    {
        const string timestamp = "1700000000000";
        const string apiKey = "myapikey";
        const string recvWindow = "5000";
        const string body = "{\"category\":\"spot\",\"symbol\":\"BTCUSDT\"}";

        var signString = BybitSignatureService.BuildPostSignString(timestamp, apiKey, recvWindow, body);
        // POST sign-string is timestamp + apiKey + recvWindow + jsonBody, in that exact order.
        signString.Should().Be("1700000000000myapikey5000{\"category\":\"spot\",\"symbol\":\"BTCUSDT\"}");

        var sig = new BybitSignatureService("mysecret").Sign(signString);
        sig.Should().Be("fb82487b851c53ded56974724fb43c493ccf1da751588bc1812aebf78deaebe7");
    }

    [Fact]
    public void BuildGetSignString_RejectsBlankRequiredFields()
    {
        var act = () => BybitSignatureService.BuildGetSignString(" ", "k", "5000", "q=1");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildPostSignString_AllowsEmptyBodyButNotNull()
    {
        // An empty JSON body is valid (e.g. a parameterless POST); null is not.
        BybitSignatureService.BuildPostSignString("1", "k", "5000", string.Empty)
            .Should().Be("1k5000");
        var act = () => BybitSignatureService.BuildPostSignString("1", "k", "5000", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SignatureService_RejectsBlankSecret()
    {
        var act = () => new BybitSignatureService(" ");
        act.Should().Throw<ArgumentException>();
    }

    // ── Symbol round-trip via SymbolMapper(BybitSymbolFormat.Instance) ──

    [Fact]
    public void Symbol_ToWire_ConcatenatesUpperCaseNoDelimiter()
    {
        var mapper = new SymbolMapper(BybitSymbolFormat.Instance);
        var btcusdt = new Symbol(Asset.Btc, Asset.Usdt);
        mapper.ToWire(btcusdt).Should().Be("BTCUSDT");
    }

    [Fact]
    public void Symbol_FromWire_RoundTrips()
    {
        var mapper = new SymbolMapper(BybitSymbolFormat.Instance);
        var btcusdt = new Symbol(Asset.Btc, Asset.Usdt);

        var wire = mapper.ToWire(btcusdt);
        var resolved = mapper.FromWire(wire);

        resolved.Should().Be(btcusdt);
    }

    // ── BybitValueParsers invariants + malformed-input rejection ──

    [Theory]
    [InlineData("", 0)]
    [InlineData("0", 0)]
    [InlineData("1.5", 1.5)]
    [InlineData("100.25", 100.25)]
    public void ParseDecimal_ParsesOrReturnsZero(string input, double expected)
        => BybitValueParsers.ParseDecimal(input).Should().Be((decimal)expected);

    [Fact]
    public void ParseDecimal_RejectsMalformedInput()
    {
        var act = () => BybitValueParsers.ParseDecimal("not-a-number");
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("0", null)]
    [InlineData("0.00000000", null)]
    [InlineData("12.5", 12.5)]
    public void ParseOptionalDecimal_TreatsZeroAndEmptyAsNull(string input, double? expected)
    {
        var result = BybitValueParsers.ParseOptionalDecimal(input);
        if (expected is null)
            result.Should().BeNull();
        else
            result.Should().Be((decimal)expected.Value);
    }

    [Fact]
    public void ParseAssetOrNone_FallsBackToNoneForUnrepresentable()
    {
        BybitValueParsers.ParseAssetOrNone("BTC").Should().Be(Asset.Btc);
        BybitValueParsers.ParseAssetOrNone("bad-ticker!").Should().Be(Asset.None);
        BybitValueParsers.ParseAssetOrNone(null).Should().Be(Asset.None);
    }

    [Theory]
    [InlineData("Buy", OrderSide.Buy)]
    [InlineData("Sell", OrderSide.Sell)]
    public void ParseOrderSide_MapsKnownValues(string input, OrderSide expected)
        => BybitValueParsers.ParseOrderSide(input).Should().Be(expected);

    [Fact]
    public void ParseOrderSide_RejectsUnknown()
    {
        var act = () => BybitValueParsers.ParseOrderSide("Hold");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("Limit", OrderType.Limit)]
    [InlineData("Market", OrderType.Market)]
    public void ParseOrderType_MapsKnownValues(string input, OrderType expected)
        => BybitValueParsers.ParseOrderType(input).Should().Be(expected);

    [Fact]
    public void ParseOrderType_RejectsUnknown()
    {
        var act = () => BybitValueParsers.ParseOrderType("Conditional");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("New", OrderStatus.New)]
    [InlineData("Untriggered", OrderStatus.New)]
    [InlineData("PartiallyFilled", OrderStatus.PartiallyFilled)]
    [InlineData("Filled", OrderStatus.Filled)]
    [InlineData("Cancelled", OrderStatus.Canceled)]
    [InlineData("PartiallyFilledCanceled", OrderStatus.Canceled)]
    [InlineData("Rejected", OrderStatus.Rejected)]
    [InlineData("Triggered", OrderStatus.PendingNew)]
    [InlineData("SomethingNew", OrderStatus.Unknown)]
    public void ParseOrderStatus_MapsKnownAndUnknown(string input, OrderStatus expected)
        => BybitValueParsers.ParseOrderStatus(input).Should().Be(expected);

    [Theory]
    [InlineData("GTC", TimeInForce.Gtc)]
    [InlineData("PostOnly", TimeInForce.Gtc)]
    [InlineData("IOC", TimeInForce.Ioc)]
    [InlineData("FOK", TimeInForce.Fok)]
    public void ParseTimeInForce_MapsKnownValues(string input, TimeInForce expected)
        => BybitValueParsers.ParseTimeInForce(input).Should().Be(expected);

    [Fact]
    public void ParseTimeInForce_RejectsUnknown()
    {
        var act = () => BybitValueParsers.ParseTimeInForce("WHENEVER");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── BybitRequestValidation ──

    [Fact]
    public void ValidateHistoryWindow_AcceptsInRangeLimit()
    {
        var act = () => BybitRequestValidation.ValidateHistoryWindow(50, null, null);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    [InlineData(500)]
    public void ValidateHistoryWindow_RejectsOutOfRangeLimit(int limit)
    {
        var act = () => BybitRequestValidation.ValidateHistoryWindow(limit, null, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidateHistoryWindow_RejectsInvertedWindow()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(-1);
        var act = () => BybitRequestValidation.ValidateHistoryWindow(10, start, end);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateHistoryWindow_RejectsWindowOverSevenDays()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(8);
        var act = () => BybitRequestValidation.ValidateHistoryWindow(10, start, end);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateHistoryWindow_AcceptsSevenDayWindow()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(7);
        var act = () => BybitRequestValidation.ValidateHistoryWindow(10, start, end);
        act.Should().NotThrow();
    }

    // ── BybitTimeSync ──

    [Fact]
    public void ComputeOffset_ReturnsServerMinusLocal()
    {
        BybitTimeSync.ComputeOffset(serverTimeMs: 10_000, localNowMs: 8_000).Should().Be(2_000);
        BybitTimeSync.ComputeOffset(serverTimeMs: 8_000, localNowMs: 10_000).Should().Be(-2_000);
    }

    [Fact]
    public void ApplyOffset_WritesIntoHolderAndReturnsOffset()
    {
        var holder = new long[] { 0L };
        var written = BybitTimeSync.ApplyOffset(serverTimeMs: 12_345, localNowMs: 12_000, holder);

        written.Should().Be(345);
        holder[0].Should().Be(345);
    }

    [Fact]
    public void ApplyOffset_RejectsZeroLengthHolder()
    {
        var act = () => BybitTimeSync.ApplyOffset(1, 0, []);
        act.Should().Throw<ArgumentException>();
    }

    // ── BybitErrorTranslator: retCode / HTTP-status → exception type ──

    private static ExchangeException Translate(HttpStatusCode status, string body)
    {
        using var resp = new HttpResponseMessage(status);
        return new BybitErrorTranslator().Translate(resp, body);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"retCode\":10003,\"retMsg\":\"invalid key\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"retCode\":10004,\"retMsg\":\"bad sign\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.Unauthorized, "{\"retCode\":1,\"retMsg\":\"unauth\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"retCode\":110007,\"retMsg\":\"insufficient\"}", typeof(InsufficientBalanceException))]
    [InlineData(HttpStatusCode.OK, "{\"retCode\":170131,\"retMsg\":\"insufficient spot\"}", typeof(InsufficientBalanceException))]
    [InlineData(HttpStatusCode.OK, "{\"retCode\":110001,\"retMsg\":\"order not found\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.OK, "{\"retCode\":170140,\"retMsg\":\"below min\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.OK, "{\"retCode\":999999,\"retMsg\":\"weird\"}", typeof(ExchangeApiException))]
    public void ErrorTranslator_MapsCodes(HttpStatusCode status, string body, Type expected)
        => Translate(status, body).Should().BeOfType(expected);

    [Fact]
    public void ErrorTranslator_Maps429_ToRateLimitWithRetryAfter()
    {
        using var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        resp.Headers.Add("Retry-After", "7");
        var ex = new BybitErrorTranslator().Translate(resp, "{\"retCode\":10006,\"retMsg\":\"too many\"}");
        ex.Should().BeOfType<RateLimitExceededException>();
        ((RateLimitExceededException)ex).RetryAfter.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void ErrorTranslator_RateLimitRetCode_MapsToRateLimit()
        => Translate(HttpStatusCode.OK, "{\"retCode\":10018,\"retMsg\":\"ip limit\"}")
            .Should().BeOfType<RateLimitExceededException>();

    [Fact]
    public void ErrorTranslator_SuccessEnvelope_IsNotARateLimitOrAuthError()
    {
        // retCode == 0 is a success envelope, not an error: it must NOT become a typed error subtype.
        var ex = Translate(HttpStatusCode.OK, "{\"retCode\":0,\"retMsg\":\"OK\"}");
        ex.Should().BeOfType<ExchangeApiException>();
        ex.Should().NotBeOfType<RateLimitExceededException>();
        ex.Should().NotBeOfType<AuthenticationException>();
    }

    [Fact]
    public void ErrorTranslator_NonJsonBody_FallsBackToApiException()
        => Translate(HttpStatusCode.BadGateway, "<html>502</html>")
            .Should().BeOfType<ExchangeApiException>();

    [Theory]
    [InlineData("{\"retCode\":10003,\"retMsg\":12345}")]   // retMsg as a number
    [InlineData("{\"retCode\":10003,\"retMsg\":{\"x\":1}}")] // retMsg as an object
    [InlineData("{\"retCode\":10003,\"retMsg\":null}")]      // retMsg null
    public void ErrorTranslator_NonStringRetMsg_DoesNotThrow(string body)
    {
        // JsonElement.GetString() throws InvalidOperationException (not JsonException) for a
        // non-string retMsg; the translator must guard ValueKind and still classify by retCode.
        var act = () => Translate(HttpStatusCode.OK, body);
        act.Should().NotThrow();
        Translate(HttpStatusCode.OK, body).Should().BeOfType<AuthenticationException>();
    }
}
