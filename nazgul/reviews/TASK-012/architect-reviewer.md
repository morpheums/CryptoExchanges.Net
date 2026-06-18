VERDICT: APPROVED

# Architect Review — TASK-012: OkxSigningHandler

**Reviewer:** Architect Reviewer
**File under review:** `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs`
**Date:** 2026-06-18

---

## Checklist

- [x] No `using` or `ProjectReference` in Core pointing to Http, Binance, or DI
- [x] No `using` or `ProjectReference` in Http pointing to Binance or DI
- [x] No previously-`internal` types made `public`
- [x] No new behavior added to existing public interfaces
- [x] Signing is a `DelegatingHandler`, not a client concern
- [x] Re-signs per attempt (strips prior headers before re-adding fresh ones)
- [x] `Func<long> timeOffset` pattern preserved for clock-skew sharing
- [x] No global or static mutable state introduced
- [x] `OkxSigningRequest.MarkSigned()` / `IsSigned()` pattern correctly used
- [x] Build passes with `TreatWarningsAsErrors=true` — 0 warnings, 0 errors

---

## Findings

### Finding: DELETE requests excluded from body signing — PASS
- **Severity:** LOW
- **Confidence:** 72
- **File:** `OkxSigningHandler.cs:56`
- **Category:** Architecture
- **Verdict:** CONCERN (non-blocking — confidence < 80)
- **Issue:** The body-read branch covers `POST` and `PUT` only. `DELETE` requests with a body are theoretically possible on OKX (some endpoints). Currently they would be signed with an empty body string. This is consistent with how Bybit handles DELETE (treats it as query-signed), and OKX v5 DELETE endpoints in practice do not carry a JSON body, so this is likely correct. However the comment does not mention DELETE explicitly to document this intentional decision.
- **Fix:** Add a short inline comment noting that OKX v5 DELETE endpoints do not carry a body (so the empty-string default is correct), to prevent future maintainers from accidentally adding a DELETE+body branch that skips body signing.
- **Pattern reference:** `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:44` (Bybit also excludes DELETE from body-read)

---

### Finding: Visibility — `internal sealed` DelegatingHandler
- **Severity:** N/A
- **Confidence:** 100
- **File:** `OkxSigningHandler.cs:17-19`
- **Category:** Architecture
- **Verdict:** PASS
- **Issue:** None. Class is `internal sealed`, keeping exchange internals private per ADR-001 (Invariant 3).
- **Pattern reference:** `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:12` / `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:17`

---

### Finding: Constructor is OkxOptions-agnostic
- **Severity:** N/A
- **Confidence:** 100
- **File:** `OkxSigningHandler.cs:17-18`
- **Category:** Architecture
- **Verdict:** PASS
- **Issue:** None. Constructor takes plain `string apiKey`, `string passphrase`, `OkxSignatureService`, and `Func<long>` — no reference to `OkxOptions` or any DI/Exchange-layer type. This respects the cross-layer invariant.

---

### Finding: Strict layering — no upward dependency
- **Severity:** N/A
- **Confidence:** 100
- **File:** `OkxSigningHandler.cs:1` (single `using CryptoExchanges.Net.Okx.Auth`)
- **Category:** Architecture
- **Verdict:** PASS
- **Issue:** None. Only imports `OkxSignatureService` from the sibling `Auth` namespace within the same exchange project. No Core, Http, DI, or other exchange assembly reference.

---

### Finding: Re-sign per attempt — header strip-then-add
- **Severity:** N/A
- **Confidence:** 100
- **File:** `OkxSigningHandler.cs:67-74`
- **Category:** Architecture
- **Verdict:** PASS
- **Issue:** None. All four `OK-ACCESS-*` headers are removed before re-adding on every attempt, which is exactly the pattern required to avoid duplicate headers on retry. Matches `BybitSigningHandler.cs:59-64` structurally.

---

### Finding: `Func<long> timeOffset` clock-skew sharing
- **Severity:** N/A
- **Confidence:** 100
- **File:** `OkxSigningHandler.cs:18, 46`
- **Category:** Architecture
- **Verdict:** PASS
- **Issue:** None. The `timeOffset` delegate (Invariant 7/clock-skew pattern) is captured via primary constructor and applied as `DateTimeOffset.UtcNow.AddMilliseconds(timeOffset())`, producing a fresh timestamp per attempt. Consistent with the Bybit pattern (`BybitSigningHandler.cs:40`) and the Binance `_offsetHolder` mechanism.

---

### Finding: `OkxSigningRequest.MarkSigned()` / `IsSigned()` usage
- **Severity:** N/A
- **Confidence:** 100
- **File:** `OkxSigningHandler.cs:27`
- **Category:** Architecture
- **Verdict:** PASS
- **Issue:** None. Uses `OkxSigningRequest.IsSigned(request)` as the gate, exactly mirroring `BinanceSigningRequest` and `BybitSigningRequest`.

---

### Finding: `ArgumentNullException.ThrowIfNull(request)` guard
- **Severity:** N/A
- **Confidence:** 100
- **File:** `OkxSigningHandler.cs:25`
- **Category:** Architecture
- **Verdict:** PASS
- **Issue:** None. Defensive null check on the incoming `HttpRequestMessage` before inspecting it. Mirrors Binance and Bybit patterns exactly.

---

### Finding: Runtime credential guards in ResignAsync
- **Severity:** N/A
- **Confidence:** 100
- **File:** `OkxSigningHandler.cs:38-43`
- **Category:** Architecture
- **Verdict:** PASS
- **Issue:** None. Guards against empty `apiKey`/`passphrase` inside `ResignAsync` with clear `InvalidOperationException` messages. This is a belt-and-suspenders check consistent with the doc comment's note that the composer's secret-gated finalizer should normally prevent this state. Not present in Binance/Bybit (they have no passphrase), but OKX-appropriate.

---

### Finding: No global/static mutable state
- **Severity:** N/A
- **Confidence:** 100
- **File:** `OkxSigningHandler.cs` (all)
- **Category:** Architecture
- **Verdict:** PASS
- **Issue:** None. All state is captured via primary-constructor parameters. No static fields. Complies with thread-safety requirement (Invariant 11).

---

### Finding: `RequestUri!` null-forgiving operator
- **Severity:** LOW
- **Confidence:** 60
- **File:** `OkxSigningHandler.cs:52`
- **Category:** Architecture
- **Verdict:** CONCERN (non-blocking — confidence < 80)
- **Issue:** `request.RequestUri!.PathAndQuery` uses a null-forgiving operator. In practice `HttpRequestMessage.RequestUri` should always be set before a signing handler sees it, but this could silently produce a `NullReferenceException` at runtime if a misconfigured `OkxHttpClient` sends a request without a URI. Bybit does the same (`BybitSigningHandler.cs:51`) so this is consistent with the established pattern. The risk is real but the probability is extremely low given the HTTP client factory setup.
- **Fix:** Consider adding an explicit guard: `if (request.RequestUri is null) throw new InvalidOperationException("OKX signed request has no RequestUri.")` — or leave as-is since it matches the Bybit pattern and the Http layer already validates URI construction.
- **Pattern reference:** `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:51`

---

## Summary

- PASS: Visibility (`internal sealed`) — no public surface added, ADR-001 respected
- PASS: Layering / dependency direction — only `CryptoExchanges.Net.Okx.Auth` imported; no Core, Http, DI, or other exchange reference
- PASS: OkxOptions-agnostic constructor — plain primitive + service + offset func; no cross-layer leak
- PASS: Re-sign per attempt — all four `OK-ACCESS-*` headers stripped and re-added with a fresh timestamp/signature
- PASS: `Func<long> timeOffset` clock-skew pattern — applied correctly per attempt via `DateTimeOffset.UtcNow.AddMilliseconds(timeOffset())`
- PASS: `OkxSigningRequest.IsSigned()` gate — matches Binance/Bybit pattern exactly
- PASS: `ArgumentNullException.ThrowIfNull` guard on entry — matches reference pattern
- PASS: No global/static mutable state — all state via primary-constructor capture
- PASS: Build — 0 warnings, 0 errors with `TreatWarningsAsErrors=true`
- CONCERN: DELETE-with-body not documented — the empty-string body default for DELETE is correct for OKX v5, but the intent should be commented to prevent future regression (confidence: 72/100, non-blocking)
- CONCERN: `RequestUri!` null-forgiving — consistent with Bybit pattern but could produce an opaque NRE; low probability given pipeline setup (confidence: 60/100, non-blocking)

---

## Final Verdict

**APPROVED**

The implementation is a clean, pattern-conformant OKX signing handler. It correctly: enforces `internal sealed` visibility, remains OkxOptions-agnostic, re-signs on every attempt with a fresh timestamp, strips stale headers before retry, delegates to `OkxSignatureService` for signing, and carries no global mutable state. Both concerns are low-confidence, non-blocking observations consistent with (or less severe than) the same patterns already present in the Bybit reference implementation.
