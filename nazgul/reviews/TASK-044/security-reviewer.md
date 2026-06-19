# Security Review — TASK-044 (Cycle 2)

## Verdict: APPROVED

---

## Prior Concern Resolution

### FINDING-7 (was LOW, 60 confidence): _idleCloseTask race with _gate.Dispose

RESOLVED.

The fix is present and correct. `DisposeAsync` now does:

```csharp
var idleCloseTask = _idleCloseTask;   // capture BEFORE cancel
CancelIdleClose();                     // signals the task to exit
if (idleCloseTask is not null)
{
    try { await idleCloseTask.ConfigureAwait(false); }
    catch (Exception) { /* swallow */ }
}
await _disposeCts.CancelAsync().ConfigureAwait(false);
// ... then _gate.Dispose() at the very end
```

The capture-before-cancel pattern is the correct fix. The local `idleCloseTask` variable holds the task reference so that even if `CancelIdleClose()` nulls out `_idleCloseTask`, the await proceeds against the captured reference. The idle-close task itself tries to acquire `_gate` with `CancellationToken.None`, but since it observes the cancellation token it was started with (`token` from `_idleCloseCts`), it exits via `OperationCanceledException` before it can race with `_gate.Dispose()`. The await-with-swallow ensures `DisposeAsync` does not proceed to `_gate.Dispose()` until the task has fully exited. The race is closed.

### FINDING-8 (was LOW, 70 confidence, test code only): SemaphoreSlim disposal in FakeWebSocketConnection

ADDRESSED — and substantially improved beyond what was requested.

The previous concern was that `DisposeAsync` disposed `_available` directly, which would break reconnect-cycle test patterns where the engine re-uses the same fake instance. The remediation:

1. `DisposeAsync` no longer disposes `_available`. It only sets `State = Closed`. This intentionally leaves the semaphore alive for the next `ConnectAsync` call.
2. A separate `IDisposable.Dispose()` path (under `_semLock`) performs the final cleanup.
3. `ConnectAsync` now recreates `_available` under `_semLock`, disposing the old instance atomically. All helpers (`EnqueueFrame`, `SimulateDisconnect`, `ReceiveAsync`) capture `sem` under `_semLock` before using it, eliminating the data race with reconnect cycles.
4. The class now implements `IDisposable` in addition to `IAsyncDisposable`, making the ownership model explicit.

The pattern is correct. The `_semLock` guard ensures the semaphore swap in `ConnectAsync` is atomic with respect to all callers that access `_available`. No semaphore can be used after disposal.

---

## New Security Assessment (remediation delta)

### SendPingAsync surface

The new `SendPingAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)` method added to `IWebSocketConnection` and implemented in `FakeWebSocketConnection` presents no security concern.

Payload origin: `policy.ClientPingPayload` comes from `HeartbeatPolicy`, which is constructed at configuration time by the exchange-specific `IStreamProtocol` implementation. It is a static byte array set at startup — it is not derived from any external, network-supplied, or user-supplied input at call time. There is no unbounded payload risk at the call site.

The fake implementation stores received payloads in `ConcurrentQueue<ReadOnlyMemory<byte>> SentPings` for test assertion only. The queue is accessible only to test code within the unit test assembly. No payload bytes are logged, serialized, or transmitted to any external sink.

The engine's heartbeat loop dispatches to `SendPingAsync` only when `policy.PingFormat == PingFormat.ControlFrame`. The distinction from `SendPongAsync` is correct per RFC 6455 §5.5.2 (Ping = opcode 0x09, client-initiated) vs §5.5.3 (Pong = opcode 0x0A, response to server ping). The engine correctly sends Ping on `ControlFrame`, never conflating Ping and Pong semantics. The new test `Engine_HeartbeatClientPing_ControlFrame_SendsPingNotPong` explicitly asserts `SentPongs.Should().BeEmpty()` after a `ControlFrame` ping cycle, closing the semantic correctness loop.

PASS.

### [Range] validation on TimeSpan fields

`StreamEngineOptions` now has `[Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]` on `IdleCloseDelay`, `BackoffInitial`, and `BackoffMax`. The minimum bound of 1 ms prevents zero or negative values that would cause `BackoffSchedule` to throw `ArgumentOutOfRangeException` at construction time (which guards `initial <= TimeSpan.Zero`). The maximum of 24 hours prevents pathologically long values that could functionally disable reconnect.

`BackoffMultiplier` has `[Range(1.0, 10.0)]` — lower bound 1.0 prevents divisor-style shrinkage that would violate the `BackoffSchedule` constructor's `multiplier < 1.0` guard.

`ChannelCapacity` has `[Range(1, int.MaxValue)]` — prevents zero-capacity channels where every frame would be dropped.

`MaxReconnectAttempts` has `[Range(0, int.MaxValue)]` — zero is intentionally allowed (means unlimited, as documented).

These attributes work in concert with the `BackoffSchedule` constructor's own argument guards. Even without `ValidateOnStart`, the construction-time guards independently prevent degenerate behavior. With `ValidateOnStart`, misconfiguration is caught at startup rather than at first reconnect. No DoS surface exists from zero/negative values reaching the reconnect loop.

PASS.

### Credential material check

No credential material was introduced in the remediation delta. The diff contains no `ApiKey`, `SecretKey`, `api_key`, `secret_key`, `Authorization`, or any token/auth field references. `StreamEngine`, `StreamEngineOptions`, `FakeWebSocketConnection`, and all test code are entirely credential-free. The streaming layer operates exclusively on byte frames and routing keys.

PASS.

### Exchange brand name check

No exchange brand names (Binance, Bybit, OKX, Bitget, or any other venue name) appear in any of the committed files in the remediation delta. The implementation is correctly exchange-agnostic. The only grep hit in the diff for `ToString` is `request.Kind.ToString().ToUpperInvariant()` in `BuildRoutingKey`, which operates on an enum value, not credentials.

PASS.

### CA1848 compliance (log call pattern)

All log calls in `StreamEngine` and `StreamSubscriptionChannel` use pre-compiled `LoggerMessage.Define` delegates assigned to `private static readonly` fields (`s_log*` pattern). There is no string interpolation, `LogInformation(...)`, `LogWarning(...)`, or any other runtime-allocation logging path in either production file. The 18 delegates in `StreamEngine` and 1 in `StreamSubscriptionChannel` all follow the CA1848-compliant pattern. No new raw logger calls were introduced anywhere in the remediation delta.

PASS.

---

## Summary

- PASS: _idleCloseTask race (FINDING-7) — capture-before-cancel + await-with-swallow pattern correctly eliminates the race with `_gate.Dispose`.
- PASS: SemaphoreSlim disposal (FINDING-8) — substantially improved; `_semLock`-guarded swap in `ConnectAsync`, `IDisposable` split from `IAsyncDisposable`, no disposal-after-use possible.
- PASS: SendPingAsync surface — payload is config-time static, not external input; fake stores in test-only `ConcurrentQueue`; RFC 6455 Ping/Pong semantics are correct and test-verified.
- PASS: [Range] validation — prevents zero/negative TimeSpan values that could cause tight reconnect loops; consistent with `BackoffSchedule` constructor guards.
- PASS: Credential material — none introduced.
- PASS: Brand names — none introduced; implementation is exchange-agnostic.
- PASS: CA1848 — all 19 log delegates are pre-compiled; no string-interpolation log calls.

## Final Verdict: APPROVED

Confidence: 97/100.

All cycle-1 concerns are resolved. No new security issues were introduced by the remediation delta. The implementation is clean across all seven security dimensions checked.
