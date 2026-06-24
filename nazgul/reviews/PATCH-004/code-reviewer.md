# Code Review — PATCH-004
## StreamEngine.cs: dispose-during-reconnect race fix

**Reviewer**: Code Reviewer (automated)
**Date**: 2026-06-24
**Patch**: Working-tree diff to `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs`

---

### Finding 1: Root-cause fix is mechanically correct
- **Severity**: N/A (positive finding)
- **Confidence**: 98
- **File**: `StreamEngine.cs:512-513, 934-941, 989-991`
- **Category**: Correctness
- **Verdict**: PASS

The three-part fix directly addresses the root cause. `_disposeCts.CancelAsync()` at line 929 is awaited before the `_reconnectTask` snapshot at line 934. By the time `CancelAsync()` completes, the CTS's internal lock provides the full memory fence required for the pump's prior write to `_reconnectTask` to be visible to `DisposeAsync`. The pump cannot write to `_reconnectTask` AFTER `CancelAsync()` completes because `_pumpCts.Token` (linked to `_disposeCts.Token`) is already cancelled, causing `ReceiveAsync(ct)` to throw `OperationCanceledException`, which is caught at line 425 and returns cleanly — it never reaches the fire sites at 431/439. The watchdog likewise sees its linked-CTS token cancelled and returns at line 847. There is no window in which `_reconnectTask` can be assigned a live task after the snapshot at line 934.

### Finding 2: `_reconnectTask` plain field visibility is consistent with codebase pattern
- **Severity**: LOW
- **Confidence**: 75
- **File**: `StreamEngine.cs:151, 934`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking, confidence 75/100)

`_reconnectTask` is a plain (non-volatile) `Task?` field. The read in `DisposeAsync` at line 934 and the writes at lines 431/439/853 occur on different threads. The implicit memory barrier comes from `await _disposeCts.CancelAsync()` at line 929, which acquires CTS's internal `Monitor` lock — any write to `_reconnectTask` that preceded the token being raised is ordered before the lock release, making it visible to `DisposeAsync`'s continuation. This is the same pattern used for `_pumpTask` (line 125), `_heartbeatTask` (line 140), and `_idleCloseTask` (line 136), all plain `Task?` fields read from `DisposeAsync` without `volatile`. The pattern is self-consistent and correct in .NET's memory model. No change required.

### Finding 3: `ObjectDisposedException` exception filter is theoretically unreachable but correct to retain
- **Severity**: LOW
- **Confidence**: 85
- **File**: `StreamEngine.cs:513`
- **Category**: Correctness
- **Verdict**: PASS

With this fix in place, `_gate` is not disposed until AFTER `DisposeAsync` awaits `reconnectTask` (line 937), so the re-acquire at line 575 should throw `OperationCanceledException` (cancelled CTS) rather than `ObjectDisposedException` (disposed gate). Retaining the `ObjectDisposedException` catch is correct defensive practice — the `when (_isDisposed)` filter ensures it only swallows during legitimate disposal, not masking bugs. No action required.

### Finding 4: Single `_reconnectTask` field in last-writer-wins concurrent-fire scenario
- **Severity**: LOW
- **Confidence**: 80
- **File**: `StreamEngine.cs:431, 439, 853`
- **Category**: Correctness
- **Verdict**: PASS

If two fire sites race (e.g., pump receive-error at line 431 and watchdog at line 853 fire simultaneously), both write to `_reconnectTask` but the `Interlocked.CompareExchange` guard at line 502 ensures only one `ReconnectAsync()` body actually executes `ReconnectCoreAsync`. The losing task returns immediately via lines 499 or 502. `DisposeAsync` captures only the last-written task; the prior task completes in microseconds. This is pre-existing behavior; the patch does not make it worse.

### Finding 5: `#pragma warning disable CA1031` pair in `DisposeAsync` is correctly formed
- **Severity**: N/A
- **Confidence**: 99
- **File**: `StreamEngine.cs:938-940`
- **Category**: Code Style
- **Verdict**: PASS

The new pragma pair at lines 938-940 is correctly structured (disable/restore paired, justification comment present), matching the existing pattern at lines 925-927, 407-409, etc. The justification is accurate.

### Finding 6: `_reconnectTask` field comment is accurate and non-noisy
- **Severity**: N/A
- **Confidence**: 99
- **File**: `StreamEngine.cs:148-151`
- **Category**: Documentation
- **Verdict**: PASS

The comment explains WHY the field exists and its invariant (awaited before gate/semaphore/CTS disposal). It explains non-obvious "why" not "what" — consistent with the project's lean-comment mandate.

### Finding 7: `ReconnectAsync` exception-filter comments explain the `_isDisposed` rationale
- **Severity**: N/A
- **Confidence**: 99
- **File**: `StreamEngine.cs:508-513`
- **Category**: Documentation
- **Verdict**: PASS

The comment block at 508-511 correctly explains the critical constraint: do NOT check `_disposeCts.IsCancellationRequested` in the exception filter because the CTS may already be disposed by the time the filter runs. Using the `volatile bool _isDisposed` field (set first in `DisposeAsync` at line 918) is the correct alternative.

---

### Build and Test Verification

- `dotnet build CryptoExchanges.Net.sln --configuration Release`: 0 warnings, 0 errors.
- `dotnet test CryptoExchanges.Net.Http.Tests.Unit`: 98/98 passed.
- `Engine_DisposeDuringReconnectBackoff_ReleasesGateExactlyOnce_NoOverRelease`: PASSED (95 ms).

---

### Summary

- PASS: Root-cause fix (3-part: field + exception filters + fire-site assignment + DisposeAsync await) — directly and correctly addresses the unobserved `ObjectDisposedException` by ensuring no primitive is disposed while a reconnect task can still re-acquire it.
- PASS: `_isDisposed` volatile bool used in exception filter instead of `_disposeCts.IsCancellationRequested` — correctly avoids touching the disposed CTS.
- PASS: Await ordering in `DisposeAsync` — `CancelAsync()` before snapshot, snapshot before `_gate.Dispose()`. Order is sound.
- PASS: `#pragma warning disable CA1031` pairs — all correctly formed with justification comments.
- PASS: Build clean, all 98 unit tests pass.
- CONCERN: `_reconnectTask` plain field (non-volatile) read cross-thread (confidence: 75/100, non-blocking) — functionally correct due to the implicit CTS lock fence, consistent with the same pattern used for `_pumpTask`/`_heartbeatTask`/`_idleCloseTask` throughout the class. No action required.

---

## Final Verdict

APPROVED

The fix is minimal, targeted, and mechanically correct. The three fire sites now track the reconnect task, `ReconnectAsync` defensively filters dispose-induced exceptions using `_isDisposed` (not the potentially-disposed CTS), and `DisposeAsync` awaits the tracked task before releasing any primitive the reconnect could re-acquire. No new race conditions are introduced. Build is clean. The full Http unit suite passes.
