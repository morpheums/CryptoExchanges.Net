APPROVED

# Code Review — TASK-071 (FEAT-008): Outbound Control-Frame Throttling + Serialisation

**Reviewer**: Code Reviewer
**Date**: 2026-06-23
**Build**: 0 warnings / 0 errors (`TreatWarningsAsErrors=true`)
**Tests**: 38 passed / 0 failed (StreamEngineTests, including 5 new TASK-071 tests)

---

## Findings

### Finding: _sendSemaphore correctly dedicated and independent of _gate
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:110
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. `_sendSemaphore` is a separate `SemaphoreSlim(1,1)` from `_gate`. A pacing delay inside `SendControlAsync` holds `_sendSemaphore` but does not block the `_gate` critical section. The heartbeat ping (which runs outside `_gate`) can no longer race a subscribe send on the same socket.
- **Rule reference**: N/A

### Finding: CancellationToken linked with _disposeCts in SendControlAsync
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:330
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. `CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token)` ensures that dispose mid-throttle cancels `Task.Delay` (and the semaphore wait) cleanly. The `using var` correctly disposes the linked CTS. Verified by `Engine_Throttle_DisposeDuringDelay_CompletesCleanly`.

### Finding: try/finally in SendControlAsync guarantees semaphore release
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:332-349
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. `_sendSemaphore.Release()` is in the `finally` block; no exception path can leave the semaphore unreleased.

### Finding: Monotonic Stopwatch timing (not DateTime.UtcNow)
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:336,344
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. `Stopwatch.GetElapsedTime(_lastSendTicks)` and `Stopwatch.GetTimestamp()` use the high-resolution monotonic clock. `ApplyConnectionPacing` sets `_lastSendTicks = Stopwatch.GetTimestamp() - Stopwatch.Frequency` (older than any interval) so the first frame on a freshly opened socket is never artificially delayed.

### Finding: Task.Delay skipped when interval == Zero
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:334
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. The guard `if (_minOutboundInterval > TimeSpan.Zero)` is the outermost condition; no delay path is entered for `TimeSpan.Zero`. Verified by `Engine_Throttle_ZeroInterval_PreservesUnthrottledBehavior` (5 subscribes well under 500ms).

### Finding: Socket re-checked open after pacing delay
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:341
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. `if (_socket is not null && _socket.IsOpen)` is evaluated after the pacing delay, not before. A socket that disconnects during the delay will be detected and the send is skipped (no throw from a closed socket).

### Finding: _sendSemaphore disposed in DisposeAsync (CA2213)
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:887
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. `_sendSemaphore.Dispose()` is called after `_gate.Dispose()` and before `_disposeCts.Dispose()` — a valid disposal order that CA2213 requires.

### Finding: All four send sites routed through SendControlAsync
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:239,273,598,721
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. Subscribe (239), unsubscribe (273), reconnect-replay (598), and client-ping (721) all route through `SendControlAsync`. The broad-catch + `s_log*` delegate wrappers are preserved at each call site. Direct `_socket.SendTextAsync` calls have been eliminated from production code paths.

### Finding: volatile removed from _livenessFlag — correct since all access is via Interlocked
- **Severity**: PASS
- **Confidence**: 95
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:136
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. Every read and write of `_livenessFlag` goes through `Interlocked.Exchange` (lines 428, 658, 724, 758), which issues full memory barriers — making `volatile` redundant. The removal is correct and avoids the CA1805-style redundancy.

### Finding: _lastSendTicks and _minOutboundInterval thread-safety
- **Severity**: PASS
- **Confidence**: 92
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:116-117,310-313,334-344
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. `_minOutboundInterval` and `_lastSendTicks` are written only by `ApplyConnectionPacing`, which is called under `_gate`. They are read only inside `SendControlAsync`, which holds `_sendSemaphore`. Because `ApplyConnectionPacing` is always called before the subsequent `SendControlAsync` calls within the same `_gate` critical section (subscribe path) or before replay starts (reconnect path), the memory ordering is safe without additional `volatile` or `Interlocked` — the same-thread sequential writes happen-before the reads.

### Finding: LR-001 guard present on SendControlAsync
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:328
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. `ArgumentException.ThrowIfNullOrWhiteSpace(text)` is the first statement in `SendControlAsync`, satisfying LR-001.
- **Rule reference**: LR-001

### Finding: ConfigureAwait(false) on every await in SendControlAsync and ApplyConnectionPacing
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:331,338,342
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. All three `await` expressions in `SendControlAsync` use `.ConfigureAwait(false)`. `ApplyConnectionPacing` is synchronous (no awaits).

### Finding: LEAN comments — no self-evident remarks or noise
- **Severity**: PASS
- **Confidence**: 96
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:107-109,308-309,681-683
- **Category**: Style
- **Verdict**: PASS
- **Issue**: None. The comment block at lines 107–109 explains the *why* (pacing delay must not block the subscribe/reconnect critical section; heartbeat race with subscribe send). The `ApplyConnectionPacing` comment (308–309) explains the non-obvious initialization of `_lastSendTicks`. The ping-text decode comment (681–683) explains a correctness invariant. No comment restates what the code says.
- **Rule reference**: LR-010

### Finding: SubscribeAsync <remarks> added — evaluate for LEAN compliance
- **Severity**: LOW
- **Confidence**: 72
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:188-192
- **Category**: Style
- **Verdict**: CONCERN (non-blocking, confidence 72)
- **Issue**: A `<remarks>` block was added to `SubscribeAsync` explaining that the task may be delayed under throttling and that the subscribe gate is held across the delay. This is caller-observable behavior that is not obvious from the signature, so documenting it has value. The concern is whether it crosses into "essay" territory per the LEAN rule. The text is 2 sentences and describes a non-obvious side effect, which falls within the acceptable range. Borderline but acceptable.
- **Fix**: None required. If the maintainer considers this too verbose, it can be trimmed to a one-line note in the `<returns>` doc.
- **Rule reference**: LR-010

### Finding: StreamConnectionInfo <remarks> constraint K1 updated correctly
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs:8-13
- **Category**: Style
- **Verdict**: PASS
- **Issue**: None. The `<remarks>` constraint K1 was updated to mention `TimeSpan` pacing without introducing essay-level content. The `<param name="MinOutboundInterval">` doc is detailed but describes a non-obvious venue-vs-consumer distinction that callers of `IStreamProtocol.ResolveConnectionAsync` need to know.

### Finding: 5 new tests cover all required behaviors (LR-005)
- **Severity**: PASS
- **Confidence**: 99
- **File**: tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs:944-1165
- **Category**: Testing
- **Verdict**: PASS
- **Issue**: None.
  - (a) `Engine_Throttle_InitialMultiSubscribe_FramesSpacedByMinOutboundInterval` — spacing >= interval with 30ms tolerance
  - (b) `Engine_Throttle_SendsAreSerialised_MaxConcurrencyOne` — `MaxObservedConcurrency == 1` via lock-free CAS in `RecordingWebSocketConnection`
  - (c) `Engine_Throttle_ZeroInterval_PreservesUnthrottledBehavior` — 5 sends under 500ms wall time
  - (d) `Engine_Throttle_DisposeDuringDelay_CompletesCleanly` — no `UnobservedTaskException` after GC.Collect
  - (e) `Engine_Throttle_ReconnectReplay_IsPaced` — SimulateDisconnect, wait for 3 replay stamps, assert gaps
- **Rule reference**: LR-005

### Finding: No dead _ = x vars or noise in tests
- **Severity**: PASS
- **Confidence**: 99
- **File**: tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs:944-1165
- **Category**: Style
- **Verdict**: PASS
- **Issue**: None. No `_ = x` discards or self-evident comments were introduced. Test names are descriptive and the assertion messages are human-readable failure strings.
- **Rule reference**: LR-010

### Finding: FakeStreamProtocol updated to surface MinOutboundInterval
- **Severity**: PASS
- **Confidence**: 99
- **File**: tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeStreamProtocol.cs:41-45,65
- **Category**: Testing
- **Verdict**: PASS
- **Issue**: None. `MinOutboundInterval` property (defaulting to `TimeSpan.Zero`) is forwarded in `ResolveConnectionAsync` to `StreamConnectionInfo`. Existing tests unaffected (zero = unthrottled).

### Finding: RecordingWebSocketConnection uses lock-free CAS for MaxObservedConcurrency
- **Severity**: PASS
- **Confidence**: 97
- **File**: tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs:1193-1201
- **Category**: Testing
- **Verdict**: PASS
- **Issue**: None. `UpdateMax` uses a CAS loop (`Interlocked.CompareExchange`) — correct for concurrent increment tracking without a lock. `MaxObservedConcurrency` is read via `Volatile.Read`.

### Finding: BinanceStreamProtocol and KucoinStreamProtocol MinOutboundInterval values
- **Severity**: PASS
- **Confidence**: 95
- **File**: src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs:37-40, src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs:55
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. Binance: 200ms (5 msg/s with margin against the 5 msg/s PolicyViolation limit). KuCoin: 100ms (10 msg/s, comment notes this is conservative). Both values are justified in inline comments.

### Finding: pingText decoded before loop in ClientPingLoopAsync
- **Severity**: PASS
- **Confidence**: 99
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:681-684
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: None. `pingText` is decoded once from `policy.ClientPingPayload.Span` before the loop. The `pingText!` null-forgiving operator at line 721 is safe because it is only reached inside `case PingFormat.Text: case PingFormat.Json:` which is the same condition that produces a non-null `pingText` at line 682–684.

---

## Summary

- PASS: `_sendSemaphore` dedicated, independent of `_gate` — correct serialisation of all outbound control frames
- PASS: `CancellationToken` linked with `_disposeCts` in `SendControlAsync` — dispose mid-throttle cancels cleanly, verified by test (d)
- PASS: `try/finally` guarantees `_sendSemaphore.Release()` on all paths
- PASS: Monotonic `Stopwatch` timing; `ApplyConnectionPacing` resets `_lastSendTicks` on every connect/reconnect
- PASS: `Task.Delay` skipped when `MinOutboundInterval == Zero` — unthrottled path byte-identical to before
- PASS: Socket re-checked open after pacing delay before `SendTextAsync`
- PASS: `_sendSemaphore.Dispose()` in `DisposeAsync` (CA2213)
- PASS: All four send sites (subscribe, unsubscribe, reconnect-replay, client-ping) routed through `SendControlAsync`
- PASS: `volatile` removed from `_livenessFlag` — all accesses via `Interlocked` provide full barriers
- PASS: Thread-safety of `_lastSendTicks`/`_minOutboundInterval` is correct (writes under `_gate`, reads under `_sendSemaphore`)
- PASS: LR-001 guard (`ArgumentException.ThrowIfNullOrWhiteSpace`) on `SendControlAsync` text parameter
- PASS: `ConfigureAwait(false)` on every `await` in changed methods
- PASS: LEAN comments — all explain non-obvious *why*, none restate code
- PASS: 5 new unit tests covering all required behaviors (LR-005)
- PASS: No dead `_ = x` vars or noise in tests (LR-010)
- PASS: `FakeStreamProtocol.MinOutboundInterval` forwarded correctly, existing tests unaffected
- PASS: `RecordingWebSocketConnection` uses correct lock-free CAS for concurrency tracking
- PASS: Binance 200ms and KuCoin 100ms intervals justified in comments
- PASS: Build: 0 warnings, 0 errors; Tests: 38/38 passed
- CONCERN: `SubscribeAsync` `<remarks>` block (2 sentences) borderline under LEAN rule — non-blocking, confidence 72

---

## Final Verdict

APPROVED

All correctness checks pass. The serialisation + pacing design is sound: `_sendSemaphore` is independent of `_gate` (pacing delay never blocks the subscribe critical section), the linked CTS ensures clean cancellation on dispose, `Stopwatch` provides monotonic timing, `ApplyConnectionPacing` correctly resets the clock on every connect/reconnect, and all four send paths are funnelled through `SendControlAsync`. Five targeted unit tests verify the complete contract. Build is clean at 0 warnings / 0 errors.
