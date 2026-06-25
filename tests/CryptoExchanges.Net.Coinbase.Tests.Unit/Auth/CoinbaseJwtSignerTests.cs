using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Coinbase.Auth;
using CryptoExchanges.Net.Coinbase.Resilience;
using CryptoExchanges.Net.Core.Exceptions;

namespace CryptoExchanges.Net.Coinbase.Tests.Unit.Auth;

/// <summary>
/// KAT tests for <see cref="CoinbaseJwtSigner"/>: ES256 (P1363/JOSE) JWT structure, claim values,
/// and signature verifiability. All key material is generated in-test — no real credentials in source.
/// Ed25519/EdDSA: deferred — BCL ECDsa does not accept OID 1.3.101.112 on .NET 10; see
/// <see cref="SignRequest_EdDSA_Deferred"/>.
/// </summary>
public class CoinbaseJwtSignerTests
{
    private const string KeyName = "test-key-name";
    private const string Host = "api.coinbase.com";
    private const string Path = "/api/v3/brokerage/products";

    private static (CoinbaseJwtSigner signer, ECDsa publicKey) BuildEcSigner()
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var secBytes = ecdsa.ExportECPrivateKey();
        var pem = new StringBuilder();
        pem.AppendLine("-----BEGIN EC PRIVATE KEY-----");
        pem.AppendLine(Convert.ToBase64String(secBytes, Base64FormattingOptions.InsertLineBreaks));
        pem.AppendLine("-----END EC PRIVATE KEY-----");
        var signer = new CoinbaseJwtSigner(KeyName, pem.ToString());
        return (signer, ecdsa);
    }

    private static (string header, string payload, byte[] sig) SplitJwt(string jwt)
    {
        var parts = jwt.Split('.');
        parts.Should().HaveCount(3, "a compact JWT must have exactly three segments");
        return (parts[0], parts[1], Base64UrlDecode(parts[2]));
    }

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private static T DecodeJson<T>(string base64url) =>
        JsonSerializer.Deserialize<T>(Base64UrlDecode(base64url))!;

    [Fact]
    public void SignRequest_ES256_ProducesValidJwt_P1363Encoding()
    {
        var (signer, ecdsa) = BuildEcSigner();

        var jwt = signer.MintJwt("GET", Host, Path);
        var (headerSeg, payloadSeg, sigBytes) = SplitJwt(jwt);

        var header = DecodeJson<Dictionary<string, string>>(headerSeg);
        header["alg"].Should().Be("ES256");
        header["kid"].Should().Be(KeyName);
        header["typ"].Should().Be("JWT");

        var payload = DecodeJson<Dictionary<string, JsonElement>>(payloadSeg);
        payload["uri"].GetString().Should().Be($"GET {Host}{Path}");
        payload["sub"].GetString().Should().Be(KeyName);
        payload["iss"].GetString().Should().Be("cdp");

        // P1363 = raw R‖S = 64 bytes → 86 base64url chars (Coinbase rejects DER-encoded signatures)
        sigBytes.Should().HaveCount(64, "ES256 P1363 signature is always 64 bytes (R‖S for P-256)");
        var jwtParts = jwt.Split('.');
        jwtParts[2].Should().HaveLength(86, "64 bytes base64url-encoded without padding = 86 chars");

        var signingInput = Encoding.ASCII.GetBytes($"{jwtParts[0]}.{jwtParts[1]}");
        ecdsa.VerifyData(signingInput, sigBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
            .Should().BeTrue("the JWT signature must verify with the corresponding EC public key");
    }

    [Fact]
    public void SignRequest_ES256_DifferentAttemptsProduceDifferentJwts()
    {
        var (signer, _) = BuildEcSigner();

        var jwt1 = signer.MintJwt("GET", Host, Path);
        var jwt2 = signer.MintJwt("GET", Host, Path);

        // ECDSA is non-deterministic; two calls produce different signatures even for the same input.
        jwt1.Should().NotBe(jwt2);
    }

    [Fact]
    public void SignRequest_ES256_ExpiryWithin120Seconds()
    {
        var (signer, _) = BuildEcSigner();
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var jwt = signer.MintJwt("GET", Host, Path);

        var (_, payloadSeg, _) = SplitJwt(jwt);
        var payload = DecodeJson<Dictionary<string, JsonElement>>(payloadSeg);
        var nbf = payload["nbf"].GetInt64();
        var exp = payload["exp"].GetInt64();

        (exp - nbf).Should().Be(120, "token lifetime must be exactly 120 seconds");
        nbf.Should().BeGreaterThanOrEqualTo(before);
        nbf.Should().BeLessThanOrEqualTo(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    [Fact]
    public void SignRequest_NullKey_ThrowsCryptographicException()
    {
        var act = () => new CoinbaseJwtSigner(KeyName, " ").MintJwt("GET", Host, Path);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SignRequest_EdDSA_Deferred()
    {
        // Ed25519/EdDSA is deferred: BCL ECDsa rejects OID 1.3.101.112 on .NET 10.
        // When BCL support ships, add SignRequest_EdDSA_ProducesValidJwt verifying alg=EdDSA + 64-byte sig.
        const string fakePkcs8Ed25519 = """
            -----BEGIN PRIVATE KEY-----
            MC4CAQAwBQYDK2VwBCIEIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
            -----END PRIVATE KEY-----
            """;
        var signer = new CoinbaseJwtSigner(KeyName, fakePkcs8Ed25519);
        var act = () => signer.MintJwt("GET", Host, Path);
        act.Should().Throw<Exception>("Ed25519 is deferred and must signal failure, not silently produce a wrong JWT");
    }

    [Fact]
    public void SigningRequest_MarkerRoundTrips()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://{Host}{Path}");
        CoinbaseSigningRequest.IsSigned(request).Should().BeFalse();
        CoinbaseSigningRequest.MarkSigned(request);
        CoinbaseSigningRequest.IsSigned(request).Should().BeTrue();
    }

    [Fact]
    public void ErrorTranslator_Unauthorized_MapsToAuthenticationException()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var ex = new CoinbaseErrorTranslator().Translate(response,
            "{\"error\":\"UNAUTHORIZED\",\"message\":\"invalid token\",\"error_details\":\"expired\"}");
        ex.Should().BeOfType<AuthenticationException>();
    }

    [Fact]
    public void ErrorTranslator_TooManyRequests_MapsToRateLimitExceededException()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var ex = new CoinbaseErrorTranslator().Translate(response, "{}");
        ex.Should().BeOfType<RateLimitExceededException>();
    }

    [Fact]
    public void ErrorTranslator_InsufficientFunds_MapsToInsufficientBalanceException()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var ex = new CoinbaseErrorTranslator().Translate(response,
            "{\"error\":\"INSUFFICIENT_FUNDS\",\"message\":\"no funds\"}");
        ex.Should().BeOfType<InsufficientBalanceException>();
    }

    [Fact]
    public void ErrorTranslator_InvalidArgument_MapsToInvalidOrderException()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var ex = new CoinbaseErrorTranslator().Translate(response,
            "{\"error\":\"INVALID_ARGUMENT\",\"message\":\"bad param\"}");
        ex.Should().BeOfType<InvalidOrderException>();
    }

    [Fact]
    public void ErrorTranslator_UnknownError_MapsToExchangeApiException()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var ex = new CoinbaseErrorTranslator().Translate(response,
            "{\"error\":\"SOME_NEW_ERROR\",\"message\":\"unknown\"}");
        ex.Should().BeOfType<ExchangeApiException>();
    }

    [Fact]
    public void ErrorTranslator_NonJsonBody_MapsToExchangeApiException()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway);
        var ex = new CoinbaseErrorTranslator().Translate(response, "<html>502</html>");
        ex.Should().BeOfType<ExchangeApiException>();
    }

    [Theory]
    [InlineData("INVALID_API_KEY")]
    [InlineData("INVALID_SIGNATURE")]
    [InlineData("EXPIRED_TOKEN")]
    [InlineData("AUTHENTICATION_REQUIRED")]
    public void ErrorTranslator_AuthErrorCodes_MapToAuthenticationException(string errorCode)
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        var ex = new CoinbaseErrorTranslator().Translate(response,
            $"{{\"error\":\"{errorCode}\",\"message\":\"auth failed\"}}");
        ex.Should().BeOfType<AuthenticationException>();
    }
}
