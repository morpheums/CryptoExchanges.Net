using System.Net;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Bitget;
using CryptoExchanges.Net.Bitget.Auth;
using CryptoExchanges.Net.Bitget.Internal;
using CryptoExchanges.Net.Bitget.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Auth;
using CryptoExchanges.Net.Core.Exceptions;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Enums;

namespace CryptoExchanges.Net.Bitget.Tests.Unit;

/// <summary>
/// No-network unit tests for the Bitget signature service (base64 HMAC-SHA256 + prehash assembly),
/// epoch-ms timestamp format, symbol round-trip, value parsers, request validation, error mapping,
/// and time-sync offset arithmetic.
/// </summary>
public class BitgetSigningTests
{
    // ── base64 HMAC-SHA256 signature: FIXED known vectors ──

    [Fact]
    public void Sign_ProducesExpectedBase64ForFixedVector()
    {
        // HMAC-SHA256("hello", key="secret") rendered as base64 (the same hash OKX renders identically).
        var sig = new BitgetSignatureService("secret").Sign("hello");
        sig.Should().Be("iKqz7ejTrflNJquQ07r9SiCDBww7zOnAFO4EpEOEfAs=");
    }

    [Fact]
    public void Sign_MatchesCoreHmacBase64()
    {
        // The service is exactly Core HmacSignature with base64 output — confirm they agree.
        var viaService = new BitgetSignatureService("mysecret").Sign("some-prehash");
        var viaCore = HmacSignature.Compute("mysecret", "some-prehash", SignatureEncoding.Base64);
        viaService.Should().Be(viaCore);
    }

    [Fact]
    public void BuildPrehash_Get_WithQuery_AssemblesTimestampMethodPathQuery()
    {
        const string timestamp = "1700000000000";
        // GET prehash WITH query: timestamp + METHOD + requestPath + '?' + queryString + emptyBody.
        var prehash = BitgetSignatureService.BuildPrehash(timestamp, "GET", "/api/v2/spot/market/tickers", "symbol=BTCUSDT", string.Empty);
        prehash.Should().Be("1700000000000GET/api/v2/spot/market/tickers?symbol=BTCUSDT");
    }

    [Fact]
    public void BuildPrehash_Get_WithoutQuery_OmitsQuestionMark()
    {
        const string timestamp = "1700000000000";
        // GET prehash WITHOUT query: NO trailing '?' is added when the query string is empty.
        var prehash = BitgetSignatureService.BuildPrehash(timestamp, "GET", "/api/v2/spot/account/assets", string.Empty, string.Empty);
        prehash.Should().Be("1700000000000GET/api/v2/spot/account/assets");
        prehash.Should().NotContain("?");
    }

    [Fact]
    public void BuildPrehash_Post_AssemblesTimestampMethodPathBody()
    {
        const string timestamp = "1700000000000";
        const string body = "{\"symbol\":\"BTCUSDT\",\"side\":\"buy\"}";
        // POST prehash: timestamp + METHOD + requestPath + jsonBody (no query string).
        var prehash = BitgetSignatureService.BuildPrehash(timestamp, "POST", "/api/v2/spot/trade/place-order", string.Empty, body);
        prehash.Should().Be("1700000000000POST/api/v2/spot/trade/place-order{\"symbol\":\"BTCUSDT\",\"side\":\"buy\"}");
    }

    [Fact]
    public void BuildPrehash_UpperCasesMethod()
        => BitgetSignatureService.BuildPrehash("1", "get", "/p", string.Empty, string.Empty).Should().Be("1GET/p");

    [Fact]
    public void BuildPrehash_RejectsBlankRequiredFields()
    {
        var act = () => BitgetSignatureService.BuildPrehash(" ", "GET", "/p", string.Empty, string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildPrehash_RejectsNullQueryOrBody()
    {
        var actQuery = () => BitgetSignatureService.BuildPrehash("1", "GET", "/p", null!, string.Empty);
        actQuery.Should().Throw<ArgumentNullException>();
        var actBody = () => BitgetSignatureService.BuildPrehash("1", "GET", "/p", string.Empty, null!);
        actBody.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FormatTimestamp_ProducesEpochMillis()
    {
        var instant = DateTimeOffset.FromUnixTimeMilliseconds(1700000000123);
        BitgetSignatureService.FormatTimestamp(instant).Should().Be("1700000000123");
    }

    [Fact]
    public void SignatureService_RejectsBlankSecret()
    {
        var act = () => new BitgetSignatureService(" ");
        act.Should().Throw<ArgumentException>();
    }

    // ── Symbol round-trip via SymbolMapper(BitgetSymbolFormat.Instance) ──

    [Fact]
    public void Symbol_ToWire_UsesDelimiterlessUpperCase()
    {
        var mapper = new SymbolMapper(BitgetSymbolFormat.Instance);
        mapper.ToWire(new Symbol(Asset.Btc, Asset.Usdt)).Should().Be("BTCUSDT");
    }

    [Fact]
    public void Symbol_FromWire_RoundTrips()
    {
        var mapper = new SymbolMapper(BitgetSymbolFormat.Instance);
        var btcusdt = new Symbol(Asset.Btc, Asset.Usdt);

        var wire = mapper.ToWire(btcusdt);
        wire.Should().Be("BTCUSDT");
        mapper.FromWire(wire).Should().Be(btcusdt);
    }

    // ── BitgetValueParsers invariants + malformed-input rejection ──

    [Theory]
    [InlineData("", 0)]
    [InlineData("0", 0)]
    [InlineData("1.5", 1.5)]
    [InlineData("100.25", 100.25)]
    public void ParseDecimal_ParsesOrReturnsZero(string input, double expected)
        => BitgetValueParsers.ParseDecimal(input).Should().Be((decimal)expected);

    [Fact]
    public void ParseDecimal_RejectsMalformedInput()
    {
        var act = () => BitgetValueParsers.ParseDecimal("not-a-number");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseAssetOrNone_FallsBackToNoneForUnrepresentable()
    {
        BitgetValueParsers.ParseAssetOrNone("BTC").Should().Be(Asset.Btc);
        BitgetValueParsers.ParseAssetOrNone("bad-ticker!").Should().Be(Asset.None);
        BitgetValueParsers.ParseAssetOrNone(null).Should().Be(Asset.None);
    }

    [Theory]
    [InlineData("buy", OrderSide.Buy)]
    [InlineData("sell", OrderSide.Sell)]
    public void ParseOrderSide_MapsKnownValues(string input, OrderSide expected)
        => BitgetValueParsers.ParseOrderSide(input).Should().Be(expected);

    [Fact]
    public void ParseOrderSide_RejectsUnknown()
    {
        var act = () => BitgetValueParsers.ParseOrderSide("hold");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("limit", OrderType.Limit)]
    [InlineData("market", OrderType.Market)]
    public void ParseOrderType_MapsKnownValues(string input, OrderType expected)
        => BitgetValueParsers.ParseOrderType(input).Should().Be(expected);

    [Fact]
    public void ParseOrderType_RejectsUnknown()
    {
        var act = () => BitgetValueParsers.ParseOrderType("conditional");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("init", OrderStatus.New)]
    [InlineData("new", OrderStatus.New)]
    [InlineData("live", OrderStatus.New)]
    [InlineData("partially_filled", OrderStatus.PartiallyFilled)]
    [InlineData("filled", OrderStatus.Filled)]
    [InlineData("cancelled", OrderStatus.Canceled)]
    [InlineData("something_new", OrderStatus.Unknown)]
    public void ParseOrderStatus_MapsKnownAndUnknown(string input, OrderStatus expected)
        => BitgetValueParsers.ParseOrderStatus(input).Should().Be(expected);

    [Theory]
    [InlineData("gtc", TimeInForce.Gtc)]
    [InlineData("post_only", TimeInForce.Gtc)]
    [InlineData("ioc", TimeInForce.Ioc)]
    [InlineData("fok", TimeInForce.Fok)]
    public void ParseTimeInForce_MapsKnownValues(string input, TimeInForce expected)
        => BitgetValueParsers.ParseTimeInForce(input).Should().Be(expected);

    [Fact]
    public void ParseTimeInForce_RejectsUnknown()
    {
        var act = () => BitgetValueParsers.ParseTimeInForce("whenever");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParseOrderType_AcceptsMarket()
    {
        // MARKET-ORDER regression (TASK-013/020 carry): the parser must accept "market" so a market
        // order round-trips through the mapping profile without throwing.
        BitgetValueParsers.ParseOrderType("market").Should().Be(OrderType.Market);
    }

    [Theory]
    [InlineData("", 0L)]
    [InlineData("not-ms", 0L)]
    [InlineData("1700000000000", 1700000000000L)]
    public void ParseMs_ParsesOrReturnsZero(string input, long expected)
        => BitgetValueParsers.ParseMs(input).Should().Be(expected);

    // ── BitgetRequestValidation ──

    [Fact]
    public void ValidateHistoryWindow_AcceptsInRangeLimit()
    {
        var act = () => BitgetRequestValidation.ValidateHistoryWindow(100, null, null);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(500)]
    public void ValidateHistoryWindow_RejectsOutOfRangeLimit(int limit)
    {
        var act = () => BitgetRequestValidation.ValidateHistoryWindow(limit, null, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidateHistoryWindow_RejectsInvertedWindow()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(-1);
        var act = () => BitgetRequestValidation.ValidateHistoryWindow(10, start, end);
        act.Should().Throw<ArgumentException>();
    }

    // ── BitgetErrorTranslator: code / HTTP-status → exception type ──

    private static ExchangeException Translate(HttpStatusCode status, string body)
    {
        using var resp = new HttpResponseMessage(status);
        return new BitgetErrorTranslator().Translate(resp, body);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"40037\",\"msg\":\"apikey does not exist\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"40006\",\"msg\":\"invalid sign\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"40012\",\"msg\":\"apikey/passphrase incorrect\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.Unauthorized, "{\"code\":\"1\",\"msg\":\"unauth\"}", typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"43012\",\"msg\":\"insufficient balance\"}", typeof(InsufficientBalanceException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"43011\",\"msg\":\"insufficient funds\"}", typeof(InsufficientBalanceException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"43001\",\"msg\":\"order not found\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"45110\",\"msg\":\"below min\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"400172\",\"msg\":\"invalid symbol\"}", typeof(InvalidOrderException))]
    [InlineData(HttpStatusCode.OK, "{\"code\":\"999999\",\"msg\":\"weird\"}", typeof(ExchangeApiException))]
    public void ErrorTranslator_MapsCodes(HttpStatusCode status, string body, Type expected)
        => Translate(status, body).Should().BeOfType(expected);

    [Fact]
    public void ErrorTranslator_Maps429_ToRateLimitWithRetryAfter()
    {
        using var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        resp.Headers.Add("Retry-After", "7");
        var ex = new BitgetErrorTranslator().Translate(resp, "{\"code\":\"429\",\"msg\":\"too many\"}");
        ex.Should().BeOfType<RateLimitExceededException>();
        ((RateLimitExceededException)ex).RetryAfter.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void ErrorTranslator_SuccessCode_IsNotAnError()
    {
        // code == "00000" (a STRING) is Bitget's success envelope, not an error: it must NOT become a
        // typed subtype (this is the Bitget-specific success-code carry-in).
        var ex = Translate(HttpStatusCode.OK, "{\"code\":\"00000\",\"msg\":\"success\",\"data\":[]}");
        ex.Should().BeOfType<ExchangeApiException>();
        ex.Should().NotBeOfType<RateLimitExceededException>();
        ex.Should().NotBeOfType<AuthenticationException>();
    }

    [Fact]
    public void ErrorTranslator_NonJsonBody_FallsBackToApiException()
        => Translate(HttpStatusCode.BadGateway, "<html>502</html>")
            .Should().BeOfType<ExchangeApiException>();

    [Theory]
    [InlineData("{\"code\":40037,\"msg\":\"num code\"}")] // code as a number (Bitget uses strings) -> unparsed -> ApiException
    [InlineData("{\"code\":\"40037\",\"msg\":12345}")]      // msg as a number
    [InlineData("{\"code\":\"40037\",\"msg\":null}")]        // msg null
    public void ErrorTranslator_MalformedFields_DoNotThrow(string body)
    {
        var act = () => Translate(HttpStatusCode.OK, body);
        act.Should().NotThrow();
    }

    // ── Time-sync (Core ExchangeTimeSync) offset arithmetic shared by Bitget ──

    [Fact]
    public void TimeSync_ApplyOffset_WritesServerMinusLocal()
    {
        var holder = new long[] { 0L };
        var sync = new CryptoExchanges.Net.Core.Resilience.ExchangeTimeSync();
        var offset = sync.ApplyOffset(serverTimeMs: 1_000_500, localNowMs: 1_000_000, holder);
        offset.Should().Be(500);
        holder[0].Should().Be(500);
    }
}
