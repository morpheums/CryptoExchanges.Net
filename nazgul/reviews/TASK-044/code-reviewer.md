# Code Review — TASK-044 (Cycle 2)

## Verdict: APPROVED

---

## Prior Blocking Finding Resolution

### FINDING-1 (was BLOCKING): ControlFrame branch called SendPongAsync instead of SendPingAsync

**RESOLVED.**

Evidence from `StreamEngine.cs:629-636` (current HEAD):

```csharp
case PingFormat.ControlFrame:
    // RFC 6455 §5.5.2: client-initiated heartbeat sends a Ping control
    // frame (opcode 0x09). SendPingAsync carries the correct semantics;
    // SendPongAsync (opcode 0x0A) is reserved for replying to server pings.
    await _socket.SendPingAsync(policy.ClientPingPayload, ct).ConfigureAwait(false);
    break;
```

The regression check `grep -n "SendPongAsync" StreamEngine.cs` returns only line 634, which is inside the comment explaining why `SendPongAsync` is NOT called — confirming `SendPongAsync` is not invoked anywhere in the engine's send path.

`IWebSocketConnection.SendPingAsync` (`IWebSocketConnection.cs:49-55`) has correct XML docs:
- `<summary>` explicitly names "Ping control frame (RFC 6455 §5.5.2)"
- Notes opcode semantics and `PingFormat.ControlFrame` usage

`FakeWebSocketConnection.SendPingAsync` (`FakeWebSocketConnection.cs:83-87`) correctly enqueues to `SentPings`, not `SentPongs`.

New test `Engine_HeartbeatClientPing_ControlFrame_SendsPingNotPong` (`StreamEngineTests.cs:2023-2056`):
- Configures `ClientPing` + `PingFormat.ControlFrame` policy with a 100 ms interval.
- Polls `fake.SentPings.IsEmpty` with a 3-second deadline — no flaky fixed sleep.
- Asserts `fake.SentPings.Should().NotBeEmpty(...)` — positive assertion.
- Asserts `fake.SentPongs.Should().BeEmpty(...)` — critical anti-regression, confirmed present.
- Uses `FakeWebSocketConnection` — no network.

All four sub-requirements confirmed.

---

### FINDING-2 (was non-blocking): `_idleCloseTask` not awaited in `DisposeAsync`

**RESOLVED.**

Evidence from `StreamEngine.cs:744-756`:

```csharp
var idleCloseTask = _idleCloseTask;   // capture BEFORE cancel
CancelIdleClose();                     // cancels and nulls _idleCloseCts / _idleCloseTask
if (idleCloseTask is not null)
{
    try { await idleCloseTask.ConfigureAwait(false); }
#pragma warning disable CA1031 // intentional: idle-close task exits on cancellation
    catch (Exception) { /* swallow — exits via OperationCanceledException */ }
#pragma warning restore CA1031
}
```

Task reference is captured on line 744 before `CancelIdleClose()` is called on line 745. The pattern exactly matches the capture-before-cancel + await-with-swallow pattern required. `ConfigureAwait(false)` is present. The pragma pair is justified with a comment. This is consistent with how `_heartbeatTask` (lines 769-775) and `_pumpTask` (lines 777-782) are handled in the same method.

---

### FINDING-3 (was non-blocking): `MaxSubscriptionsPerSocket` dead property

**RESOLVED.**

`grep -n "MaxSubscriptionsPerSocket"` on `StreamEngineOptions.cs` returns no output. The property is completely absent. `[Range]` attributes are present on all remaining properties:
- `[Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]` on `IdleCloseDelay`, `BackoffInitial`, and `BackoffMax`.
- `[Range(0, int.MaxValue)]` on `MaxReconnectAttempts`.
- `[Range(1.0, 10.0)]` on `BackoffMultiplier`.
- `[Range(1, int.MaxValue)]` on `ChannelCapacity`.

---

### FINDING-4 (was non-blocking): `FakeStreamProtocol` co-located in `StreamEngineTests.cs`

**RESOLVED.**

`FakeStreamProtocol.cs` exists at `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeStreamProtocol.cs` with a single class — one-type-per-file convention satisfied. All interface members carry `/// <inheritdoc/>`. `StreamEngineTests.cs` (807 lines) contains no `FakeStreamProtocol` class definition.

---

## New Findings

No new blocking findings were introduced by the remediation.

### Finding: `FakeWebSocketConnection.IDisposable` — no `GC.SuppressFinalize`
- **Severity**: LOW
- **Confidence**: 50
- **File**: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeWebSocketConnection.cs:124-130`
- **Category**: Code Quality
- **Verdict**: PASS (confidence below threshold; test-only code; no finalizer; build is clean at 0 warnings)

---

## Async Correctness Assessment

All async patterns in new and changed code are correct:

- Every `await` in `StreamEngine.cs`, `StreamSubscriptionChannel.cs`, and `FakeWebSocketConnection.cs` uses `.ConfigureAwait(false)`.
- `CancellationToken` is forwarded throughout. `OperationCanceledException` is re-thrown at every cancellation boundary in loops (`PumpLoopAsync:444`, `ClientPingLoopAsync:617`, `WatchdogAsync:771`, `ReconnectCoreAsync:490,592`).
- No `async void` introduced anywhere.
- All fire-and-forget `Task.Run(...)` invocations are intentional and documented.
- All `IDisposable`/`IAsyncDisposable` instances are disposed in `DisposeAsync`. `await using` is used consistently in tests.

---

## LR-001 / LR-005 Compliance

**LR-001**: `SendPingAsync` accepts `ReadOnlyMemory<byte>` (not a string) and `CancellationToken ct`. No string parameters — `ArgumentException.ThrowIfNullOrWhiteSpace` is not applicable. No new string-accepting public methods were added without guards.

**LR-005**: `SendPingAsync` on the interface is covered by `Engine_HeartbeatClientPing_ControlFrame_SendsPingNotPong`. The test calls `SubscribeAsync`, waits for the heartbeat loop to fire, and asserts both `SentPings.NotBeEmpty` and `SentPongs.BeEmpty`. Coverage requirement satisfied.

---

## Build and Test Results

- `dotnet build CryptoExchanges.Net.sln --configuration Release`: **0 warnings, 0 errors** (TreatWarningsAsErrors=true).
- `dotnet test` (unit tests only): **503 passed, 0 failed, 0 skipped** across all unit test assemblies. The `CryptoExchanges.Net.Http.Tests.Unit` assembly passed all 71 tests including the new `Engine_HeartbeatClientPing_ControlFrame_SendsPingNotPong` test.

---

## Final Verdict: APPROVED

Confidence: 98/100.

All four prior findings — one blocking and three non-blocking — are fully resolved. The remediation is correct, minimal, and follows existing codebase patterns. No new blocking issues were introduced.
