# Security Review: TASK-018
**VERDICT: APPROVED**
**Overall confidence: 95/100**
**Blocking items: 0**

Reviewed files:
- `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs`
- `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs`

---

## Findings

### Finding: No re-implemented HMAC — delegates correctly to Core primitive
- **Severity**: N/A
- **Confidence**: 100/100
- **File**: `Auth/BitgetSignatureService.cs:17`
- **Verdict**: PASS
- `Sign(payload)` is `HmacSignature.Compute(_secretKey, payload, SignatureEncoding.Base64)`. No System.Security.Cryptography imports in this file; all crypto is in the Core primitive. Matches OKX pattern exactly (`OkxSignatureService.cs:16`).

---

### Finding: Secret-key guard present and not weakened
- **Severity**: N/A
- **Confidence**: 100/100
- **File**: `Auth/BitgetSignatureService.cs:41-45`
- **Verdict**: PASS
- `InitializeSecretKey` calls `ArgumentException.ThrowIfNullOrWhiteSpace(secretKey)` before storing in `private readonly string _secretKey`. Mirrors OKX (`OkxSignatureService.cs:52-55`) and the task manifest's pattern reference (`BinanceSignatureService.cs:37-41`).

---

### Finding: Secret not logged, serialized, or exposed via ToString/exceptions
- **Severity**: N/A
- **Confidence**: 100/100
- **File**: `Auth/BitgetSignatureService.cs`
- **Verdict**: PASS
- No `ToString()` override; no `[JsonInclude]` or serialization attributes; `_secretKey` is `private readonly` and appears only as a parameter to `HmacSignature.Compute`. `BitgetOptions.cs` (out of scope but checked for exposure risk) has no `ToString()` override or serialization attributes, and `SecretKey` is accessed only via `ToCredentials()` which routes into the signing service — no transmission path.

---

### Finding: Prehash canonicalization is unambiguous and correct
- **Severity**: N/A
- **Confidence**: 95/100
- **File**: `Auth/BitgetSignatureService.cs:33-34`
- **Verdict**: PASS
- Prehash: `timestamp + UPPER(method) + requestPath + ('?' + queryString when non-empty) + body`. The conditional `?` insertion (`queryString.Length > 0`) matches Bitget's documented signing spec exactly. There is no ambiguity between "empty query" and "no query" because both produce identical prehash output (the `?` is omitted in both cases) — this is correct per the Bitget API: a GET with no params and a GET with an empty query string are the same wire request.

---

### Finding: Timestamp format is epoch-milliseconds (correct for Bitget)
- **Severity**: N/A
- **Confidence**: 100/100
- **File**: `Auth/BitgetSignatureService.cs:38-39`
- **Verdict**: PASS
- `ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)` produces an integer epoch-ms string (e.g., `"1718582400000"`). This is the correct Bitget timestamp format, distinct from OKX's ISO-8601 (`OkxSignatureService.cs:49-50`). `CultureInfo.InvariantCulture` prevents locale-specific formatting issues.

---

### Finding: Sign() returns base64; not appended to request path
- **Severity**: N/A
- **Confidence**: 100/100
- **File**: `Auth/BitgetSignatureService.cs:22-23`
- **Verdict**: PASS
- Returns the base64 string for placement in the `ACCESS-SIGN` header. The service does not append the signature to a URL. The signing handler (future task) is responsible for header placement; the service contract matches `ISignatureService.Sign()`.

---

### Finding: BitgetSigningRequest marker is idempotent and retry-safe
- **Severity**: N/A
- **Confidence**: 100/100
- **File**: `Resilience/BitgetSigningRequest.cs:9-21`
- **Verdict**: PASS
- `MarkSigned` sets `Options["bitget.signed"] = true`; calling it twice is harmless (overwrites with same value). `IsSigned` reads the same key. The key string `"bitget.signed"` is exchange-namespaced and cannot collide with Binance's `HttpRequestOptionsKey` entries. Per-attempt re-signing model: the signing handler (future task) reads auth headers and timestamp from scratch per attempt rather than stripping stale params — this is Bitget's correct retry model (header-based signing, unlike Binance's query-param based signing which requires strip-on-retry).

---

### Finding: No double-sign risk from missing strip-on-retry
- **Severity**: N/A
- **Confidence**: 90/100
- **File**: `Resilience/BitgetSigningRequest.cs`
- **Verdict**: PASS
- Bitget uses request headers (`ACCESS-SIGN`, `ACCESS-TIMESTAMP`) not query parameters for auth. Header-based signing handlers naturally overwrite headers on retry (no accumulation possible), unlike Binance's query-string approach where duplicate `signature=` params accumulate. No strip-before-sign logic is needed here. The pattern difference from `BinanceSigningRequest.cs` (which does strip-and-re-sign on retry) is intentional and correct for Bitget.

---

### Finding: Potential ArgumentException from HmacSignature.Compute if prehash is empty/whitespace
- **Severity**: LOW
- **Confidence**: 55/100
- **File**: `Auth/BitgetSignatureService.cs:22-23`
- **Category**: Security
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- `HmacSignature.Compute` enforces `ThrowIfNullOrWhiteSpace(payload)`. If `BuildPrehash` produced a whitespace-only or empty string (e.g., due to a bug or malformed timestamp/path), `Sign()` would throw an unhandled `ArgumentException` rather than a typed SDK exception. In practice this cannot happen because `BuildPrehash` guards timestamp/method/requestPath with `ThrowIfNullOrWhiteSpace` — those three fields ensure the prehash is always non-empty. Risk is theoretical but the exception surface differs from other SDK errors.
- **Fix**: No action required for TASK-018. The signing handler (future task) should wrap `Sign()` in a try/catch and surface it as a typed `ExchangeApiException` if this edge case matters.
- **Pattern reference**: `Core/Auth/SignatureEncoding.cs:40-41`

---

## Summary

- PASS: No re-implemented HMAC — delegates to `HmacSignature.Compute` with `SignatureEncoding.Base64`
- PASS: Secret guard via `InitializeSecretKey` (`ThrowIfNullOrWhiteSpace`); stored `private readonly`; never logged, serialized, or exposed
- PASS: Prehash `timestamp+UPPER(method)+requestPath[+'?'+query]+body` — unambiguous, matches Bitget spec
- PASS: Timestamp is epoch-ms (`ToUnixTimeMilliseconds`), not OKX's ISO-8601
- PASS: `Sign()` returns base64 string; not appended to URL
- PASS: `BitgetSigningRequest` marker is idempotent, exchange-namespaced, retry-safe
- PASS: No double-sign risk — header-based auth naturally overwrites on retry; strip-on-retry not needed
- CONCERN: Theoretical `ArgumentException` from `HmacSignature.Compute` if prehash is empty (confidence: 55/100, non-blocking; guarded upstream by `ThrowIfNullOrWhiteSpace` on identity inputs)

## Final Verdict
**APPROVED** — All security checks pass. No blocking findings. One low-confidence theoretical concern logged for the future signing handler task.
