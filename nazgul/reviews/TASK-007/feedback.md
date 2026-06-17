# Review Gate Feedback — TASK-007: BybitErrorTranslator + BybitTimeSync

**Aggregate verdict**: CHANGES_REQUESTED
**Commit reviewed**: c6bfbb3
**Date**: 2026-06-17
**Policy**: require_all_approve=true, confidence_threshold=80, block_on_security_reject=true, auto_approve_concerns=true

## Reviewer Verdicts

| Reviewer | Verdict | Confidence |
|----------|---------|-----------|
| architect-reviewer | APPROVE | 93 |
| code-reviewer | APPROVE | 88 |
| security-reviewer | APPROVE | (no blocking; concerns below threshold) |
| api-reviewer | CHANGES_REQUESTED | 95 (blocking finding) |

## Blocking Items (MUST fix — REJECT at/above confidence threshold 80)

### BLOCK-1: `BybitTimeSync.ApplyOffset` missing zero-length guard on `offsetHolder`
- **Source**: api-reviewer, Finding 1, confidence 95 (>= 80 → blocking)
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs:14-19`
- **Issue**: `ApplyOffset` null-checks `offsetHolder` via `ArgumentNullException.ThrowIfNull` but does not validate length. A caller passing `new long[0]` triggers `IndexOutOfRangeException` from `Interlocked.Exchange(ref offsetHolder[0], offset)` — an undiagnosable runtime crash rather than a clean `ArgumentException`. Wrong for a public NuGet API surface.
- **Required fix**: Add immediately after the null check:
  ```csharp
  if (offsetHolder.Length < 1)
      throw new ArgumentException("offsetHolder must have at least one element.", nameof(offsetHolder));
  ```
- **Note**: security-reviewer independently flagged the same missing length validation (as a non-blocking concern), which corroborates this finding.

## Non-Blocking Concerns (auto-approved; address if cheap, may defer)

- **api-reviewer Finding 2 (CONCERN, conf 82)**: `ApplyOffset` public `long[]` holder leaks the internal slot convention into the NuGet surface; `BinanceTimeSync` keeps the Interlocked write inside the client. Cleaner to scope `ApplyOffset` `internal` (with `InternalsVisibleTo` for TASK-008 tests). Task manifest explicitly documents and justifies the public deviation for testability — deferred.
- **api-reviewer Finding 3 (LOW, conf 88)**: project-wide CS1591 suppression masks future missing docs. No current defect (all members documented). Tech debt.
- **security-reviewer**: no bounds check on clock-skew offset magnitude; no offsetHolder length validation (overlaps BLOCK-1). Both below threshold, non-blocking.
- **code-reviewer**: 4 LOW findings, all below threshold.
- **architect-reviewer**: 2 non-blocking concerns below threshold.

## Resolution
Address BLOCK-1, re-commit, and re-run the gate. Acceptance criteria 1-3 are otherwise met; build clean and 135 tests pass at c6bfbb3.
