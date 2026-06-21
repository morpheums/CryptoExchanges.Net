---
verdict: APPROVE
---
# Security Review — TASK-057 (Re-review, retry 1/3)

## What Changed Since Prior APPROVE

Two post-approval changes were applied:

1. **DIP fix**: `IKucoinSignatureService` interface introduced; `KucoinSigningHandler` constructor parameter widened from `KucoinSignatureService` (concrete) to `IKucoinSignatureService` (interface). This directly addresses the non-blocking concern raised in the original review.

2. **Simplify pass**: Four cosmetic/correctness changes:
   - `<inheritdoc/>` added to `SignPassphrase` implementation.
   - `using System.Globalization` added to `KucoinSignatureService.cs` (was needed by `CultureInfo.InvariantCulture` in `FormatTimestamp`).
   - `.ToUniversalTime()` removed before `.ToUnixTimeMilliseconds()` in `FormatTimestamp`.
   - `"2"` extracted to `private const string KeyVersion` in `KucoinSigningHandler`.

---

## Checklist Re-verification

### 1. HMAC-SHA256 algorithm — PASS
`KucoinSignatureService.Sign` delegates to `HmacSignature.Compute` with `SignatureEncoding.Base64`. `HmacSignature.Compute` (Core, line 30-32) uses `Encoding.UTF8.GetBytes(secret)` as the key and `HMACSHA256.HashData` as the primitive. UTF-8 encoding of the secret key is correct and unchanged.

### 2. Base64 encoding — PASS
`HmacSignature.Compute` with `SignatureEncoding.Base64` returns `Convert.ToBase64String(hash)`. Not hex. Unchanged from prior review.

### 3. Prehash order — PASS
`BuildPrehash` at `KucoinSignatureService.cs:52` produces `$"{timestamp}{method.ToUpperInvariant()}{requestPath}{body}"`. Order is timestamp + METHOD + requestPath + body. Correct and unchanged.

### 4. PassphraseV2 — PASS
`KucoinSigningHandler.ResignAsync` (line 70) calls `signatureService.SignPassphrase(passphrase)` and sets the result as the `KC-API-PASSPHRASE` header (line 82). The raw passphrase string is never transmitted. `SignPassphrase` itself calls `HmacSignature.Compute(_secretKey, passphrase, SignatureEncoding.Base64)`. Test `Handler_SignedRequest_PassphraseHeaderIsSignedNotRaw` (tests line 217-231) independently verifies the header value equals `HmacSignature.Compute` output, not the raw passphrase. Unchanged.

### 5. Secret exposure — PASS
`_secretKey` is a `private readonly string` field inside `KucoinSignatureService`. It is not exposed in any property, method return value, exception message, `ToString()`, log call, or serialization path. No change from DIP fix or simplify pass touches this field.

### 6. KC-API-KEY-VERSION: 2 — PASS
The simplify pass extracted the literal `"2"` to `private const string KeyVersion = "2"` (handler line 24). The value used on line 83 (`request.Headers.Add("KC-API-KEY-VERSION", KeyVersion)`) is still exactly `"2"`. Test `Handler_SignedRequest_SetsAllFiveKcApiHeaders` (tests line 213) asserts `.Single().Should().Be("2")`. No regression.

### 7. Mark-and-strip — PASS
All five headers (`KC-API-KEY`, `KC-API-SIGN`, `KC-API-TIMESTAMP`, `KC-API-PASSPHRASE`, `KC-API-KEY-VERSION`) are removed (lines 74-78) then added (lines 79-83) on every call to `ResignAsync`. Neither the DIP fix nor the simplify pass touched these lines. Pattern is intact.

### 8. Per-attempt re-sign — PASS
`DateTimeOffset.UtcNow.AddMilliseconds(timeOffset())` is evaluated inside `ResignAsync`, which is called on every `SendAsync` invocation when the request is marked. Fresh timestamp every attempt. Unchanged.

### 9. Unsigned passthrough — PASS
`SendAsync` only calls `ResignAsync` when `KucoinSigningRequest.IsSigned(request)` returns true (handler line 31-32). Unsigned requests go directly to `base.SendAsync` with no headers set. Test `Handler_UnsignedRequest_PassesThroughWithNoKcApiHeaders` covers this. Unchanged.

### 10. Guard on missing credentials — PASS
`ResignAsync` throws `InvalidOperationException` for empty `apiKey` (lines 42-44) and empty `passphrase` (lines 45-47). Covered by tests `Handler_MissingApiKey_Throws` and `Handler_MissingPassphrase_Throws`. Unchanged.

### 11. `.ToUniversalTime()` removal — PASS (verified, see finding below)
`DateTimeOffset.ToUnixTimeMilliseconds()` is defined as returning the number of milliseconds elapsed since 1970-01-01T00:00:00.000Z, regardless of the `Offset` property of the `DateTimeOffset` value. Calling `.ToUniversalTime()` first was a no-op: it merely changed the `Offset` to zero without altering the underlying instant. Removing it produces identical output for all inputs. The test `FormatTimestamp_ConvertsToUtc` (tests line 125-130) directly exercises this: a `+05:00` instant is asserted to produce the same millisecond string as the equivalent UTC instant. This test was already present and continues to cover the correctness of UTC-normalization semantics.

---

## Findings

### Finding: `.ToUniversalTime()` removal is a correct no-op
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Kucoin/Auth/KucoinSignatureService.cs:62-63`
- **Category**: Security (signing correctness)
- **Verdict**: PASS
- **Issue**: `DateTimeOffset.ToUnixTimeMilliseconds()` always computes relative to the Unix epoch in UTC regardless of the stored `Offset`. `.ToUniversalTime()` only changed the `Offset` representation to zero; it did not change the underlying instant or the millisecond count. Removing it produces byte-for-byte identical output.
- **Fix**: No fix required. The existing test `FormatTimestamp_ConvertsToUtc` in `KucoinSigningTests.cs:125-130` verifies that a non-UTC `DateTimeOffset` still produces the correct epoch-millisecond string.
- **Pattern reference**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinSigningTests.cs:125-130`

### Finding: DIP fix — `KucoinSigningHandler` now depends on `IKucoinSignatureService`
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:21`
- **Category**: Design / testability
- **Verdict**: PASS
- **Issue**: None. This resolves the non-blocking concern from the prior review. The handler now accepts the interface, which is the established pattern. No security regression introduced.
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Kucoin/Auth/IKucoinSignatureService.cs:10`

---

## Summary

- PASS: HMAC-SHA256 algorithm — `HMACSHA256.HashData` with UTF-8 key bytes, unchanged
- PASS: Base64 encoding — `Convert.ToBase64String`, unchanged
- PASS: Prehash order — `timestamp + METHOD + requestPath + body`, unchanged
- PASS: PassphraseV2 — passphrase HMAC-signed via `SignPassphrase`, raw passphrase never transmitted
- PASS: Secret exposure — `_secretKey` private field only, never logged or serialized
- PASS: KC-API-KEY-VERSION: 2 — extracted to `const string KeyVersion = "2"`, semantically identical
- PASS: Mark-and-strip — all five headers removed before re-add, unchanged
- PASS: Per-attempt re-sign — fresh `DateTimeOffset.UtcNow` on each `SendAsync`
- PASS: Unsigned passthrough — unmarked requests bypass `ResignAsync` entirely
- PASS: Guard on missing credentials — `InvalidOperationException` for empty key or passphrase
- PASS: `.ToUniversalTime()` removal — `DateTimeOffset.ToUnixTimeMilliseconds()` is UTC-intrinsic; removal is a correct no-op, covered by test `FormatTimestamp_ConvertsToUtc`

---

## Final Verdict

APPROVED — No security regressions from either the DIP fix or the simplify pass. All 11 security checks pass. The `.ToUniversalTime()` removal is correct by the .NET `DateTimeOffset` contract and is independently verified by the existing timestamp test.
