# Security Review — TASK-044

## Verdict: APPROVED

## Findings

### FINDING-1: No credential material in committed artifacts
- **Severity**: N/A
- **Confidence**: 100
- **File**: all new files
- **Blocking**: No
- **Result**: PASS — No API keys, secret keys, tokens, passwords, or other credential material appears anywhere in the diff. The engine is exchange-agnostic and operates on opaque `byte[]` frames with no knowledge of authentication.

---

### FINDING-2: Backoff jitter cannot produce zero or negative delay
- **Severity**: N/A
- **Confidence**: 100
- **File**: BackoffSchedule.cs:62-67
- **Blocking**: No
- **Result**: PASS — Constructor guard (`initial <= TimeSpan.Zero` throws) ensures `_current` is always positive. The ±10% jitter range is computed only when `jitterRangeMs > 0` (small-delay short-circuit). The `if (jittered <= TimeSpan.Zero) jittered = delay;` guard at line 67 is a correct defensive fallback. The reconnect loop always calls `await Task.Delay(delay, _disposeCts.Token)` (StreamEngine.cs:492) before any connect attempt — no tight loop possible.

---

### FINDING-3: Bounded channel — no unbounded growth path
- **Severity**: N/A
- **Confidence**: 100
- **File**: StreamSubscriptionChannel.cs:67-74
- **Blocking**: No
- **Result**: PASS — `Channel.CreateBounded<object>(new BoundedChannelOptions(capacity))` with `FullMode = BoundedChannelFullMode.DropOldest`. `capacity` flows from `StreamEngineOptions.ChannelCapacity` (default 128, validated `[Range(1, int.MaxValue)]`). No `Channel.CreateUnbounded` call anywhere in the diff.

---

### FINDING-4: Exception handling does not silently swallow security events
- **Severity**: N/A
- **Confidence**: 100
- **File**: StreamEngine.cs:139-188 (log delegates); StreamEngine.cs:379-381 (error frame)
- **Blocking**: No
- **Result**: PASS — Error frames from the venue are logged at `Warning` level via `s_logErrorFrame` (EventId 3). Reconnect failures are logged at `Warning` via `s_logReconnectFailed` (EventId 9). Watchdog triggers are logged at `Warning` via `s_logWatchdogTriggered` (EventId 14). All broad `catch (Exception)` blocks have CA1031 suppression with documented intent and log the exception. No silent security-relevant swallowing.

---

### FINDING-5: No competitor or product brand names in committed code
- **Severity**: N/A
- **Confidence**: 100
- **File**: all new files
- **Blocking**: No
- **Result**: PASS — No exchange brand names (Binance, Bybit, OKX, Bitget, Kraken, Coinbase, FTX, etc.) appear in the new files. The only exchange-prefixed names are `CryptoExchanges.Net.*` assembly namespace prefixes, which are the project's own namespace.

---

### FINDING-6: LoggerMessage delegates — CA1848 compliance
- **Severity**: N/A
- **Confidence**: 100
- **File**: StreamEngine.cs:137-188; StreamSubscriptionChannel.cs:26-28
- **Blocking**: No
- **Result**: PASS — All 17 log invocations in `StreamEngine` use static `s_log*` delegates defined via `LoggerMessage.Define`. `StreamSubscriptionChannel` defines and uses `s_logCallbackException`. No raw `_logger.Log*(...)` string interpolation anywhere in the diff.

---

### FINDING-7: Resource disposal — IDisposable/IAsyncDisposable completeness
- **Severity**: LOW
- **Confidence**: 60
- **File**: StreamEngine.cs:745, 716-724 (CancelIdleClose / DisposeAsync)
- **Blocking**: No
- **Result**: CONCERN — `DisposeAsync` calls `CancelIdleClose()` which cancels and disposes `_idleCloseCts`, but `_idleCloseTask` is not awaited in `DisposeAsync`. The task will observe the cancellation and exit, but there is no join point ensuring it has completed before `_gate` is disposed at line 794. If the task wakes up and tries to acquire `_gate.WaitAsync` (line 700) after `_gate.Dispose()` is called, it would throw `ObjectDisposedException`. The `OperationCanceledException` path at line 698 is the intended exit, so in practice the token cancellation should win — but this is a race. All other disposables (`_gate`, `_disposeCts`, `_pumpCts`, `_heartbeatCts`, `_cts` in channel) are correctly disposed.

  Suggested fix: after `CancelIdleClose()` in `DisposeAsync`, capture `_idleCloseTask` and await it with a swallow block, the same way `_heartbeatTask` and `_pumpTask` are handled.

---

### FINDING-8: FakeWebSocketConnection SemaphoreSlim disposal pattern (test code only)
- **Severity**: LOW
- **Confidence**: 70
- **File**: tests/.../FakeWebSocketConnection.cs (diff lines 1335-1350)
- **Blocking**: No
- **Result**: CONCERN (test code, not production) — `DisposeAsync()` no longer disposes `_available` (correctly documented: engine may reconnect). The `IDisposable.Dispose()` path added in this diff handles final cleanup. Tests that use `FakeWebSocketConnection` via `await using` would call `DisposeAsync`, not `Dispose`, leaving the semaphore undisposed unless the test explicitly calls `Dispose()`. This is test infrastructure only and does not affect production security; however a `using` wrapper or GC finalizer would prevent the SDK analysers from flagging `SemaphoreSlim` leaks in the test project.

---

## Summary

TASK-044 introduces a pure WebSocket byte-engine with no credential handling, signing logic, or HTTP pipeline interaction. All seven security-focus areas reviewed clean: no secrets committed, no busy-spin reconnect, bounded-only channels, security events logged at Warning or above, no brand-name leakage, full CA1848 compliance, and all production IDisposable resources disposed. Two low-confidence concerns are noted — the `_idleCloseTask` non-await in `DisposeAsync` (theoretical race with `_gate.Dispose`) and the `FakeWebSocketConnection` SemaphoreSlim disposal in test code — neither is blocking. The implementation is approved.
