# Security Review — TASK-007: BybitErrorTranslator + BybitTimeSync

**Reviewer**: Security Reviewer
**Date**: 2026-06-17
**Files reviewed**:
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs`

---

## Findings

### Finding 1: retCode == 0 success envelope guard
- **Severity**: LOW
- **Confidence**: 90
- **File**: `BybitErrorTranslator.cs:19-20`
- **Verdict**: PASS
- **Issue**: The `retCode == 0` guard is present and fires before any error branch, preventing success envelopes from being misclassified as errors. Acceptance criteria satisfied.
- **Fix**: None required.
- **Pattern reference**: Bybit-specific addition; Binance has no equivalent because Binance does not use a zero-code success envelope.

---

### Finding 2: Auth-failure code classification correctness
- **Severity**: HIGH
- **Confidence**: 95
- **File**: `BybitErrorTranslator.cs:30-32`
- **Verdict**: PASS
- **Issue**: Codes 10003 (invalid API key), 10004 (invalid signature), 10005 (permission denied), 10010 (IP allowlist mismatch), and 33004 (expired key) are all routed to `AuthenticationException`. HTTP 401 and 403 are also caught. Auth codes and rate-limit codes (10006, 10018) are mutually exclusive — no overlap that could misclassify an auth failure as a retryable rate-limit, which would risk credential retry storms.
- **Fix**: None required.
- **Pattern reference**: `BinanceErrorTranslator.cs:22-24`

---

### Finding 3: Raw body attached to exceptions — secret leakage risk
- **Severity**: MEDIUM
- **Confidence**: 85
- **File**: `BybitErrorTranslator.cs:20, 25, 32, 37, 45, 48`
- **Verdict**: PASS
- **Issue**: Every exception type carries the raw response `body` as `RawBody`. Bybit V5 error responses are server-generated JSON containing only `retCode`, `retMsg`, `retExtInfo`, and `result`. The exchange does not reflect the caller's `X-API-KEY` header or `sign` query parameter back in error bodies. The `text` field used as the exception `Message` is constructed from `retCode`/`retMsg` only — no body interpolation in `Message`. This is identical to the Binance pattern. No new leakage risk introduced.
- **Fix**: None required.
- **Pattern reference**: `ExchangeExceptions.cs:21`, `BinanceErrorTranslator.cs:20`

---

### Finding 4: JsonDocument.Parse on attacker-influenced body
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `BybitErrorTranslator.cs:51-68`
- **Verdict**: PASS
- **Issue**: `JsonDocument.Parse(body)` is wrapped in `try/catch (JsonException)` and the document is `using`-disposed. Malformed JSON returns `(null, null)` and falls through to `ExchangeApiException`. Default .NET max depth of 64 applies — sufficient for the flat `{retCode, retMsg}` envelope. Structurally identical to `BinanceErrorTranslator.cs:36-50`.
- **Fix**: None required.
- **Pattern reference**: `BinanceErrorTranslator.cs:36-50`

---

### Finding 5: BybitTimeSync — no bounds check on computed offset (replay-window DoS concern)
- **Severity**: HIGH
- **Confidence**: 60
- **File**: `BybitTimeSync.cs:10, 14-20`
- **Verdict**: CONCERN (non-blocking — confidence 60/100, below 80 threshold)
- **Issue**: `ComputeOffset` is `serverTimeMs - localNowMs` with no bounds checking. An extreme server-time value (MITM or misconfigured endpoint) would be stored verbatim. This causes signed requests to be rejected by Bybit (outside recv-window), constituting a denial-of-service rather than a security bypass — Bybit rejects timestamps outside the recv-window rather than accepting them. HTTPS transport makes MITM implausible in normal deployment. The Binance inline pattern also has no bounds check.
- **Fix** (non-blocking suggestion): Add a sanity check — if `Math.Abs(offset) > 60_000` (60 seconds), log a warning or throw before writing into the holder.
- **Pattern reference**: `BinanceExchangeClient.cs:106`

---

### Finding 6: Interlocked.Exchange on array element — thread safety
- **Severity**: LOW
- **Confidence**: 95
- **File**: `BybitTimeSync.cs:18`
- **Verdict**: PASS
- **Issue**: `Interlocked.Exchange(ref offsetHolder[0], offset)` performs an atomic 64-bit write to the shared holder element. The `ref` to an array element is valid and well-defined on .NET. Exactly mirrors the Binance pattern. No tearing or race condition.
- **Fix**: None required.
- **Pattern reference**: `BinanceExchangeClient.cs:106`

---

### Finding 7: offsetHolder length not validated
- **Severity**: LOW
- **Confidence**: 75
- **File**: `BybitTimeSync.cs:14-19`
- **Verdict**: CONCERN (non-blocking — confidence 75/100, below 80 threshold)
- **Issue**: `ArgumentNullException.ThrowIfNull(offsetHolder)` guards against null but does not validate `offsetHolder.Length >= 1`. Passing an empty `long[0]` throws `IndexOutOfRangeException` at line 18. Not a security vulnerability — the holder is always constructed as `new long[1]` by the client — but the public API surface does not enforce the length contract.
- **Fix** (non-blocking suggestion): Add `if (offsetHolder.Length < 1) throw new ArgumentException("offsetHolder must have at least one element.", nameof(offsetHolder));` after the null check.
- **Pattern reference**: Binance performs the Interlocked write inline in the client where the array is locally constructed, so no separate guard is needed there.

---

## Summary

- PASS: Auth-failure code classification — 10003/10004/10005/10010/33004 correctly route to `AuthenticationException`; no overlap with rate-limit codes; no credential retry storm risk.
- PASS: Rate-limit classification — 429, 10006, 10018 route to `RateLimitExceededException` with `RetryAfter` from headers.
- PASS: `retCode == 0` guard — success envelopes caught before any error branch, never misclassified.
- PASS: `JsonDocument.Parse` safety — wrapped in `try/catch (JsonException)`, using-disposed, identical to Binance.
- PASS: Raw `body` on `RawBody` property — not echoed in `Message`; Bybit error responses do not reflect auth material; consistent with Binance.
- PASS: `Interlocked.Exchange` atomic write — correct pattern, no tearing.
- CONCERN: No bounds check on computed offset in `BybitTimeSync` — extreme server-time values could produce an unusable offset (DoS via broken signing), but not a security bypass. Confidence 60/100, non-blocking.
- CONCERN: `offsetHolder` length not validated — `IndexOutOfRangeException` possible with empty array; not a security issue. Confidence 75/100, non-blocking.

---

## Final Verdict

**APPROVED**

No blocking security issues found. Both concerns are below the confidence threshold of 80 and neither represents a demonstrable exploitable vulnerability. The implementation correctly mirrors the Binance reference pattern for error translation, JSON parsing, exception construction, and atomic offset writing.
