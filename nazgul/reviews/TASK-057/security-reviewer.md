---
verdict: APPROVE
---
# Security Review — TASK-057

## Verdict
APPROVE

## Summary
The KuCoin KC-API passphrase-v2 signing implementation is correct and consistent with the OKX reference pattern. All five KC-API headers are stripped and re-added on every attempt, the passphrase is HMAC-SHA256-signed (not sent raw), the timestamp uses Unix epoch milliseconds, and secrets never appear in exception messages or serialization paths.

## Security Checklist
- [x] Prehash order correct (timestamp+method+path+body): PASS
- [x] base64 HMAC-SHA256 used (not hex): PASS
- [x] Passphrase-v2: passphrase is signed (not raw): PASS
- [x] Unix-ms timestamp (not ISO-8601): PASS
- [x] Per-attempt re-sign (fresh timestamp each SendAsync): PASS
- [x] Mark-and-strip: all 5 headers removed before re-add: PASS
- [x] Marker uses request Options (not header): PASS
- [x] No retry policy in handler (GET-only enforced elsewhere): PASS
- [x] Secrets not in exception messages / logs: PASS
- [x] Success code "200000" compared as string: PASS
- [x] JSON exceptions caught in error translator: PASS

## Findings

### Finding: KucoinSigningHandler accepts concrete KucoinSignatureService, not ISignatureService
- **Severity**: INFO
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:21`
- **Category**: Design
- **Verdict**: PASS
- **Issue**: The handler takes `KucoinSignatureService` (concrete type) as its constructor parameter rather than `ISignatureService`. OKX's `OkxSigningHandler` uses `ISignatureService`. This is not a security issue — the concrete type is `internal sealed` and is never accessible from outside the assembly. However, it ties test setup to the concrete type and could make future extension (e.g., swapping in a test double) slightly harder.
- **Fix**: Consider widening the parameter to `ISignatureService` for interface-driven testability parity with OKX. Not blocking.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:19`

### Finding: All five KC-API headers stripped before re-add on retry
- **Severity**: INFO
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:73-82`
- **Category**: Signing integrity
- **Verdict**: PASS
- **Issue**: None. All five headers (`KC-API-KEY`, `KC-API-SIGN`, `KC-API-TIMESTAMP`, `KC-API-PASSPHRASE`, `KC-API-KEY-VERSION`) are removed before being added. The test `Handler_RetrySimulation_YieldsDifferentTimestampsAndNoDuplicateHeaders` verifies no duplicates on re-sign. Correct.
- **Fix**: N/A

### Finding: Passphrase-v2 signing verified by test with known vector
- **Severity**: INFO
- **Confidence**: 99
- **File**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinSigningTests.cs:43-45`
- **Category**: Signing integrity
- **Verdict**: PASS
- **Issue**: None. `SignPassphrase_ProducesExpectedBase64ForFixedVector` asserts the exact base64 HMAC-SHA256 output against an independently computed value. `Handler_SignedRequest_PassphraseHeaderIsSignedNotRaw` further confirms the header is the HMAC result, not the raw passphrase.
- **Fix**: N/A

### Finding: Success code "200000" compared as string literal
- **Severity**: INFO
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinErrorTranslator.cs:29`
- **Category**: Error translator safety
- **Verdict**: PASS
- **Issue**: None. `if (code == "200000")` is a string equality check. The `ReadString` helper returns `null` for any non-string JSON value kind, so a numeric `200000` in the JSON body would return `null` from `ReadString` and therefore never match the string `"200000"`. Correct.
- **Fix**: N/A

### Finding: JSON malformed body handled correctly
- **Severity**: INFO
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinErrorTranslator.cs:67-81`
- **Category**: JSON deserialization safety
- **Verdict**: PASS
- **Issue**: None. `Parse` catches `JsonException` and returns `(null, null)`, which causes `Translate` to fall back to `ExchangeApiException`. Test `ErrorTranslator_NonJsonBody_FallsBackToApiException` confirms this.
- **Fix**: N/A
