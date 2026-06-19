# Code Review — TASK-044

## Verdict: CHANGES_REQUESTED

---

## Findings

### FINDING-1: PingFormat.ControlFrame sends Pong instead of Ping (MEDIUM, confidence 88, blocking)
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:631-632`
- **Blocking**: YES

`ClientPingLoopAsync` handles `PingFormat.ControlFrame` by calling `_socket.SendPongAsync(policy.ClientPingPayload, ct)`. Per `IWebSocketConnection.SendPongAsync`'s XML doc: "Used by the engine to respond to server-initiated Ping frames (RFC 6455 §5.5.3)." A client-initiated control-frame heartbeat should send a WebSocket **Ping** (opcode 0x09, RFC 6455 §5.5.2), not a Pong (opcode 0x0A). An unsolicited Pong is technically valid per RFC 6455 as a unidirectional keep-alive, but it is not what `ControlFrame` implies in the docs, and it is not what a venue expecting an RFC 6455 Ping will respond to.

`IWebSocketConnection` has no `SendPingAsync` method, so there is no correct path to follow today. This gap must be resolved:
- Option A: Add `Task SendPingAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)` to `IWebSocketConnection` and its fake; call it in the `ControlFrame` branch.
- Option B: Document explicitly in code + XML doc that `PingFormat.ControlFrame` intentionally sends an unsolicited Pong (e.g., because .NET `ClientWebSocket` does not expose `SendPingAsync`), and rename `SendPongAsync` to `SendControlFrameAsync` across the interface.

Same finding raised independently by architect-reviewer and api-reviewer. Three independent reviewers agree.

---

### FINDING-2: _idleCloseTask not awaited in DisposeAsync — narrow race with _gate.Dispose (LOW, confidence 65, non-blocking)
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:745, 700, 794`
- **Blocking**: NO

`DisposeAsync` calls `CancelIdleClose()` (cancels `_idleCloseCts`, disposes it) but does not await `_idleCloseTask`. The idle-close task body at line 696-697 awaits `Task.Delay(_options.IdleCloseDelay, token)` — if the delay completes just before the CTS is cancelled, the task proceeds to `_gate.WaitAsync(CancellationToken.None)` at line 700 (note: `CancellationToken.None`, not the CTS token). If `_gate.Dispose()` at line 794 executes concurrently, `WaitAsync` throws `ObjectDisposedException` on the fire-and-forget task. In practice the window is extremely narrow (the CTS cancel fires immediately on `CancelAsync`, so the task should see cancellation before `Task.Delay` unblocks), but there is no synchronization guarantee.

Fix: capture `_idleCloseTask` before calling `CancelIdleClose()`, then await it with a swallow block after the cancel — same pattern as `_heartbeatTask` and `_pumpTask`.

---

### FINDING-3: MaxSubscriptionsPerSocket is dead — implied reserved member (LOW, confidence 90, non-blocking)
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngineOptions.cs:53-57`
- **Blocking**: NO

`MaxSubscriptionsPerSocket = 1024` is defined, documented as "the engine opens a second socket (sharding)", but `StreamEngine` never reads `_options.MaxSubscriptionsPerSocket`. This is a configuration knob that does nothing. Per plan constraint: "no reserved-for-v1.1 members on any v1 interface." Remove until sharding is implemented. Also noted by architect-reviewer and api-reviewer.

---

### FINDING-4: FakeStreamProtocol co-located with tests — one-type-per-file convention violation (LOW, confidence 85, non-blocking)
- **File**: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs:760-796`
- **Blocking**: NO

`FakeStreamProtocol` is a free-standing `internal sealed class` at the bottom of `StreamEngineTests.cs`. `FakeWebSocketConnection` was correctly extracted to its own file. The convention is one type per file. Extract to `FakeStreamProtocol.cs`.

---

### Async correctness — PASS items (no blocking issues found)

- **Pump fire-and-forget** (`_ = Task.Run(() => ReconnectAsync(), _disposeCts.Token)`): Safe — pump task returns immediately, reconnect is bounded by `_disposeCts`. `_reconnecting` CAS prevents concurrent reconnect loops from watchdog + pump error firing simultaneously. CORRECT.

- **Gate ordering in ReconnectCoreAsync**: Gate acquired → Reconnecting transition → gate released → pump cancel → socket dispose. Subscribe racing during the gap acquires the gate, finds `_socket` still non-null but potentially closed; the `_socket.IsOpen` check at line 190 catches it, sends on null/closed socket throws, caught by the subscribe gate's broad-catch. Benign.

- **CancellationToken flow**: `SubscribeAsync` uses caller CT (short-lived, subscribe only). Pump runs on `_pumpCts.Token` (linked to `_disposeCts`). Reconnect uses `_disposeCts.Token`. `DisposeAsync` signals `_disposeCts` → pump unblocks → reconnect exits loop. CORRECT.

- **Heartbeat task await**: `DisposeAsync` calls `StopHeartbeat()` then awaits `_heartbeatTask` with swallow at lines 759-763. CORRECT.

- **Double-dispose guard**: `_isDisposed = true` then cancel `_disposeCts`. `volatile bool` is sufficient for this pattern (single setter, reader only checks). CORRECT.

- **WatchdogAsync concurrent**: Fires `_ = Task.Run(() => ReconnectAsync(), ...)` and returns. `_reconnecting` CAS in `ReconnectAsync` ensures only one reconnect loop runs. CORRECT.

---

### _pendingCount accounting — CORRECT

Single writer (pump), single reader (consumer). Write path: `Interlocked.Increment(_pendingCount)` → if `pending > _capacity` (channel full): `Interlocked.Decrement(_pendingCount)` (account for DropOldest eviction) + `Interlocked.Increment(_droppedCount)` → `TryWrite` (always succeeds, evicts oldest). Read path: `Interlocked.Decrement(_pendingCount)` after reading from channel. The accounting is correct: `_pendingCount` reflects in-flight items from the writer's view, and the decrement-before-TryWrite correctly accounts for the eviction that DropOldest will perform. Cannot go negative: consumer only decrements after a successful `ReadAllAsync` iteration; writer only decrements on overflow detection.

---

## Test Quality Assessment

Tests are genuinely substantive, not smoke:

- **Reconnect + K2 replay**: Waits for `lifecycle.Contains("reconnected")` (not just ConnectCount), then asserts `sub.State == Live` and `>= 2` SUBSCRIBE messages. The K2 unsubscribe test additionally verifies no TICKER replay after unsubscribe. SUBSTANTIVE.

- **Backpressure / OnLagged**: Consumer stalled via `SemaphoreSlim`, 7 frames flooded on a capacity-2 channel, then verifies `lagged.DroppedCount > 0`. Uses precise stall/release mechanics. SUBSTANTIVE.

- **Heartbeat ClientPing**: Waits for `>= 2` ping messages at 100ms interval via polling. Genuinely asserts the timer fires. SUBSTANTIVE.

- **Callback exception isolation**: First frame throws, second frame delivered — verifies pump survived the exception. SUBSTANTIVE.

- **Watchdog reconnect**: Short 150ms timeout, no frames sent, verifies `ConnectCount >= 2`. SUBSTANTIVE.

- **All tests use FakeWebSocketConnection** — no network. CONFIRMED.

---

## LR-001 Compliance

Full compliance:
- `StreamEngine.UnsubscribeAsync(string routingKey)`: `ArgumentException.ThrowIfNullOrWhiteSpace(routingKey)` at line 241 — PASS
- `StreamSubscriptionHandle<T>` ctor `(string routingKey, ...)`: `ArgumentException.ThrowIfNullOrWhiteSpace(routingKey)` at line 32 — PASS
- `StreamEngine.BuildRoutingKey(StreamRequest request)`: `ArgumentNullException.ThrowIfNull(request)` — PASS (request is not a string, correct guard type)
- All other methods: no non-optional string params — PASS

---

## Summary

TASK-044 delivers a solid async byte-engine with correct receive-pump lifecycle, safe fire-and-forget reconnect guards, proper CTS lifetime management, and substantive test coverage across all specified behaviors. One blocking issue shared by three independent reviewers: `PingFormat.ControlFrame` calls `SendPongAsync` where a Ping frame is semantically required — `IWebSocketConnection` lacks `SendPingAsync` and the design intent must be resolved either by adding the method or explicitly documenting the workaround. Two non-blocking low-severity concerns: the `_idleCloseTask` is not awaited in `DisposeAsync` (narrow race with `_gate.Dispose`), and `MaxSubscriptionsPerSocket` is a dead configuration knob violating the no-reserved-member rule.
