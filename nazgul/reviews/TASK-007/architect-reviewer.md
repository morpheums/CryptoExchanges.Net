# Architect Review — TASK-007: BybitErrorTranslator + BybitTimeSync

**Reviewer**: Architect Reviewer
**Date**: 2026-06-17
**Task**: TASK-007
**Files reviewed**:
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs`

---

### Finding 1: Dependency direction is correct
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:12-13`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The Bybit `.csproj` references only `CryptoExchanges.Net.Core` and `CryptoExchanges.Net.Http` — exactly the same two-dependency pattern as Binance. No upward or sideways leakage to DI or other exchange assemblies.

---

### Finding 2: IExchangeErrorTranslator interface contract fully satisfied
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs:8-11`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `BybitErrorTranslator` implements `IExchangeErrorTranslator.Translate(HttpResponseMessage, string)` with the exact signature defined in Core. No new members were added to the interface.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Interfaces/IExchangeErrorTranslator.cs:14`

---

### Finding 3: `IExchangeErrorTranslator` resolved via GlobalUsings — implicit but correct
- **Severity**: LOW
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs:1-4`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `IExchangeErrorTranslator` is used at line 8 but its namespace (`CryptoExchanges.Net.Core.Interfaces`) is not in the file-level `using` directives — it arrives via `GlobalUsings.cs:5`. This is intentional per the task manifest ("same `using` set… `Core.Interfaces` + `System.Text.Json` come from GlobalUsings"), and it matches BinanceErrorTranslator which omits those same namespaces from file-level usings for the same reason. The slight concern is that a reader of the file in isolation cannot tell where `IExchangeErrorTranslator` comes from without knowing about GlobalUsings. This is an existing codebase pattern (identical in Binance), not a new deviation introduced here.
- **Fix**: No action required. Add a comment only if the team considers self-contained `using` declarations important for readability; the global-using pattern is already established.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceErrorTranslator.cs:1-5`

---

### Finding 4: `public` visibility on both classes — justified by documented exception
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs:8`, `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs:7`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `BybitErrorTranslator` is `public` for the same reason `BinanceErrorTranslator` is `public`: it is resolved across assembly boundaries by the resilience pipeline and DI, so `internal` is not viable. The task manifest explicitly documents this as the approved exception. `BybitTimeSync` is `public` matching `BinanceTimeSync`. Both are stateless utilities with no sensitive internals exposed.

---

### Finding 5: `retCode == 0` guard returns an ExchangeApiException rather than throwing
- **Severity**: MEDIUM
- **Confidence**: 68
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs:17-20`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: When `retCode == 0` the method returns `new ExchangeApiException(text, code, body)` as a defensive guard. The comment explains "Callers should not reach the translator with a success envelope." However, returning a non-null exception from a success envelope could confuse callers that assume a non-null return always means a business error. The resilience pipeline only calls `Translate` on non-success HTTP statuses, so the guard is not reachable in normal flow, but the return value is semantically odd. The Binance reference has no analogous success-sentinel guard.
- **Fix**: Consider `throw new InvalidOperationException(...)` instead of returning a typed exception, which would surface the caller bug immediately rather than silently propagating an `ExchangeApiException` for a successful response. Low-urgency code-quality point, not a layering violation.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceErrorTranslator.cs:19`

---

### Finding 6: `ApplyOffset` deviation — encapsulated Interlocked write
- **Severity**: LOW
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs:14-20`
- **Category**: Architecture
- **Verdict**: PASS (deviation is architecturally sound and explicitly documented)
- **Issue**: `BybitTimeSync.ApplyOffset` encapsulates `Interlocked.Exchange(ref offsetHolder[0], offset)` — the Binance pattern leaves this write inline in `BinanceExchangeClient.SyncServerTimeAsync`. This deviation is a conscious design choice, documented in the task manifest under "Deviation from the Binance pattern." The reasoning (independent testability of the holder-write in TASK-008) is valid. The thread-safety invariant is preserved: `Interlocked.Exchange` on the array element is identical in behavior to the inline version. The `ArgumentNullException.ThrowIfNull(offsetHolder)` null-guard is a small improvement over the Binance inline code. The deviation creates no new coupling or layering issue.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceTimeSync.cs:8`

---

### Finding 7: Ordered if-cascade matches Binance's structural template
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs:22-48`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Rate-limit checked before auth, auth before balance, balance before order, unknown falls through to `ExchangeApiException` — mirrors Binance's cascade order exactly. `RetryAfterReader.GetDelay(response)` is used for rate-limit retry delays, matching the established Http-layer pattern.

---

### Finding 8: `BybitTimeSync` has no explicit `using` directives
- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs:1`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `Interlocked` is used but no `using System.Threading` directive appears. This compiles because `Interlocked` is included in the SDK's implicit global usings. Consistent with how `BinanceTimeSync` also has no using directives. Minor readability note only.
- **Fix**: No action required; consistent with Binance pattern and compiles cleanly.

---

### Finding 9: No new public API surface added to Core interfaces
- **Severity**: N/A
- **Confidence**: 100
- **File**: All Core interface files unchanged.
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Neither `IMarketDataService`, `ITradingService`, `IAccountService`, nor `IExchangeClient` were modified. No breaking API change.

---

## Summary

- PASS: Dependency direction — Bybit csproj references Core + Http only, no upward/sideways leakage.
- PASS: Interface contract — `IExchangeErrorTranslator` implemented correctly, no new interface members added.
- PASS: Pattern fidelity — `BybitErrorTranslator` is structurally identical to `BinanceErrorTranslator`: same signature, same `Parse` helper shape, same `(int? code, string? msg)` return, same ordered if-cascade, same fallthrough `ExchangeApiException`.
- PASS: `ApplyOffset` deviation — encapsulating the `Interlocked.Exchange` write is sound, improves testability, preserves thread-safety, and is explicitly task-manifest-documented.
- PASS: Visibility — `public` on both classes is justified by cross-assembly resolution requirements; consistent with Binance.
- PASS: Build — `dotnet build` produces 0 warnings, 0 errors with `TreatWarningsAsErrors=true`.
- CONCERN: `retCode == 0` returns `ExchangeApiException` rather than throwing — semantically ambiguous for a success envelope, though unreachable in normal pipeline flow. (confidence: 68/100, non-blocking)
- CONCERN: `IExchangeErrorTranslator` namespace not in file-level `using` (comes via GlobalUsings) — existing codebase pattern, not a new deviation. (confidence: 72/100, non-blocking)

---

## Final Verdict

**APPROVED** — All checks pass. Two non-blocking concerns noted (both below confidence threshold of 80). No blocking issues found.

VERDICT: APPROVE
CONFIDENCE: 93
