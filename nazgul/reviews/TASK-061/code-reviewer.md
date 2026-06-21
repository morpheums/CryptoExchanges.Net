---
reviewer: code-reviewer
task: TASK-061
verdict: APPROVE
confidence: 95
---
# Code Review — TASK-061

## Verdict: APPROVE

## Learned Rule Checks

### LR-001: String parameter guards
No new string parameters were introduced by any of the new or modified methods. `ResolveConnectionAsync` takes only a `CancellationToken` (value type). `StartPump` takes `HeartbeatPolicy` (value type). `StartHeartbeat` and `HeartbeatLoopAsync` similarly take only value types. LR-001 is not triggered by this diff. PASS.

### LR-005: Test coverage
All new behavior has unit test coverage:
- `StreamEngineResolveConnectionTests.cs` adds four tests: resolve called exactly once on first connect, called again on each reconnect, not cached across reconnects, `OperationCanceledException` propagation from `ResolveConnectionAsync`.
- `BinanceStreamProtocolTests.cs` adds two tests: `ResolveConnectionAsync_ReturnsServerPingClientPong` and `ResolveConnectionAsync_ReturnsCachedInstance`.
All 540+ unit tests pass. PASS.

## Findings

### Finding 1: Doc-vs-code asymmetry on `ValueTask` construction style
- **Severity**: LOW
- **Confidence**: 75
- **Blocking**: NO
- **Description**: The interface XML doc (`IStreamProtocol.cs:32-33`) tells implementors to use `ValueTask.FromResult<TResult>` for static venues. Both `BinanceStreamProtocol` (line 40-41) and `FakeStreamProtocol` (line 56-60) use `new ValueTask<StreamConnectionInfo>(cached)` instead. For a reference-type `T`, both forms are allocation-equivalent and correct. The defect is the inconsistency between the doc guidance and the actual implementation — a future KuCoin implementor reading the interface docs would follow `FromResult` while existing code shows `new(value)`. Fix: either update the `<remarks>` to state both forms are valid, or align implementations to `ValueTask.FromResult`.

### Finding 2: `StartPump` precondition is implicit
- **Severity**: LOW
- **Confidence**: 60
- **Blocking**: NO
- **Description**: `StartPump` unconditionally assigns `_pumpCts` and `_pumpTask` without asserting they are null. Both callers (`OpenSocketAsync` and `ReconnectCoreAsync`) do correctly null-out the prior fields before calling `StartPump`, so this is not a current bug. The concern is maintainability: a future caller that forgets to drain the old pump would silently leak the prior CTS and task. A `Debug.Assert(_pumpCts is null && _pumpTask is null)` at the top of `StartPump` would document the precondition explicitly.

### Finding 3: `BeSameAs` tests reference identity as a caching proxy
- **Severity**: LOW
- **Confidence**: 70
- **Blocking**: NO
- **Description**: `BinanceStreamProtocolTests.ResolveConnectionAsync_ReturnsCachedInstance` (line 213) uses `info1.Should().BeSameAs(info2)` — reference equality — to verify caching. The assertion is correct: `_connectionInfo` is a single `readonly` field returned both times via `new(_connectionInfo)`. However, if the implementation ever creates a fresh `StreamConnectionInfo` per call (still behaviorally equivalent), this test would fail even though the behavior is correct. Behavioral assertions (`info1.Endpoint.Should().Be(info2.Endpoint)`) would be more robust, or a comment explaining the identity check is intentional for the static-venue performance contract.

### Finding 4: `StreamConnectionInfo` `<remarks>` constraint block — LEAN check
- **Severity**: LOW
- **Confidence**: 65
- **Blocking**: NO
- **Description**: The `<remarks>` block on `StreamConnectionInfo` (`StreamConnectionInfo.cs:8-15`) contains "Constraint K1: this record carries ONLY...". This is architectural context explaining a non-obvious cross-project boundary invariant, which is defensible under the LEAN mandate. Not a clear-cut reject.

## Build and Test Results
- `dotnet build CryptoExchanges.Net.sln --configuration Release`: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true`.
- `dotnet test` (unit tests only): **540+ tests, 0 failed, 0 skipped**.

## Summary

The refactoring is mechanically correct. `ResolveConnectionAsync` is called fresh on every connect and every reconnect — not cached by the engine. `StartPump` faithfully consolidates the three-line post-connect wiring from both `OpenSocketAsync` and `ReconnectCoreAsync` (`_pumpCts`, `_pumpTask`, `StartHeartbeat`, `_backoff.Reset()`). `StartHeartbeat` now receives `HeartbeatPolicy` as a parameter rather than reading `_protocol.Heartbeat` directly — a clean dependency inversion. `ArgumentNullException.ThrowIfNull(options)` is present in the `BinanceStreamProtocol` constructor. `<inheritdoc/>` is on `BinanceStreamProtocol.ResolveConnectionAsync`. The `CancellingStreamProtocol` test double is correctly structured. All four concerns are LOW severity and non-blocking; none meet the HIGH/MEDIUM + confidence ≥ 80 bar for `CHANGES_REQUESTED`.

**Final Verdict: APPROVED**
