# Security Review: TASK-002 — BybitSignatureService + BybitSigningRequest

**Reviewer**: Security Reviewer  
**Reviewed commit**: 5654d93  
**Branch**: feat/m2-exchange-expansion  
**Date**: 2026-06-17

## Files Under Review
- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs`

## Pattern References
- `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs`
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs`

---

## Findings

### Finding 1: HMAC key/data argument order is correct
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:25`
- **Category**: Signing integrity
- **Verdict**: PASS
- **Issue**: N/A
- `HMACSHA256.HashData(_secretKeyBytes, signBytes)` — first argument is the HMAC key (secret), second is the data (sign-string). Correct ordering; no key/data swap.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:21`

---

### Finding 2: Signature output encoding matches Bybit spec
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:26`
- **Category**: Signing integrity
- **Verdict**: PASS
- **Issue**: N/A
- `Convert.ToHexStringLower(hash)` produces lowercase hex. Bybit specifies lowercase hex in `X-BAPI-SIGN`. Matches the Binance pattern reference exactly.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:22`

---

### Finding 3: Secret key is not stored in cleartext — only byte[] cache retained
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:13-15`
- **Category**: Credential safety
- **Verdict**: PASS
- **Issue**: N/A
- The primary constructor parameter `secretKey` is consumed immediately by `InitializeSecretKey`, converted to `byte[]`, and stored only as `_secretKeyBytes`. The original `string secretKey` value is not retained in any instance field. The class is `sealed` — no subclass can expose the byte array. No `ToString()` override present.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:11`

---

### Finding 4: `ArgumentException.ThrowIfNullOrWhiteSpace` emits param name only, not value
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:57`
- **Category**: Credential safety
- **Verdict**: PASS
- **Issue**: N/A
- `ArgumentException.ThrowIfNullOrWhiteSpace(secretKey)` — .NET runtime includes only the parameter name `"secretKey"` in the exception message, never the value. Safe.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:39`

---

### Finding 5: No serialization or logging paths for the secret
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs` (entire file)
- **Category**: Credential safety
- **Verdict**: PASS
- **Issue**: N/A
- No `[JsonInclude]`, no `JsonSerializer`, no `ISerializable`, no logging calls, no exception message construction involving key material.

---

### Finding 6: Secret key used only for HMAC computation — never transmitted
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:22-27`
- **Category**: Credential safety / Signing integrity
- **Verdict**: PASS
- **Issue**: N/A
- `_secretKeyBytes` is used only as the HMAC key in `Sign()`. It is never returned, never placed in any header argument, and never included in the sign-string builders (which are `static` with no instance access).

---

### Finding 7: Sign-string builders cannot access secret; output matches Bybit spec
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:37-53`
- **Category**: Signing integrity / Credential safety
- **Verdict**: PASS
- **Issue**: N/A
- `BuildGetSignString` and `BuildPostSignString` are `static` — they cannot reference `_secretKeyBytes`. Output is `timestamp + apiKey + recvWindow + queryString/jsonBody`, exactly matching the Bybit canonical sign-string from the research trace (`timestamp+apiKey+recvWindow+queryString` GET / `+jsonBody` POST). The `apiKey` is the public API key, not the secret.

---

### Finding 8: Signing marker key is namespaced and distinct from Binance
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:7`
- **Category**: Signing integrity
- **Verdict**: PASS
- **Issue**: N/A
- `new HttpRequestOptionsKey<bool>("bybit.signed")` is distinct from the Binance key `"binance.signed"`. No risk of cross-handler consumption.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs:7`

---

### Finding 9: `IsSigned`/`MarkSigned` idempotency across retries
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:10-21`
- **Category**: Signing integrity / Double-signing prevention
- **Verdict**: PASS
- **Issue**: N/A
- `MarkSigned` sets a boolean option key on `HttpRequestMessage.Options` — calling it multiple times overwrites with `true`, idempotent. `IsSigned` reads back the value. The Options dictionary persists across retries on the same `HttpRequestMessage` object, so `IsSigned` correctly remains `true` on retry without re-marking. The future `BybitSigningHandler` will use this gate to re-sign with a fresh timestamp on each attempt (header-based, not query-based — strip-and-re-add responsibility lies with the handler, not the marker). Marker pattern is correct.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs`

---

### Finding 10: `Sign(string signString)` lacks explicit null guard
- **Severity**: LOW
- **Confidence**: 65
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:22-27`
- **Category**: Input validation
- **Verdict**: CONCERN (non-blocking — confidence 65/100)
- **Issue**: `Sign(string signString)` does not call `ArgumentNullException.ThrowIfNull(signString)` before passing to `Encoding.UTF8.GetBytes(signString)`. A `null` argument would throw a `NullReferenceException` from `GetBytes` rather than a clean `ArgumentNullException`.
- **Fix**: Add `ArgumentNullException.ThrowIfNull(signString);` as the first line of `Sign()`. Low urgency — the call path is tightly controlled (callers use the static builder methods which produce non-null strings via interpolation), and this gap is consistent with the Binance pattern reference.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:18` (same gap exists in Binance — consistent but not ideal)

---

### Finding 11: `BybitOptions.SecretKey` lacks `ToString()` redaction (out of scope, pre-existing)
- **Severity**: LOW
- **Confidence**: 50
- **File**: `src/CryptoExchanges.Net.Bybit/BybitOptions.cs:15` (outside TASK-002 file scope)
- **Category**: Credential safety
- **Verdict**: CONCERN (non-blocking — confidence 50/100, outside declared file scope)
- **Issue**: `BybitOptions` has no `ToString()` override and no `[JsonIgnore]` on `SecretKey`. If the options object were serialized (e.g., by a health check or settings dump), the secret would be exposed. This is a pre-existing gap from TASK-001, not introduced by TASK-002.
- **Fix**: Add a `ToString()` override to `BybitOptions` that redacts `SecretKey` (e.g., returns `"[REDACTED]"` or `"***"`). Track in a follow-up task.

---

## Summary

- PASS: HMAC key/data ordering — `_secretKeyBytes` as key, `signBytes` as data; no swap.
- PASS: Signature output encoding — `Convert.ToHexStringLower` produces correct lowercase hex matching Bybit spec.
- PASS: Secret key storage — converted immediately to `byte[]`, original string not retained in any field.
- PASS: `ThrowIfNullOrWhiteSpace` on `secretKey` — emits param name only, not the value; safe.
- PASS: No serialization or logging paths for secret key or derived bytes.
- PASS: Secret transmitted nowhere — used only as HMAC key in `Sign()`.
- PASS: Sign-string builders are `static`, cannot access secret, produce `timestamp+apiKey+recvWindow+payload` as specified.
- PASS: Signing marker key `"bybit.signed"` is namespaced and distinct from Binance `"binance.signed"`.
- PASS: `IsSigned`/`MarkSigned` are idempotent; no double-signing risk from the marker itself.
- CONCERN: `Sign(string signString)` lacks explicit `ArgumentNullException` guard before `Encoding.UTF8.GetBytes` (confidence: 65/100, non-blocking — consistent with Binance pattern, call path tightly controlled).
- CONCERN: `BybitOptions.SecretKey` has no `ToString()` redaction or `[JsonIgnore]` (confidence: 50/100, non-blocking — outside TASK-002 file scope, pre-existing from TASK-001).

---

## Final Verdict

**APPROVED**

No blocking security findings. Both files faithfully mirror the Binance pattern. HMAC construction is correct (key and data in the correct argument positions), the secret key is never retained as a string, never logged, never transmitted, and never included in any exception message. The signing marker is namespaced, idempotent, and correctly implements the double-signing prevention gate that the future `BybitSigningHandler` will rely on. The two CONCERN items are low-confidence, low-severity, and non-blocking.
