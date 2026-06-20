using System.Security.Cryptography;
using System.Text;
using Xunit;
using AwesomeAssertions;
using CryptoExchanges.Net.Core.Auth;

namespace CryptoExchanges.Net.Core.Tests.Unit;

/// <summary>
/// Unit tests for the shared HMAC-SHA256 signer. The hex vector is the SAME one pinned by
/// <c>BinanceSignatureService</c>/<c>BybitSignatureService</c> tests
/// (<c>HMAC-SHA256("hello", key="secret")</c>), so hex output is byte-identical to those services.
/// </summary>
public class HmacSignatureTests
{
    // HMAC-SHA256("hello", key="secret") — identical to the Binance/Bybit pinned hex vector.
    private const string Secret = "secret";
    private const string Payload = "hello";
    private const string ExpectedHex = "88aab3ede8d3adf94d26ab90d3bafd4a2083070c3bcce9c014ee04a443847c0b";
    // base64 of the SAME hash, computed independently (Python hmac/base64).
    private const string ExpectedBase64 = "iKqz7ejTrflNJquQ07r9SiCDBww7zOnAFO4EpEOEfAs=";

    [Fact]
    public void Compute_Hex_MatchesPinnedBinanceBybitVector()
        => HmacSignature.Compute(Secret, Payload, SignatureEncoding.Hex).Should().Be(ExpectedHex);

    [Fact]
    public void Compute_Hex_IsByteIdenticalToRawHmacLowerHex()
    {
        // Independent recomputation of the exact primitive Binance uses: HMACSHA256 + Convert.ToHexStringLower.
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(Payload));
        var expected = Convert.ToHexStringLower(hash);
        HmacSignature.Compute(Secret, Payload, SignatureEncoding.Hex).Should().Be(expected);
    }

    [Fact]
    public void Compute_Base64_MatchesIndependentReference()
        => HmacSignature.Compute(Secret, Payload, SignatureEncoding.Base64).Should().Be(ExpectedBase64);

    [Fact]
    public void Compute_HexAndBase64_AreSameUnderlyingHash()
    {
        var hex = HmacSignature.Compute(Secret, Payload, SignatureEncoding.Hex);
        var base64 = HmacSignature.Compute(Secret, Payload, SignatureEncoding.Base64);
        Convert.FromHexString(hex).Should().Equal(Convert.FromBase64String(base64));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compute_Throws_OnBlankSecret(string? secret)
    {
        var act = () => HmacSignature.Compute(secret!, Payload, SignatureEncoding.Hex);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compute_Throws_OnBlankPayload(string? payload)
    {
        var act = () => HmacSignature.Compute(Secret, payload!, SignatureEncoding.Hex);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Compute_Throws_OnUndefinedEncoding()
    {
        var act = () => HmacSignature.Compute(Secret, Payload, (SignatureEncoding)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

/// <summary>
/// Unit tests for <see cref="ExchangeCredentials"/> — optional passphrase and secret redaction.
/// </summary>
public class ExchangeCredentialsTests
{
    [Fact]
    public void Constructor_WithoutPassphrase_HasNullPassphrase()
    {
        var creds = new ExchangeCredentials("key", "secret");
        creds.ApiKey.Should().Be("key");
        creds.SecretKey.Should().Be("secret");
        creds.Passphrase.Should().BeNull();
        creds.HasPassphrase.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithPassphrase_CarriesPassphrase()
    {
        var creds = new ExchangeCredentials("key", "secret", "pass");
        creds.Passphrase.Should().Be("pass");
        creds.HasPassphrase.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "secret")]
    [InlineData("", "secret")]
    [InlineData("   ", "secret")]
    [InlineData("key", null)]
    [InlineData("key", "")]
    [InlineData("key", "   ")]
    public void Constructor_Throws_OnBlankApiKeyOrSecret(string? apiKey, string? secret)
    {
        var act = () => new ExchangeCredentials(apiKey!, secret!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_OnBlankNonNullPassphrase(string passphrase)
    {
        var act = () => new ExchangeCredentials("key", "secret", passphrase);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToString_DoesNotLeakSecretOrPassphrase()
    {
        var creds = new ExchangeCredentials("publicApiKey1234", "topSecretValue", "myPassphrase");
        var text = creds.ToString();

        text.Should().NotContain("topSecretValue");
        text.Should().NotContain("myPassphrase");
        text.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void ToString_MasksApiKey_RevealingOnlyLastFour()
    {
        var creds = new ExchangeCredentials("publicApiKey1234", "topSecretValue");
        var text = creds.ToString();

        text.Should().Contain("1234");
        text.Should().NotContain("publicApiKey");
        text.Should().Contain("(none)"); // no passphrase
    }

    [Fact]
    public void ToString_MasksShortApiKeyEntirely()
        => new ExchangeCredentials("ab", "secret").ToString()
            .Should().Contain("ApiKey = ****").And.NotContain("ab");

    [Fact]
    public void ToString_MasksExactlyFourCharApiKeyEntirely()
        => new ExchangeCredentials("abcd", "secret").ToString()
            .Should().Contain("ApiKey = ****").And.NotContain("abcd");
}
