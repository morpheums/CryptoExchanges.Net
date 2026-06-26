using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CryptoExchanges.Net.Coinbase.Auth;

/// <summary>
/// Mints a fresh, short-lived JWT per request to authenticate Coinbase Advanced Trade (CDP) API calls.
/// Supports ES256 (ECDSA P-256). Ed25519/EdDSA is deferred: BCL <c>ECDsa</c> does not accept OID
/// 1.3.101.112 on .NET 10 — a TODO marks that branch. Signature MUST be P1363/IEEE (raw R‖S, 64 bytes),
/// NOT DER; Coinbase silently rejects DER-encoded signatures despite a valid header and claims.
/// </summary>
internal sealed class CoinbaseJwtSigner
{
    private readonly string _keyName;
    private readonly string _pemKey;

    /// <param name="keyName">The CDP API key name (placed in <c>kid</c> and <c>sub</c> claims).</param>
    /// <param name="pemKey">The PEM-encoded private key string (SEC1/PKCS8 EC or PKCS8 Ed25519).</param>
    public CoinbaseJwtSigner(string keyName, string pemKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pemKey);
        _keyName = keyName;
        _pemKey = pemKey;
    }

    /// <summary>
    /// Mints a compact JWT valid for 120 seconds for the given request target.
    /// </summary>
    /// <param name="method">HTTP method (e.g. <c>GET</c>, <c>POST</c>).</param>
    /// <param name="host">The request host (e.g. <c>api.coinbase.com</c>).</param>
    /// <param name="path">The request path only, no query string (e.g. <c>/api/v3/brokerage/products</c>).</param>
    /// <returns>A compact JWT string (<c>header.payload.signature</c>).</returns>
    /// <exception cref="CryptographicException">Thrown when the key cannot be imported or signing fails.</exception>
    public string MintJwt(string method, string host, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var now = DateTimeOffset.UtcNow;
        var nbf = now.ToUnixTimeSeconds();
        var exp = nbf + 120;

        var uri = $"{method.ToUpperInvariant()} {host}{path}";

        if (IsEc(_pemKey))
            return SignEs256(nbf, exp, uri);

        // Ed25519/EdDSA: BCL ECDsa does not support OID 1.3.101.112 on .NET 10.
        // TODO: revisit when System.Security.Cryptography gains Ed25519 signing support.
        throw new CryptographicException(
            "Ed25519 key material detected but EdDSA JWT signing is not yet supported by BCL ECDsa on .NET 10. " +
            "Use an EC P-256 key (BEGIN EC PRIVATE KEY or PKCS8 with P-256 OID) for ES256 signing.");
    }

    private string SignEs256(long nbf, long exp, string uriClaim)
    {
        using var ecdsa = ECDsa.Create();
        ImportEcKey(ecdsa, _pemKey);

        // Build via System.Text.Json so a kid/sub/uri containing a quote or backslash is escaped.
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "ES256", kid = _keyName, typ = "JWT" }));

        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new { sub = _keyName, iss = "cdp", nbf, exp, uri = uriClaim }));

        var signingInput = $"{header}.{payload}";
        var signingBytes = Encoding.ASCII.GetBytes(signingInput);

        // P1363 (R‖S) is required — DER output silently fails Coinbase auth verification.
        var sig = ecdsa.SignData(signingBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var signature = Base64UrlEncode(sig);

        return $"{signingInput}.{signature}";
    }

    private static void ImportEcKey(ECDsa ecdsa, string pem)
    {
        // SEC1 ("BEGIN EC PRIVATE KEY") or PKCS8 ("BEGIN PRIVATE KEY") with EC OID.
        if (pem.Contains("BEGIN EC PRIVATE KEY", StringComparison.Ordinal))
            ecdsa.ImportECPrivateKey(DecodePemPayload(pem, "EC PRIVATE KEY"), out _);
        else if (pem.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal))
            ecdsa.ImportPkcs8PrivateKey(DecodePemPayload(pem, "PRIVATE KEY"), out _);
        else
            throw new CryptographicException(
                "PEM key must begin with '-----BEGIN EC PRIVATE KEY-----' or '-----BEGIN PRIVATE KEY-----'.");
    }

    private const string EcPublicKeyOid = "1.2.840.10045.2.1"; // id-ecPublicKey (Ed25519 is 1.3.101.112)

    /// <summary>
    /// True for an EC (ES256) key: SEC1 is always EC; for PKCS8 the real algorithm OID is read from the
    /// DER (id-ecPublicKey ⇒ EC, Ed25519 ⇒ not EC). The old base64-text scan never matched, so Ed25519
    /// keys were misclassified as EC.
    /// </summary>
    private static bool IsEc(string pem)
    {
        if (pem.Contains("BEGIN EC PRIVATE KEY", StringComparison.Ordinal))
            return true;
        if (!pem.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal))
            return false;
        return Pkcs8AlgorithmOid(DecodePemPayload(pem, "PRIVATE KEY")) == EcPublicKeyOid;
    }

    // PKCS8 PrivateKeyInfo ::= SEQUENCE { version INTEGER, privateKeyAlgorithm AlgorithmIdentifier, ... }
    // AlgorithmIdentifier ::= SEQUENCE { algorithm OBJECT IDENTIFIER, parameters ANY OPTIONAL }
    private static string? Pkcs8AlgorithmOid(byte[] pkcs8)
    {
        try
        {
            var info = new AsnReader(pkcs8, AsnEncodingRules.DER).ReadSequence();
            info.ReadInteger();
            return info.ReadSequence().ReadObjectIdentifier();
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    private static byte[] DecodePemPayload(string pem, string label)
    {
        var begin = $"-----BEGIN {label}-----";
        var end = $"-----END {label}-----";
        var start = pem.IndexOf(begin, StringComparison.Ordinal) + begin.Length;
        var finish = pem.IndexOf(end, StringComparison.Ordinal);
        var b64 = pem[start..finish]
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Trim();
        return Convert.FromBase64String(b64);
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
