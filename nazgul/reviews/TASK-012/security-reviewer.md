# Security Review — TASK-012: OkxSigningHandler

VERDICT: APPROVED

Reviewed file: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs`
Diff: new file, 76 lines.

---

## Findings

### Finding: No blocking issues found

All security-critical properties verified. Full detail per checklist item below.

---

## Checklist Results

### Credential Safety

**PASS** — SecretKey not stored in OkxSigningHandler. The secret is held exclusively inside `OkxSignatureService._secretKey` (Auth/OkxSignatureService.cs:18). The handler receives `OkxSignatureService` by reference and calls `signatureService.Sign(prehash)` — the secret never crosses into this file.

**PASS** — No credential logging or serialization. The two `InvalidOperationException` throw messages (lines 39-43) describe the missing credential type ("API key", "passphrase") but do NOT echo the actual credential value. No `ToString()`, `JsonSerializer`, or log call appears anywhere in the file.

**PASS** — SecretKey is never transmitted. The handler only writes four headers: `OK-ACCESS-KEY` (apiKey), `OK-ACCESS-SIGN` (computed signature), `OK-ACCESS-TIMESTAMP` (ISO-8601 string), `OK-ACCESS-PASSPHRASE` (passphrase). The secret key is consumed internally by `OkxSignatureService.Sign()` and its output (the base64 HMAC) is what travels over the wire.

### Signing Integrity

**PASS** — Signing gate via `OkxSigningRequest.IsSigned()`. Line 27 checks `OkxSigningRequest.IsSigned(request)` before entering `ResignAsync`. Public/unsigned requests skip the method entirely — no auth headers are added, matching the OKX public-endpoint model described in the task brief.

**PASS** — Strip-then-re-add all four headers (lines 67-74). On every attempt (including retries), all four `OK-ACCESS-*` headers are removed before being written. No stale timestamp or signature from a prior attempt can survive onto a retried request. Pattern matches Bybit reference: `BybitSigningHandler.cs:59-64`.

**PASS** — Handler sits below the retry boundary (per architecture). No deviation from the established pattern in `BybitSigningHandler`. Re-signing occurs inside `ResignAsync` which is called per `SendAsync` invocation, guaranteeing a fresh ISO-8601 timestamp and fresh signature on each retry attempt.

### Prehash Correctness

**PASS** — `requestPath` is set to `request.RequestUri!.PathAndQuery` (line 52). This is the actual outgoing URI including path and query string, ensuring byte-for-byte consistency between what is signed and what OKX receives. The null-forgiving `!` is acceptable here because by the time `SendAsync` executes, `RequestUri` is never null for a properly constructed `HttpRequestMessage`; this mirrors the pattern in `BybitSigningHandler`.

**PASS** — HTTP verb is taken from `request.Method.Method` (line 53) and passed directly into `BuildPrehash`, which applies `.ToUpperInvariant()` (OkxSignatureService.cs:50), ensuring the prehash is always uppercase regardless of how the caller constructed the request.

**PASS** — Body read for POST and PUT only (lines 56-60). GET/DELETE requests produce an empty body string, matching OKX API specification. Body is read once via `ReadAsStringAsync` with the `CancellationToken` forwarded correctly.

### Fail-Fast on Incomplete Credentials

**PASS** — `InvalidOperationException` is thrown on null/empty `apiKey` (lines 38-40) or null/empty `passphrase` (lines 41-43) before any header is written. A partial credential set cannot produce a silent unauthenticated request.

### Secret Management Expansion

**PASS** — No new credential source introduced. Handler receives credentials via constructor injection only; no env-var reads, config file access, or static state in this file.

### No Homegrown Crypto

**PASS** — `signatureService.Sign(prehash)` delegates to `HmacSignature.Compute(secret, prehash, SignatureEncoding.Base64)` in `CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs`. No inline HMAC or crypto implementation in this handler.

---

## Summary

- PASS: Credential safety — SecretKey not present; apiKey/passphrase not echoed in exception messages; SecretKey never transmitted.
- PASS: Signing integrity — IsSigned gate, strip-then-re-add pattern for all four headers, no stale headers on retry.
- PASS: Prehash correctness — PathAndQuery used for requestPath; verb uppercased; body read once for POST/PUT only.
- PASS: Fail-fast — InvalidOperationException before any header write when apiKey or passphrase is empty.
- PASS: Public request isolation — unsigned requests pass through with zero OK-ACCESS-* headers added.
- PASS: No homegrown crypto — signing delegated entirely to OkxSignatureService/HmacSignature.Compute.

## Final Verdict

APPROVED — All security checks pass. No blocking issues. No concerns.
