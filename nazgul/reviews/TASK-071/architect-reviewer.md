APPROVED

# Architect Review — TASK-071 (FEAT-008 Step 1: Outbound control-frame throttling + serialisation)

## Final Verdict: APPROVED

All architectural invariants hold. The implementation faithfully follows the FEAT-008 advisory design. No blocking findings.

---

## Checklist Results

### Layering (Core → Http → Exchange → DI)
PASS. `StreamEngine` and `StreamConnectionInfo` are in `CryptoExchanges.Net.Http.Streaming` (correct layer). Per-venue values (Binance 200 ms, KuCoin 100 ms) live in each exchange's `IStreamProtocol.ResolveConnectionAsync` implementation. No leakage of exchange knowledge into Http; no Http knowledge into Core.

### StreamConnectionInfo purity (K1 constraint)
PASS. The record carries only `Uri Endpoint`, `HeartbeatPolicy Heartbeat`, and `TimeSpan MinOutboundInterval`. No `Core.Models` references, no DeltaMapper references. The updated XML doc comment explicitly preserves the K1 constraint statement (`src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs:8-11`).

### Advisory Step-1 conformance
All five sub-items verified:

1. **_sendSemaphore separate from _gate**: PASS. `_gate = new(1,1)` and `_sendSemaphore = new(1,1)` are declared as independent fields (`StreamEngine.cs:105,110`). SendControlAsync acquires only `_sendSemaphore`; subscribe/reconnect critical sections acquire only `_gate`. No nesting.

2. **Interval captured on BOTH open and reconnect**: PASS. `ApplyConnectionPacing(info)` is called in `OpenSocketAsync` (`StreamEngine.cs:304`) and in `ReconnectCoreAsync` after successful connect (`StreamEngine.cs:587`). The helper resets `_lastSendTicks` to be artificially old so the first frame on a fresh socket is never delayed.

3. **Linked CTS to _disposeCts**: PASS. `SendControlAsync` creates `CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token)` on every call (`StreamEngine.cs:330`) and passes `linked.Token` to both `Task.Delay` and `SendTextAsync`. Disposal cancels the delay cleanly.

4. **All four send sites routed**: PASS.
   - Subscribe: `StreamEngine.cs:239` — `await SendControlAsync(subscribeText, ct)`
   - Unsubscribe: `StreamEngine.cs:273` — `await SendControlAsync(unsubText, _disposeCts.Token)`
   - Reconnect-replay: `StreamEngine.cs:598` — `await SendControlAsync(subscribeText, _disposeCts.Token)`
   - Client-ping: `StreamEngine.cs:721` — `await SendControlAsync(pingText!, ct)`

5. **_sendSemaphore disposed**: PASS. `_sendSemaphore.Dispose()` is called in `DisposeAsync` between `_gate.Dispose()` and `_disposeCts.Dispose()` (`StreamEngine.cs:887`).

### Zero public API change
PASS. `StreamConnectionInfo` remains `internal sealed record`. `StreamEngine` remains `internal sealed class`. `BinanceStreamProtocol` and `KucoinStreamProtocol` remain `internal sealed class`. `IStreamClient` in Core is untouched. Build confirms zero warnings and zero errors with `TreatWarningsAsErrors=true`.

### Lock-ordering / deadlock analysis
PASS. The only possible "nested" scenario is: `SubscribeAsync` holds `_gate` and then enters `SendControlAsync` which acquires `_sendSemaphore`. The heartbeat ping acquires `_sendSemaphore` but never acquires `_gate`. Reconnect acquires `_gate` first (before socket swap), then calls `SendControlAsync` inside the same `_gate` scope. There is no path where `_sendSemaphore` is held before `_gate` is attempted. The advisory's documented intentional trade-off — `SubscribeAsync` holds `_gate` across the throttle delay — is correctly described in the XML doc on `SubscribeAsync` (`StreamEngine.cs:188-191`).

### _livenessFlag volatile removal
PASS. The diff removes `volatile` from `_livenessFlag` (line 91 of the diff). This is correct: all accesses are already via `Interlocked.Exchange` and `Interlocked.Exchange` with `ref`, which carry full memory barriers. The `volatile` keyword was redundant and the removal is a correctness improvement that eliminates the confusing dual-ordering-guarantee pattern.

### pingText decode optimization
PASS. The diff moves `Encoding.UTF8.GetString(policy.ClientPingPayload.Span)` out of the per-ping-iteration loop into a once-per-connection local at `ClientPingLoopAsync` entry (`StreamEngine.cs:682-684`). This is an allocation reduction with no behavioral change. The `null` guard on the `case PingFormat.Text/Json` path is correct (`pingText!` — the `!` is justified because `pingText` is only non-null when the format is Text or Json).

### One-type-per-file
PASS. `RecordingWebSocketConnection` is defined inside `StreamEngineTests.cs` as a private sealed nested class within the test class, which is consistent with this project's test pattern for test-local doubles. `FakeStreamProtocol.cs` is its own file. No production type violations.

### Test coverage of new behavior
PASS. Five new tests added:
- `Engine_Throttle_InitialMultiSubscribe_FramesSpacedByMinOutboundInterval` — verifies send timestamps are spaced by at least `MinOutboundInterval` (minus tolerance).
- `Engine_Throttle_SendsAreSerialised_MaxConcurrencyOne` — verifies `MaxObservedConcurrency == 1` via `RecordingWebSocketConnection`.
- `Engine_Throttle_ZeroInterval_PreservesUnthrottledBehavior` — verifies zero-interval is a no-op (regression guard).
- `Engine_Throttle_ReconnectReplay_IsPaced` — verifies reconnect-replay frames are also paced.
- `Engine_Throttle_DisposeDuringDelay_CompletesCleanly` — verifies dispose mid-throttle-delay produces no unobserved exceptions.

`FakeStreamProtocol` updated to expose `MinOutboundInterval` and pass it into `StreamConnectionInfo` (`FakeStreamProtocol.cs:45,65`), so all new tests properly exercise the production path.

---

## Findings

### Finding 1: _lastSendTicks / _minOutboundInterval written outside _sendSemaphore
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:311-313` (`ApplyConnectionPacing`)
- **Category**: Architecture — thread safety
- **Verdict**: CONCERN (non-blocking, confidence 55)
- **Issue**: `_minOutboundInterval` and `_lastSendTicks` are written by `ApplyConnectionPacing` (called from `OpenSocketAsync` and `ReconnectCoreAsync`) while `_gate` is held, but `SendControlAsync` reads them after acquiring `_sendSemaphore` (not `_gate`). The heartbeat `ClientPingLoopAsync` runs outside `_gate` and can call `SendControlAsync` concurrently with an `ApplyConnectionPacing` call on a reconnect path. In practice the reconnect path stops the heartbeat via `StopHeartbeat()` before calling `ApplyConnectionPacing`, so the window is closed by the time `StartHeartbeat` is called after `ApplyConnectionPacing`. The order in `ReconnectCoreAsync` is: stop heartbeat → dispose old socket → reconnect loop → `ApplyConnectionPacing` → `StartPump` (which calls `StartHeartbeat`). This means no heartbeat ping task is alive when `ApplyConnectionPacing` writes, so there is no actual data race. The LOW severity reflects that the safety is emergent from ordering rather than from a lock or `volatile`, making it non-obvious to future maintainers. A `volatile` on `_minOutboundInterval` or a comment at the write site noting the ordering guarantee would improve safety documentation.
- **Fix (non-blocking)**: Add a comment at `ApplyConnectionPacing` noting that `StopHeartbeat` is always called before this method is invoked on reconnect, and `StartHeartbeat` is called after. This documents the safety invariant so a future refactor does not break it.
- **Pattern reference**: `StreamEngine.cs:515-590` (reconnect path: StopHeartbeat at 516 before pacing write at 587 before StartPump at 590).

### Finding 2: Step-2 batching deferred (CONCERN — pre-existing advisory item, not a new defect)
- **Severity**: LOW
- **Confidence**: 70
- **File**: N/A (advisory scope)
- **Category**: Architecture — latency at scale
- **Verdict**: CONCERN (non-blocking, confidence 70)
- **Issue**: FEAT-008 advisory explicitly warns "Do not defer" Step-2 batching: "300-symbol consumer stalls ~60 s if batching is deferred." TASK-071 implements Step 1 only. With 200 ms Binance throttle and N symbols, reconnect-replay cost is N × 200 ms. For 30 symbols that is 6 seconds; for 300 symbols it is 60 seconds. Step 1 alone does not close this risk. This is a known, documented risk carried forward.
- **Fix (non-blocking)**: Ensure a follow-on TASK for Step-2 batching (`IStreamProtocol.BuildSubscribeBatch`) is created and prioritized before FEAT-008 is marked complete. The advisory treatment is in `nazgul/reviews/FEAT-008/architect-reviewer.md:44-53`.
- **Pattern reference**: `nazgul/reviews/FEAT-008/architect-reviewer.md:68-71` (risk register row: "300-symbol consumer stalls ~60 s if batching is deferred").

---

## Summary

- PASS: Strict layering — pacing primitive in Http, per-venue values in exchange protocols, no leakage in either direction.
- PASS: StreamConnectionInfo K1 constraint — only venue/transport policy data (Uri, HeartbeatPolicy, TimeSpan), no Core.Models or DeltaMapper.
- PASS: _sendSemaphore separate from _gate, no nested acquisition possible.
- PASS: ApplyConnectionPacing called on both OpenSocketAsync and ReconnectCoreAsync paths.
- PASS: Linked CTS combines caller ct + _disposeCts.Token in SendControlAsync.
- PASS: All four send sites (subscribe/unsubscribe/reconnect-replay/client-ping) route through SendControlAsync.
- PASS: _sendSemaphore.Dispose() called in DisposeAsync.
- PASS: Zero public API change — IStreamClient untouched, all touched types remain internal.
- PASS: Build clean (0 warnings, 0 errors, TreatWarningsAsErrors=true).
- PASS: One-type-per-file respected for production code.
- PASS: Five targeted tests cover pacing, serialisation, zero-interval, reconnect-replay, and dispose-mid-delay.
- CONCERN: _lastSendTicks/_minOutboundInterval written outside _sendSemaphore — safety is real but emergent from heartbeat-stop ordering; a documentation comment would make the invariant explicit (confidence: 55/100, non-blocking).
- CONCERN: Step-2 batching deferred — 300-symbol reconnect still takes up to 60 s under Step-1 alone; follow-on task needed before FEAT-008 closes (confidence: 70/100, non-blocking).
