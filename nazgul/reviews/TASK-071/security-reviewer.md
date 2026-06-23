APPROVED

## Review Summary

TASK-071 adds `StreamConnectionInfo.MinOutboundInterval` and `StreamEngine.SendControlAsync` to pace and serialize outbound WebSocket control frames. This is a pure WebSocket transport change — public market-data streaming only. No credentials, signing, or authentication paths are touched.

---

## Findings

### Finding: No credential/signing path touched
- **Severity**: N/A
- **Confidence**: 100
- **File**: all changed files
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. The diff contains zero references to ApiKey, SecretKey, HMAC, signature, or any authentication header. The entire change is in the WebSocket streaming layer.
- **Fix**: N/A
- **Pattern reference**: BinanceSignatureService.cs (untouched)

### Finding: No secrets in logs or exception messages
- **Severity**: N/A
- **Confidence**: 100
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. The only text logged is the fixed LoggerMessage template strings (routing keys, backoff counts, timeouts). SendControlAsync's `text` parameter is a protocol-built JSON frame (e.g. `{"method":"SUBSCRIBE",...}`) — no credential data is present or structurally possible on this code path.
- **Fix**: N/A
- **Pattern reference**: StreamEngine.cs:43-92 (s_log* delegates)

### Finding: LinkedTokenSource disposal on every SendControlAsync call
- **Severity**: LOW
- **Confidence**: 95
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:330
- **Category**: Security / Resource exhaustion
- **Verdict**: PASS
- **Issue**: `using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token)` is inside `SendControlAsync` and scoped with `using`, so it is disposed on every exit path including cancellation and exception. No CTS leak per call.
- **Fix**: N/A
- **Pattern reference**: StreamEngine.cs:686 (watchdogCts using-pattern in ClientPingLoopAsync)

### Finding: SemaphoreSlim waiter count is bounded by the number of active subscriptions
- **Severity**: LOW
- **Confidence**: 90
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:331
- **Category**: Security / Resource exhaustion
- **Verdict**: PASS
- **Issue**: `_sendSemaphore.WaitAsync` is called from subscribe, unsubscribe, reconnect-replay, and client-ping paths. The number of concurrent waiters is bounded by the number of active subscriptions (finite, controlled by the caller) plus one heartbeat task. There is no mechanism for unbounded external input to queue sends — `SendControlAsync` is a private method and all call sites are engine-internal. No unbounded resource growth.
- **Fix**: N/A
- **Pattern reference**: StreamEngine.cs:105 (_gate, identical bounded pattern)

### Finding: Dispose-during-delay cancellation path
- **Severity**: LOW
- **Confidence**: 95
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:330-348
- **Category**: Security / Cancellation correctness
- **Verdict**: PASS
- **Issue**: When `DisposeAsync` is called while a `Task.Delay` is in progress inside `SendControlAsync`, the linked token (which includes `_disposeCts.Token`) is cancelled. The `Task.Delay` throws `OperationCanceledException`, the `_sendSemaphore.Release()` in the `finally` block executes, and the linked CTS is disposed by the `using` statement. The test `Engine_Throttle_DisposeDuringDelay_CompletesCleanly` explicitly covers this and checks for unobserved exceptions. No Task outlives the engine's lifetime.
- **Fix**: N/A
- **Pattern reference**: StreamEngine.cs:886-887 (_gate.Dispose() and _sendSemaphore.Dispose() in DisposeAsync)

### Finding: _minOutboundInterval / _lastSendTicks not volatile or Interlocked
- **Severity**: LOW
- **Confidence**: 60
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:116-117, 310-313, 334-344
- **Category**: Security / Concurrency
- **Verdict**: CONCERN (non-blocking, confidence < 80)
- **Issue**: `_minOutboundInterval` (TimeSpan, i.e. a `long` on 64-bit) and `_lastSendTicks` (long) are plain fields with no `volatile` or `Interlocked` qualifier. `ApplyConnectionPacing` writes both fields under `_gate` (since it is called from `OpenSocketAsync` which runs under `_gate`, and from the reconnect block which also holds `_gate`). However, `SendControlAsync` reads these fields under `_sendSemaphore` (not `_gate`). On .NET with the x86-64 memory model, 64-bit reads/writes on aligned fields are effectively atomic, and the thread-pool scheduler provides sufficient memory barriers in practice around `SemaphoreSlim.WaitAsync`. The operational risk is very low (a stale read of the interval or tick count would at worst cause one frame to be sent without the pacing delay — not a security defect, and only possible in a narrow reconnect window). No security impact; correctness risk only and very low probability on .NET.
- **Fix**: For belt-and-suspenders correctness, `_lastSendTicks` could be read/written with `Interlocked.Read`/`Interlocked.Exchange` and `_minOutboundInterval` could be stored as a `long` ticks value behind Interlocked or declared `volatile`. However, given .NET's strong memory model and that the field writes happen before `_sendSemaphore` is available to callers (socket connect under `_gate` completes before any subscribe call can proceed), the risk of a visible stale read is negligible.
- **Pattern reference**: StreamEngine.cs:482 (Interlocked.CompareExchange on _reconnecting for similar cross-task coordination)

### Finding: _livenessFlag volatile removal
- **Severity**: LOW
- **Confidence**: 50
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:136 (diff line -91 +91)
- **Category**: Security / Concurrency
- **Verdict**: CONCERN (non-blocking, confidence < 80)
- **Issue**: The diff removes `volatile` from `_livenessFlag`. The field is already accessed exclusively via `Interlocked.Exchange` / `Interlocked.CompareExchange`, which provides full memory barrier semantics. The `volatile` modifier was therefore redundant and its removal is correct. No security or correctness regression.
- **Fix**: None needed. Interlocked operations are already sufficient.
- **Pattern reference**: StreamEngine.cs:482 (_reconnecting uses Interlocked without volatile)

### Finding: Input injection surface in SendControlAsync
- **Severity**: N/A
- **Confidence**: 100
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:326-350
- **Category**: Security / Input handling
- **Verdict**: PASS
- **Issue**: `SendControlAsync` is a private method. Its `text` parameter is always provided by `_protocol.BuildSubscribe()`, `_protocol.BuildUnsubscribe()`, or the hard-coded ping payload decoded from `HeartbeatPolicy.ClientPingPayload` — all of which are engine-internal or protocol-internal. No user-supplied string can reach this method directly. `ArgumentException.ThrowIfNullOrWhiteSpace(text)` is present as an additional guard. No injection surface introduced.
- **Fix**: N/A
- **Pattern reference**: BinanceStreamProtocol.cs:59-64 (BuildSubscribe: all interpolated values are enum-derived or integer IDs)

### Finding: No new JSON deserialization or external input parsing
- **Severity**: N/A
- **Confidence**: 100
- **File**: all changed files
- **Category**: Security / JSON safety
- **Verdict**: PASS
- **Issue**: No new `JsonDocument.Parse`, `ReadFromJsonAsync`, or deserialization path is introduced. The diff is purely outbound send infrastructure.
- **Fix**: N/A

### Finding: RecordingWebSocketConnection test fake — SemaphoreSlim dispose race in ConnectAsync
- **Severity**: LOW
- **Confidence**: 45
- **File**: tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs:521-530
- **Category**: Security / Resource safety (test-only)
- **Verdict**: CONCERN (non-blocking, confidence < 80, test-only code)
- **Issue**: `ConnectAsync` in the recording fake creates a new `SemaphoreSlim` and disposes the old one under `_semLock`, but `ReceiveAsync` holds a reference to the old semaphore captured before the lock and awaits on it after the lock is released. If `Dispose` is called on the old semaphore while `ReceiveAsync` is awaiting it, a `ObjectDisposedException` could surface in the test. This is test-only infrastructure, not production code, and the test workload does not reconnect the recording fake mid-ReceiveAsync. No production security impact.
- **Fix**: Test-only concern; not blocking. If reconnect tests are added against this fake in future, the semaphore swap logic should be hardened. Out of scope for this review.
- **Pattern reference**: N/A (test-only fake)

---

## Summary

- PASS: Credential/signing path — zero overlap with signing or credential handling; ApiKey/SecretKey never referenced in the diff
- PASS: Secret leakage in logs — no credential data reachable by the logging path; SendControlAsync text is protocol-built JSON only
- PASS: LinkedTokenSource disposal — `using var` ensures per-call CTS is always disposed; no CTS leak
- PASS: SemaphoreSlim waiter count — bounded by subscriber count; private method; no external input can queue unbounded sends
- PASS: Dispose-during-delay — linked token cancels Task.Delay; finally block releases semaphore; test covers this case explicitly
- PASS: Input injection — SendControlAsync is private; all callers are engine-internal; ThrowIfNullOrWhiteSpace guard present
- PASS: No new JSON parsing or deserialization
- CONCERN: _minOutboundInterval / _lastSendTicks visibility — fields written under _gate, read under _sendSemaphore; practically safe on .NET x64 but not formally volatile/Interlocked (confidence: 60/100, non-blocking)
- CONCERN: volatile removal from _livenessFlag — correct change (Interlocked is sufficient), no regression (confidence: 50/100, non-blocking)
- CONCERN: RecordingWebSocketConnection semaphore swap in test fake — potential race if reconnect-under-receive is tested in future; test-only, not production (confidence: 45/100, non-blocking)

## Final Verdict

APPROVED — All checks pass. No blocking findings. Two low-confidence, low-severity concerns noted (pacing-field memory visibility and test-fake semaphore swap) that are non-blocking and carry no security impact.
