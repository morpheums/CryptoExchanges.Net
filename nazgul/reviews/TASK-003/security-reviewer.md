# Security Review — TASK-003: BybitSigningHandler

**Reviewer**: Security Reviewer
**Commit**: 283bcf0
**Date**: 2026-06-17
**Files reviewed**:
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs` (new)
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs` (doc-cref touch)
- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs` (supporting, read-only)

---

## Findings

### Finding 1: HMAC canonicalization matches Bybit V5 scheme
- **Severity**: N/A
- **Confidence**: 97
- **File**: `Auth/BybitSignatureService.cs:43,60`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: N/A

`BuildGetSignString` and `BuildPostSignString` produce `timestamp+apiKey+recvWindow+queryString` and `timestamp+apiKey+recvWindow+jsonBody` respectively via direct concatenation with no separator — which is exactly Bybit V5's documented canonical form. GET query string has the leading `?` stripped before signing. POST body is signed verbatim. `recvWindow` is both in the signed payload and emitted as `X-BAPI-RECV-WINDOW`, preventing in-transit substitution to extend the replay window.

---

### Finding 2: Re-sign-on-retry — no header doubling
- **Severity**: N/A
- **Confidence**: 98
- **File**: `Resilience/BybitSigningHandler.cs:55-60`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:85-91`

All three mutable auth headers (`X-BAPI-TIMESTAMP`, `X-BAPI-RECV-WINDOW`, `X-BAPI-SIGN`) are unconditionally removed before being re-added inside `ResignAsync`. `X-BAPI-API-KEY` is also removed-then-added in the outer `SendAsync`. A retried request cannot carry a stale timestamp or signature from a prior attempt. Correctly mirrors the Binance strip-before-readd pattern.

---

### Finding 3: Body idempotency — ReadAsStringAsync on StringContent is safe for retry
- **Severity**: N/A
- **Confidence**: 95
- **File**: `Resilience/BybitSigningHandler.cs:42`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: N/A

`BybitHttpClient.PostAsync` wraps the JSON in `StringContent` backed by an in-memory buffer. `StringContent` uses `MemoryStream`, which resets its read position between reads. `ReadAsStringAsync` on a retry attempt returns the same bytes as the first attempt. The handler does not consume or dispose `request.Content`, so the body is intact for transmission. No TOCTOU gap between what is signed and what is sent.

---

### Finding 4: Secret confinement — no leakage paths
- **Severity**: N/A
- **Confidence**: 99
- **File**: `Auth/BybitSignatureService.cs:15`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: N/A

`secretKey` is converted to `byte[]` at construction time inside `BybitSignatureService` and never stored as a string or exposed via any property. The handler stores only `apiKey` (non-secret), `signatureService` (opaque), `recvWindow`, and `timeOffset`. Neither class overrides `ToString()`, participates in JSON serialization, or constructs exception messages containing secret material. `X-BAPI-SIGN` is placed only in the request header, never in the URL or query string.

---

### Finding 5: Signed-but-empty-apiKey throws ArgumentException mid-pipeline rather than at construction
- **Severity**: LOW
- **Confidence**: 82
- **File**: `Resilience/BybitSigningHandler.cs:22` / `Auth/BybitSignatureService.cs:40`
- **Category**: Security
- **Verdict**: CONCERN (non-blocking — confidence 82, severity LOW)
- **Issue**: When `apiKey` is `""`, `SendAsync` skips setting `X-BAPI-API-KEY` (guarded by `!string.IsNullOrEmpty`). If the same request is also marked signed, `ResignAsync` runs and calls `BuildGetSignString(timestamp, "", ...)`, which hits `ArgumentException.ThrowIfNullOrWhiteSpace(apiKey)` at `BybitSignatureService.cs:40`. The result is an `ArgumentException` thrown from inside the `DelegatingHandler.SendAsync` pipeline rather than at DI-wire/construction time. This is not a security vulnerability — the request is never transmitted with a corrupt signed payload — but it produces a confusing mid-pipeline failure for a misconfigured client. It does not represent a regression over the Binance pattern, which has the same construction-time omission.
- **Fix**: Add `ArgumentException.ThrowIfNullOrWhiteSpace(apiKey, nameof(apiKey))` to the `BybitSigningHandler` constructor body (or a guard at the top of `ResignAsync` before computing the sign-string), so misconfiguration is caught at DI-wire time rather than on first signed request.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:63-67` — `InitializeSecretKey` validates `secretKey` at construction; `apiKey` validation in the signing path should mirror this posture.

---

### Finding 6: BybitOptions serialization posture — no regression introduced
- **Severity**: N/A
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bybit/BybitOptions.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: N/A

`BybitOptions` carries `SecretKey` and `ApiKey` as plain string properties with no `[JsonInclude]` attribute and no `ToString()` override. This matches the existing `BinanceOptions` posture exactly. No new serialization risk is introduced by this task.

---

## Summary

- PASS: HMAC canonicalization — `timestamp+apiKey+recvWindow+payload` concatenation matches Bybit V5 exactly; `recvWindow` is signed and transmitted (confidence: 97/100)
- PASS: Re-sign on retry — all three auth headers stripped before re-add on every `SendAsync` call; no stale or doubled headers possible (confidence: 98/100)
- PASS: Body idempotency — `StringContent` is memory-backed; signing and transmission use identical bytes; handler does not dispose content (confidence: 95/100)
- PASS: Secret confinement — `secretKey` held only as `byte[]` in `BybitSignatureService`; no leakage via exceptions, logging, headers, or query string (confidence: 99/100)
- CONCERN: Empty-apiKey signed request throws `ArgumentException` mid-pipeline rather than at construction time — non-blocking, no security impact, no regression versus Binance baseline (confidence: 82/100, non-blocking)
- PASS: `BybitOptions` serialization posture — no `[JsonInclude]`, no `ToString()` leak, no regression introduced by this task (confidence: 90/100)

---

## Final Verdict

APPROVED

No blocking security issues. HMAC signing integrity is sound: canonicalization is correct per Bybit V5, retry produces a single fresh header set, body bytes are identical between signing and transmission, and the secret never leaves the HMAC computation. The one CONCERN (empty-apiKey mid-pipeline throw) is a DX/robustness issue with no security consequence and no regression versus the Binance baseline.
