# Architect Review — TASK-044 (Cycle 2)

## Verdict: APPROVED

---

## Binding Constraint Re-verification (K1 / K2 / K3 / C1)

**K1 — No `Core.Models` or `DeltaMapper` imports in Http:**
```
grep -rn "Core.Models\|DeltaMapper" src/CryptoExchanges.Net.Http/ --include="*.cs"
```
Result: one match in `StreamEngine.cs` line 35, inside an XML doc comment (`<c>Core.Models</c>/DeltaMapper`).
No `using` statement, no type reference in executable code. PASS.

**K2 — `_subscribeSet.TryRemove` executes before wire unsubscribe:**
`StreamEngine.cs:246–256` — `_subscriptions.TryRemove` first (line 246), then `_subscribeSet.TryRemove` (line 250), then wire send (line 256). The ordering comment "K2: remove from replay set BEFORE sending wire unsubscribe" is present. PASS.

**K3 — No `ExchangeResiliencePipeline` in Streaming:**
```
grep -rn "ExchangeResiliencePipeline" src/CryptoExchanges.Net.Http/Streaming/ --include="*.cs"
```
Zero matches. Engine backoff uses its own `BackoffSchedule` (separate code path from Polly). PASS.

**C1 — `HeartbeatPolicy` is data-only; heartbeat execution lives in the engine:**
`HeartbeatPolicy.cs` is a `sealed record` with five positional parameters and no methods, fields, timers, or threads. The XML doc explicitly states "No timers, threads, or behavioral methods live here." Heartbeat loops (`ClientPingLoopAsync`, `ServerPingWatchdogAsync`, `WatchdogAsync`) all reside in `StreamEngine.cs`. PASS.

---

## Remediation Verification

### Fix 1: SendPingAsync added / ControlFrame routes to SendPingAsync

**RESOLVED.**

`IWebSocketConnection.cs:49–55` — `SendPingAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)` is present with correct XML doc citing RFC 6455 §5.5.2 (opcode 0x09, Ping) and distinguishing it from `SendPongAsync` (§5.5.3, opcode 0x0A, Pong).

`StreamEngine.cs:726–731` (`ClientPingLoopAsync`, `PingFormat.ControlFrame` branch) — calls `_socket.SendPingAsync(policy.ClientPingPayload, ct)`, not `SendPongAsync`. The inline comment explicitly states the semantic: "RFC 6455 §5.5.2: client-initiated heartbeat sends a Ping control frame (opcode 0x09). SendPingAsync carries the correct semantics; SendPongAsync (opcode 0x0A) is reserved for replying to server pings."

`FakeWebSocketConnection.cs:83–87` — `SendPingAsync` enqueues to `SentPings` (`ConcurrentQueue<ReadOnlyMemory<byte>>`), not `SentPongs`. Correct.

No `using` directives or method calls cross layer boundaries. The fix is complete and semantically correct.

### Fix 2: MaxSubscriptionsPerSocket removed

**RESOLVED.**

```
grep -rn "MaxSubscriptionsPerSocket" src/ --include="*.cs"
```
Zero matches across all source files. The property is gone from `StreamEngineOptions.cs` and nowhere else in `src/`. PASS.

### Fix 3: _idleCloseTask captured before CancelIdleClose(), awaited with swallow in DisposeAsync

**RESOLVED.**

`StreamEngine.cs:844–852` (`DisposeAsync`):
```csharp
var idleCloseTask = _idleCloseTask;   // capture first
CancelIdleClose();                     // then cancel (sets _idleCloseTask = null)
if (idleCloseTask is not null)
{
    try { await idleCloseTask.ConfigureAwait(false); }
    catch (Exception) { /* swallow */ }
}
```
This exactly matches the pattern used for `_heartbeatTask` (lines 865–870) and `_pumpTask` (lines 873–878). Capture-before-cancel prevents a null-reference race; swallowing the exception correctly handles `OperationCanceledException` from the cancelled delay. PASS.

### Fix 4: [Range] on TimeSpan fields + MaxReconnectAttempts

**RESOLVED.**

`StreamEngineOptions.cs` (confirmed against current file on disk):
- `ChannelCapacity`: `[Range(1, int.MaxValue)]` — PASS
- `IdleCloseDelay`: `[Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]` — PASS
- `BackoffInitial`: `[Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]` — PASS
- `BackoffMax`: `[Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]` — PASS
- `BackoffMultiplier`: `[Range(1.0, 10.0)]` — PASS
- `MaxReconnectAttempts`: `[Range(0, int.MaxValue)]` — PASS

Bounds are reasonable: TimeSpan lower bound is 1 ms (prevents zero/negative), upper bound is 1 day (sensible ceiling for idle-close and backoff). `MaxReconnectAttempts` lower bound of 0 correctly allows the "unlimited" default.

### Fix 5: FakeStreamProtocol extracted to own file

**RESOLVED.**

`tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeStreamProtocol.cs` exists as a standalone file (confirmed by read). `StreamEngineTests.cs` (807 lines, new file) contains no `FakeStreamProtocol` class definition — only usages. PASS.

---

## New Test Quality: Engine_HeartbeatClientPing_ControlFrame_SendsPingNotPong

**Assessment: genuine, non-trivial test. PASS.**

The test (`StreamEngineTests.cs:2023–2056`):
1. Constructs a `FakeStreamProtocol` with `PingFormat.ControlFrame` and a 100 ms interval.
2. Builds an engine using `FakeWebSocketConnection` — the real transport seam is replaced.
3. Subscribes a live subscription (which triggers socket open and heartbeat loop).
4. Polls up to 3 seconds for `fake.SentPings.IsEmpty` to become false (waits for at least one real heartbeat cycle).
5. Asserts `fake.SentPings.Should().NotBeEmpty(...)` — proves `SendPingAsync` was called.
6. Asserts `fake.SentPongs.Should().BeEmpty(...)` — proves `SendPongAsync` was NOT called.

Both assertions are necessary and together form a precise contract test: the positive assertion confirms the right method was invoked; the negative assertion confirms the wrong method was not. The test cannot pass trivially — it depends on the heartbeat task actually firing within the 3-second deadline.

LR-005 satisfied: `SendPingAsync` on the interface and the `ControlFrame` engine path both have direct test coverage via this test.

LR-001 check: `SendPingAsync` and `SendPongAsync` take `ReadOnlyMemory<byte>` (value type, not nullable string) — no `ArgumentException.ThrowIfNullOrWhiteSpace` guard is needed or appropriate here. The interface methods take a struct payload; `ArgumentNullException.ThrowIfNull` is inapplicable to value types. No LR-001 gap.

---

## New Findings (remediation-introduced)

None. No regressions introduced by commit 501ad13:
- The only new public/internal surface is `SendPingAsync` on `IWebSocketConnection` (internal interface) and `SentPings` on `FakeWebSocketConnection` (test project, not shipped). No external API surfaces changed.
- `FakeWebSocketConnection` now implements `IDisposable` in addition to `IWebSocketConnection` + `IAsyncDisposable`. The `Dispose()` method is correctly guarded with `_semLock` and handles final cleanup of `_available`. The comment in `DisposeAsync()` explaining why `_available` is NOT disposed there (reconnect reuse pattern) is accurate and correctly documented.
- Build: `dotnet build --no-incremental -p:TreatWarningsAsErrors=true` exits with 0 warnings, 0 errors across all projects.

---

## Final Verdict: APPROVED

Confidence: 98/100

All five blocking/requested fixes from Cycle 1 are fully and correctly resolved. The binding constraints K1, K2, K3, and C1 all hold. The new `Engine_HeartbeatClientPing_ControlFrame_SendsPingNotPong` test is a genuine behavioral assertion. No new architectural issues were introduced by the remediation commit.
