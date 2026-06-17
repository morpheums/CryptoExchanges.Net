# Security Review — TASK-005: BybitHttpClient + IBybitHttpClient

**Commit reviewed**: 2a598c8
**Reviewer**: Security Reviewer
**Files under review**:
- `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs`
- `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs`

**Supporting context reviewed**:
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs`
- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- `src/CryptoExchanges.Net.Bybit/BybitOptions.cs`

---

## Findings

### Finding 1: StringContent is fully buffered — ReadAsStringAsync is re-entrant on retry
- **Severity**: LOW (verification finding — no defect)
- **Confidence**: 98
- **File**: `BybitHttpClient.cs:44`, `Resilience/BybitSigningHandler.cs:42`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `BinanceSigningHandler.cs:39` (Binance form-body re-read pattern)

`PostAsync` creates `new StringContent(json, Encoding.UTF8, "application/json")`. `StringContent` inherits from `ByteArrayContent`, which stores the encoded bytes in an in-memory array. `ReadAsStringAsync` re-reads that array from position 0 on every call — it is not a forward-only stream. The signing handler at line 42 calls `request.Content.ReadAsStringAsync(ct)`, which will faithfully return the same UTF-8 string that was serialized in `PostAsync:43`. There is no encoding divergence: the body is serialized with `JsonSerializer.Serialize` (uses UTF-8 internally), wrapped in `StringContent(json, Encoding.UTF8, ...)`, and the handler reads it back as a UTF-8 string. The signed bytes and the wire bytes are identical on first attempt and on every retry.

---

### Finding 2: Signing integrity — mark-and-strip pattern correctly implemented for header-based signing
- **Severity**: LOW (verification finding — no defect)
- **Confidence**: 97
- **File**: `BybitHttpClient.cs:31,46,57`, `Resilience/BybitSigningHandler.cs:55-60`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `BinanceSigningHandler.cs:86-93` (StripSigning query-string pattern)

The client sets only the signed options key via `BybitSigningRequest.MarkSigned`. No timestamp, HMAC, or API key is touched in the client. The signing handler in `ResignAsync` strips all three Bybit signing headers (`X-BAPI-TIMESTAMP`, `X-BAPI-RECV-WINDOW`, `X-BAPI-SIGN`) with `Remove` before re-adding them at lines 55-60. This is the header-based analogue of the Binance `StripSigning` query-string pattern. On a Polly retry the handler runs again, strips the stale headers, and writes fresh ones — no duplication is possible.

---

### Finding 3: No secrets in BybitHttpClient — apiKey/secretKey not present
- **Severity**: LOW (verification finding — no defect)
- **Confidence**: 99
- **File**: `BybitHttpClient.cs:16-79`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `BinanceHttpClient.cs:16` (no secrets in HTTP client)

The `BybitHttpClient` constructor takes only `HttpClient`. Neither `BybitOptions`, `apiKey`, nor `secretKey` is a field or parameter. The `JsonOptions` static field contains only deserialization settings. No HMAC is computed inline. There is no `ToString()` override. No exception message in this file references credentials. The API key is added exclusively in `BybitSigningHandler.SendAsync:24-25`, and the secret key bytes exist solely inside `BybitSignatureService._secretKeyBytes`.

---

### Finding 4: Query string escaping is correctly applied
- **Severity**: LOW (verification finding — no defect)
- **Confidence**: 99
- **File**: `BybitHttpClient.cs:75`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `BinanceHttpClient.cs:91-92`

`BuildQueryString` at line 75 applies `Uri.EscapeDataString` to both key and value for every parameter, matching the reference pattern. GET and DELETE both route through `BuildUrl` → `BuildQueryString`. POST sends parameters as a JSON body, so the query string for POST is the bare `endpoint` string only — no user-supplied values appear there unescaped.

---

### Finding 5: Endpoint passed unescaped
- **Severity**: LOW
- **Confidence**: 75
- **File**: `BybitHttpClient.cs:30,45,56`
- **Category**: Security
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: The `endpoint` string is used verbatim in `BuildUrl` and the `HttpRequestMessage` constructor without `Uri.EscapeDataString`. If endpoint composition ever moves to include caller-supplied strings, injection would be possible.
- **Fix**: No immediate action required. If endpoint values ever incorporate runtime user input, apply `Uri.EscapeDataString` to path segments.
- **Pattern reference**: `BinanceHttpClient.cs:30,54,65` (identical unescaped-endpoint pattern in reference implementation)

The `endpoint` is not a user-supplied string — it originates from internal service classes that specify Bybit API path literals. No injection path exists in the current architecture. The pattern is consistent with the established Binance precedent.

---

### Finding 6: ReadFromJsonAsync called without JsonException catch on success path
- **Severity**: LOW
- **Confidence**: 60
- **File**: `BybitHttpClient.cs:33,48,59`
- **Category**: Security
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: All three methods call `response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)` without a surrounding `try/catch (JsonException)`. A 2xx response with a malformed or non-JSON body (e.g., a CDN or WAF HTML error page) would throw an unhandled `JsonException` that propagates raw to the caller, potentially with response-body content in the exception message.
- **Fix**: Wrap `ReadFromJsonAsync` in a try/catch for `JsonException` and surface it as a typed SDK exception, consistent with how `BinanceErrorTranslator.cs:39-51` wraps `JsonDocument.Parse`. Should be addressed consistently across both exchange clients in a future hardening task.
- **Pattern reference**: `BinanceErrorTranslator.cs:39-51` (JsonException catch pattern for error-body parsing)

The Binance `BinanceHttpClient.cs` has the identical uncaught pattern (lines 33, 57, 68), so this is a shared architectural gap not introduced by TASK-005.

---

### Finding 7: BybitOptions.SecretKey is not serialization-annotated
- **Severity**: LOW (verification finding — no defect)
- **Confidence**: 98
- **File**: `BybitOptions.cs:15`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: N/A
- **Fix**: N/A
- **Pattern reference**: `BinanceOptions.cs` (no [JsonInclude] on secret fields)

`BybitOptions` has no `[JsonInclude]`, `[DataMember]`, `[Serializable]`, or `ToString()` override. Both `ApiKey` and `SecretKey` are plain auto-properties with no annotations. The default `Object.ToString()` (returns type name only) would not leak secrets.

---

## Summary

- PASS: Signing integrity — `BybitSigningHandler` correctly strips all three `X-BAPI-*` signing headers before re-signing on each attempt (retry-safe). Mark-and-strip pattern fully implemented.
- PASS: StringContent re-readability — `StringContent` is backed by an in-memory byte array; `ReadAsStringAsync` is re-entrant. The handler signs the exact bytes the client sends on every attempt.
- PASS: Credential isolation — `BybitHttpClient` holds no reference to `apiKey`, `secretKey`, or `BybitOptions`. No inline HMAC. No credential in exception messages, fields, or `ToString()`.
- PASS: Query string escaping — `Uri.EscapeDataString` applied to both key and value at `BybitHttpClient.cs:75`, consistent with `BinanceHttpClient.cs:91`.
- PASS: BybitOptions secrets not serializable — no `[JsonInclude]` or `[DataMember]` on `ApiKey`/`SecretKey`.
- CONCERN: Endpoint passed unescaped — matches Binance reference pattern; endpoint is internal, not user-supplied; no injection path exists today (confidence: 75/100, non-blocking).
- CONCERN: `ReadFromJsonAsync` has no `JsonException` catch on success-path responses — shared architectural gap also present in `BinanceHttpClient.cs`; not introduced by TASK-005; recommend addressing in a future hardening task (confidence: 60/100, non-blocking).

---

## Final Verdict

APPROVED — confidence 95/100

All security-relevant properties hold: signing integrity is correct and retry-safe, no credentials are present or loggable in the client, query strings are properly escaped, and the body the handler signs is byte-for-byte what the wire receives. The two concerns (unescaped endpoint, missing JsonException on success path) are both consistent with the established Binance reference pattern and carry confidence below the 80-point blocking threshold. Neither is introduced by this task.
