# API Review — TASK-007: BybitErrorTranslator + BybitTimeSync

**Reviewer**: API Reviewer
**Date**: 2026-06-17
**Files reviewed**:
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs`

---

## Findings

### Finding 1: ApplyOffset missing bounds guard on offsetHolder.Length
- **Severity**: HIGH
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs:14-19`
- **Category**: API Design
- **Verdict**: REJECT (blocking — confidence >= 80)
- **Issue**: `ApplyOffset` null-checks `offsetHolder` via `ArgumentNullException.ThrowIfNull` but does not validate `offsetHolder.Length >= 1`. A caller who passes `new long[0]` gets an `IndexOutOfRangeException` from `Interlocked.Exchange(ref offsetHolder[0], offset)` — a runtime crash with no diagnostic message rather than a clean `ArgumentException`. This is always wrong for a public API surface.
- **Fix**: Add `if (offsetHolder.Length < 1) throw new ArgumentException("offsetHolder must have at least one element.", nameof(offsetHolder));` immediately after the null check.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs:13` — `ArgumentNullException.ThrowIfNull(response)` shows the project's defensive guard posture.

---

### Finding 2: BybitTimeSync.ApplyOffset — long[] holder is an awkward public contract
- **Severity**: MEDIUM
- **Confidence**: 82
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs:14`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — task manifest explicitly documents this deviation and the rationale is sound for testability)
- **Issue**: `ApplyOffset(long serverTimeMs, long localNowMs, long[] offsetHolder)` is public API on a NuGet package. The `long[]` as a shared mutable state slot is an implementation detail of the signing pipeline. Any external caller who copies this pattern is now coupled to the internal storage shape. `BinanceTimeSync` deliberately avoids exposing this — the `Interlocked.Exchange` stays inside `BinanceExchangeClient.SyncServerTimeAsync`. Publishing `ApplyOffset` effectively leaks the internal slot convention into the public contract.
- **Fix**: If `ApplyOffset` is needed for testability (TASK-008), scope it `internal`. Unit tests in the same assembly (or granted `InternalsVisibleTo`) can exercise it directly. The `ComputeOffset` pure function remains public and is sufficient for asserting offset sign/magnitude. Alternatively, if `ApplyOffset` must remain public, add the bounds guard from Finding 1 and document explicitly that `offsetHolder` must be a single-element array.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceTimeSync.cs:8` — pure, no holder argument.

---

### Finding 3: CS1591 blanket suppression masks future missing XML docs
- **Severity**: LOW
- **Confidence**: 88
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:8`
- **Category**: NuGet Conventions
- **Verdict**: PASS (docs are present on all members; suppression is inherited from Binance pattern; informational only)
- **Issue**: Both `ComputeOffset` and `ApplyOffset` have XML doc summaries — no actual missing-doc defect. However, the csproj suppresses CS1591 project-wide, meaning future members can omit docs silently.
- **Fix**: No immediate action required. Track as tech debt to tighten or remove the CS1591 suppression once the API surface stabilises post-preview.
- **Pattern reference**: `Directory.Build.props:8` — `GenerateDocumentationFile=true` is the project intent.

---

### Finding 4: BybitErrorTranslator — public visibility is correct and well-justified
- **Severity**: N/A
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs:8`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. `IExchangeErrorTranslator` lives in Core and is resolved across assemblies via the HTTP pipeline (`ErrorTranslationHandler`, `ResilientHttpClientServiceCollectionExtensions`). Making the concrete class `public` is the only viable option; matches `BinanceErrorTranslator` exactly.

---

### Finding 5: BybitErrorTranslator structural consistency with BinanceErrorTranslator
- **Severity**: N/A
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. `sealed class`, same `Translate(HttpResponseMessage, string)` signature, same `Parse(body)` private helper shape, same if-cascade order (rate-limit → auth → balance → order → fallback), same `RetryAfterReader.GetDelay` usage. The defensive guard for `retCode == 0` is a Bybit-specific correct addition, documented inline.

---

### Finding 6: BybitTimeSync.ComputeOffset matches Binance pure function
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs:10`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. Identical signature and body to `BinanceTimeSync.ComputeOffset`. XML doc present.

---

## Summary

- PASS: `BybitErrorTranslator` public visibility — cross-assembly DI resolution requires it; matches Binance pattern exactly.
- PASS: `BybitErrorTranslator` structural shape — sealed class, `Translate` signature, `Parse` helper, if-cascade order, `RetryAfterReader` usage.
- PASS: `BybitTimeSync.ComputeOffset` — pure function, correct signature and semantics.
- CONCERN: `BybitTimeSync.ApplyOffset` public visibility — leaks internal `long[]` holder convention into NuGet surface; task manifest justifies it for testability but `internal` is the cleaner choice. (confidence: 82, non-blocking)
- REJECT: `ApplyOffset` missing `offsetHolder.Length >= 1` guard — passes a zero-length array to `Interlocked.Exchange(ref offsetHolder[0], ...)` which throws `IndexOutOfRangeException` instead of `ArgumentException`. (confidence: 95, blocking)

---

## Final Verdict

CHANGES_REQUESTED

**Blocking issue**: `BybitTimeSync.ApplyOffset` does not guard against a zero-length `offsetHolder` array. A call with `new long[0]` produces an `IndexOutOfRangeException` rather than a diagnostic `ArgumentException`. Add a length check immediately after the null check:

```csharp
if (offsetHolder.Length < 1)
    throw new ArgumentException("offsetHolder must have at least one element.", nameof(offsetHolder));
```

Once the bounds guard is added, the remaining concern (public visibility of `ApplyOffset`) is non-blocking and may be deferred.
