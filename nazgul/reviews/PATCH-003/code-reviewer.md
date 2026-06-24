# Code Review: PATCH-003 — Reconnect-vs-Subscribe Socket-Disposal Race
**Commit**: d970e91 (amended from 44b4c39)
**Branch**: patch/PATCH-003-reconnect-subscribe-race
**Reviewer**: Code Reviewer (automated)
**Date**: 2026-06-24

**Build**: 0 warnings, 0 errors (`TreatWarningsAsErrors=true`)
**Unit tests**: 98/98 pass (all Http unit tests, including the two new regression tests).

---

## Re-review: Findings from Round 1 — Resolution Verified

### Finding 1 (HIGH): Gate double-release on dispose-during-backoff — RESOLVED

The `holdsGate` flag pattern correctly addresses the double-release. Path analysis on the fixed code (`StreamEngine.cs:521-619`):

- **Entry**: L522 `WaitAsync` → `holdsGate = true` (L523).
- **Normal release for backoff**: L553 `Release()`, L554 `holdsGate = false`.
- **Path B — OCE during `Task.Delay`**: catch at L559 (`return`), jumps directly to outer `finally`. `holdsGate == false` → no release. Correct: zero releases for a gate not held.
- **Path C — OCE during re-acquire `WaitAsync` (L564)**: `holdsGate` stays `false` (L565 never executed), outer `finally` skips release. Correct.
- **Normal success**: L564 `WaitAsync` → L565 `holdsGate = true`, L612 `return`, outer `finally` releases once. Balanced.
- **Max-attempts path**: `holdsGate = true` throughout, outer `finally` releases once. Balanced.

The regression test `Engine_DisposeDuringReconnectBackoff_ReleasesGateExactlyOnce_NoOverRelease` (line 1395) is deterministic: it waits for `LiveCount == 0` (teardown complete, loop in backoff), then calls `DisposeAsync()` mid-backoff, and asserts no `SemaphoreFullException` via the `UnobservedTaskException` handler. This test would reliably fail against the unconditional-release version.

### Finding 2 (MEDIUM): Handle stuck in `Reconnecting` post-success — RESOLVED

`Interlocked.Exchange(ref _reconnecting, 0)` is now at line 601, inside the gate, before the K2 replay and Live broadcast. A `SubscribeAsync` winning the gate after the success `return` reads `_reconnecting == 0` and takes the normal open-and-subscribe path. `ReconnectAsync.finally` (line 505) re-clears to 0 idempotently. Window closed.

### Finding 3 (LOW): CA1308 pragma span — RESOLVED

`#pragma warning disable/restore CA1308` now wraps only the two `ToLowerInvariant()` call sites (lines 1473-1476 in `StreamEngineTests.cs`), not the entire `Key()` method body. The `return` statement is outside the suppressed span. Matches the minimum-span convention from `SymbolMapper.cs:46-48`.

### Finding 4 (LOW): Private method `//` vs `///` — Accepted as-is

Left as `//` block comments per the LEAN comment mandate for private methods. Confidence was 50 and the project has mixed practice. No action required.

---

## Full Conventions Check (amended commit)

- All new `await` calls use `.ConfigureAwait(false)`. PASS.
- `_disposeCts.Token` forwarded to all async operations in reconnect path. PASS.
- All new `#pragma warning disable CA1031` blocks are paired, have inline justifications. PASS.
- No new public/internal members without XML doc. PASS.
- `holdsGate` is a stack-local — no shared-state threading concern. PASS.
- No new `lock` blocks. PASS.
- `TeardownSocketAsync` and `CloseAllSubscriptionsAsync` remain private, no guards needed. PASS.
- One type per file convention unaffected (no new top-level types). PASS.

---

## Summary

- PASS: Finding 1 (HIGH) — gate double-release fixed; `holdsGate` flag; new regression test verified.
- PASS: Finding 2 (MEDIUM) — `_reconnecting=0` moved inside gate before Live broadcast; window closed.
- PASS: Finding 3 (LOW) — CA1308 pragma tightened to two call sites.
- PASS: Finding 4 (LOW) — accepted as-is per LEAN comment mandate.
- PASS: Build clean (0 warnings, 0 errors).
- PASS: 98/98 Http unit tests pass.
- PASS: No deadlock paths.
- PASS: Gate balance verified on all control flow paths.

---

## Final Verdict

**APPROVED**

All three blocking and non-blocking findings from Round 1 have been correctly addressed. The gate-balance fix is mechanically sound across every control flow path. The two regression tests are deterministic and cover the previously untested failure modes.
