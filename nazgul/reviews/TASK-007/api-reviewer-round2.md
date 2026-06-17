# API Review — TASK-007 Round 2 (Re-review of BLOCK-1 fix)

**Reviewer**: API Reviewer
**Date**: 2026-06-17
**Commit under review**: 456a208
**Scope**: Verify fix for BLOCK-1 from round 1 only. Round-1 non-blocking concerns (Finding 2 — scope of ApplyOffset; Finding 3 — CS1591) were deferred per gate disposition and are not re-raised here.

---

## Verification: BLOCK-1 — `ApplyOffset` missing bounds guard on `offsetHolder.Length`

**File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs:16-18`

**Fix applied** (lines 16-18 of current file):

```csharp
ArgumentNullException.ThrowIfNull(offsetHolder);
if (offsetHolder.Length < 1)
    throw new ArgumentException("offsetHolder must have at least one element.", nameof(offsetHolder));
```

**Verification result**: PASS

- `ArgumentNullException.ThrowIfNull(offsetHolder)` at line 16 handles the null case.
- `if (offsetHolder.Length < 1) throw new ArgumentException(...)` at lines 17-18 is placed immediately after the null check and before the `Interlocked.Exchange(ref offsetHolder[0], offset)` call at line 20.
- The exception message is descriptive and `nameof(offsetHolder)` is correctly used as the `paramName` argument — consistent with the project's guard posture in `BybitErrorTranslator.cs:13`.
- A caller passing `new long[0]` now receives a clean `ArgumentException` with a diagnostic message instead of an `IndexOutOfRangeException`. The original blocking defect is fully resolved.

---

## Build verification

Command: `dotnet build CryptoExchanges.Net.sln`
Result: **Build succeeded. 0 Warning(s). 0 Error(s).**

---

## Summary

- PASS: BLOCK-1 length guard — `offsetHolder.Length < 1` check is present, correctly positioned before `Interlocked.Exchange`, and throws `ArgumentException` with the right message and `paramName`. (confidence: 99)
- DEFERRED (from round 1, not re-raised): Finding 2 — `ApplyOffset` public visibility. (confidence: 82, non-blocking)
- DEFERRED (from round 1, not re-raised): Finding 3 — CS1591 project-wide suppression. (confidence: 88, non-blocking)

---

## Final Verdict

APPROVED

The sole blocking issue from round 1 has been correctly addressed. The fix is minimal, precise, and does not introduce new API surface changes.
