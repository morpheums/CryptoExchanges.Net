---
reviewer: architect-reviewer
task: TASK-061
verdict: APPROVE
confidence: 99
---
# Architect Review — TASK-061

## Verdict: APPROVE

## K1 Check (Core.Models/DeltaMapper under Http)

PASS. Grep over all three changed source files (`StreamConnectionInfo.cs`, `IStreamProtocol.cs`, `StreamEngine.cs`) returns zero hits for `Core.Models` or `DeltaMapper` as code imports or type references. The only matches in the wider Http tree are in `obj/` XML documentation files (generated artifacts, not source) and comment text in `StreamConnectionInfo.cs` that names the constraint itself. `StreamConnectionInfo` carries exactly `Uri Endpoint` and `HeartbeatPolicy Heartbeat` — both primitives already resident in Http.Streaming. No new `using` directives were introduced in any changed Http file. `CryptoExchanges.Net.Http.csproj` still has a single `ProjectReference` to Core and nothing else. K1 is fully preserved.

## C1 Check (No timers/threads in protocol)

PASS. Neither `IStreamProtocol.cs` nor `BinanceStreamProtocol.cs` contain `Task.Delay`, `Timer`, `Thread`, `Task.Run`, or any `StartHeartbeat`-style invocation. The only match in `IStreamProtocol.cs` is inside the `<remarks>` XML doc comment that names the constraint. `BinanceStreamProtocol.ResolveConnectionAsync` is a single expression-bodied method returning a pre-built `ValueTask` wrapping a cached field — no behavioral logic whatsoever. C1 is intact.

## K2 Check (Subscribe-set replay on reconnect)

PASS. `_subscribeSet` is iterated in `ReconnectCoreAsync` at line 531 of `StreamEngine.cs`:

```csharp
foreach (var (routingKey, request) in _subscribeSet)
{
    var subscribeText = _protocol.BuildSubscribe(request);
    await _socket.SendTextAsync(subscribeText, _disposeCts.Token)...
}
```

This line is unchanged by the diff. The `StartPump` extraction (from the diff) does not touch the subscribe-set replay block — it only encapsulates the pump/heartbeat wiring lines that precede it. K2 replay is intact.

## K3 Check (Engine backoff, not Polly)

PASS. `ReconnectCoreAsync` uses the engine's own `BackoffSchedule _backoff` (`_backoff.Next()`, `_backoff.Attempt`, `_backoff.Reset()`). `ExchangeResiliencePipeline` and Polly do not appear anywhere in `StreamEngine.cs`. The `StartPump` extraction correctly calls `_backoff.Reset()` as its last statement, preserving the post-connect backoff reset that was previously inline at both call sites.

## ADR-002 Compliance

PASS. Both call sites confirmed at:
- `OpenSocketAsync` — line 285: `var info = await _protocol.ResolveConnectionAsync(ct).ConfigureAwait(false);`
- `ReconnectCoreAsync` — line 506: `info = await _protocol.ResolveConnectionAsync(_disposeCts.Token).ConfigureAwait(false);`

The Binance implementation caches `StreamConnectionInfo` in the constructor (line 35 of `BinanceStreamProtocol.cs`) and returns it on every call via `new(_connectionInfo)` — the no-alloc `ValueTask<T>(T result)` constructor, not `ValueTask.FromResult` (which allocates a `Task<T>` wrapper). This satisfies the "cached-instance" requirement from ADR-002 and the task description.

## Findings

### Finding: `StartPump` extraction is correct and DRY

- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:583-589`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: The simplifier extracted the three-line pump+heartbeat+backoff sequence shared by `OpenSocketAsync` and `ReconnectCoreAsync` into a private `StartPump(HeartbeatPolicy heartbeat)` method. Both call sites now delegate to it. The extraction is correct: `_pumpCts` creation, `PumpLoopAsync` task start, `StartHeartbeat` call, and `_backoff.Reset()` are all present and in the original order.

### Finding: `HeartbeatLoopAsync` signature change is safe

- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:610`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: `HeartbeatLoopAsync` was changed from reading `_protocol.Heartbeat` to accepting `HeartbeatPolicy policy` as a parameter. The policy is now sourced from the resolved `StreamConnectionInfo`, passed through `StartPump` → `StartHeartbeat` → `HeartbeatLoopAsync`. This is the correct propagation chain. No global state or re-read of the protocol field occurs post-connect.

### Finding: ValueTask no-alloc path in Binance

- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs:39-40`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: `new(_connectionInfo)` uses the `ValueTask<T>(T result)` constructor which is genuinely alloc-free for cached reference types — correct and preferable to `ValueTask.FromResult` which boxes through `Task.FromResult<T>`.

### Finding: FakeStreamProtocol allocs a new `StreamConnectionInfo` on each call

- **Severity**: LOW
- **Confidence**: 70
- **File**: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeStreamProtocol.cs:56-61`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `FakeStreamProtocol.ResolveConnectionAsync` constructs `new StreamConnectionInfo(_endpoint, HeartbeatPolicy)` on every call. This is correct behavior for the reconnect test (`Engine_CallsResolveConnectionAsync_OnEachReconnect`) but means the test cannot use reference-equality to assert the "cached instance" property the way `BinanceStreamProtocolTests.ResolveConnectionAsync_ReturnsCachedInstance` does. The test instead relies on `ResolveCount` — which is the right way to test the engine's call frequency, separate from the Binance-specific caching test. No functional issue; minor note for future test authors.

### Finding: `CancellingStreamProtocol` throws synchronously (not from async path)

- **Severity**: LOW
- **Confidence**: 75
- **File**: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineResolveConnectionTests.cs:181-182`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The test double throws `OperationCanceledException` via `throw` in an expression-bodied `ValueTask<StreamConnectionInfo>` method — i.e., it throws synchronously before returning a `ValueTask`. The engine's `await` of a `ValueTask` that throws synchronously during construction (not from an awaitable) will still surface the exception to the `SubscribeAsync` caller because `ConfigureAwait(false)` on a faulted `ValueTask` re-throws on `GetResult()`. The test passes (confirmed 87/87), and the behavior is correct. However, a more realistic KuCoin-style async cancellation would throw via `ct.ThrowIfCancellationRequested()` inside a true `async ValueTask` method. The current test covers the synchronous-throw path; an additional test for the truly-async-cancelled path (where `ct` is pre-cancelled) would give marginally stronger coverage but is not required by the acceptance criteria.

## Build and Test Results

- `dotnet build --no-incremental -warnaserror`: **0 warnings, 0 errors** — Build succeeded.
- `CryptoExchanges.Net.Http.Tests.Unit` (all): **87 passed, 0 failed** — including all 4 new `StreamEngineResolveConnectionTests` and all pre-existing engine/client tests.
- `CryptoExchanges.Net.Binance.Tests.Unit` (streaming filter): **22 passed, 0 failed** — Binance streaming regression clean.

## Summary

All hard constraints pass:
- PASS: K1 (no Core.Models/DeltaMapper under Http) — confirmed by grep over source files; `StreamConnectionInfo` carries only `Uri` + `HeartbeatPolicy`.
- PASS: C1 (no timers/threads in protocol) — `IStreamProtocol` and `BinanceStreamProtocol` contain zero behavioral code.
- PASS: K2 (subscribe-set replay on reconnect) — `_subscribeSet` iteration unchanged in `ReconnectCoreAsync`.
- PASS: K3 (engine backoff, not Polly) — `BackoffSchedule` used exclusively; no `ExchangeResiliencePipeline` reference.
- PASS: ADR-002 compliance — both call sites (`OpenSocketAsync` + `ReconnectCoreAsync`) await `ResolveConnectionAsync`; Binance caches and returns a single instance via the no-alloc `ValueTask<T>(T)` constructor.
- PASS: `StreamConnectionInfo` design — `internal sealed record` in the correct `CryptoExchanges.Net.Http.Streaming` namespace, two positional properties only, full XML docs.
- PASS: Cancellation propagation — `OperationCanceledException` from `ResolveConnectionAsync` propagates to the `SubscribeAsync` caller; test confirmed passing.
- PASS: XML docs — `IStreamProtocol.ResolveConnectionAsync` and `StreamConnectionInfo` carry complete XML documentation including constraint references.
- PASS: `StartPump` extraction — correct encapsulation, both call sites use it consistently, backoff reset preserved.
- CONCERN (non-blocking, confidence 70): `FakeStreamProtocol` allocates on each `ResolveConnectionAsync` call — correct test behavior, minor documentation note for future test authors.
- CONCERN (non-blocking, confidence 75): `CancellingStreamProtocol` tests the synchronous-throw path only — covers the acceptance criterion; an async `ct`-cancelled variant would give fuller coverage but is not required.

No blocking findings. The implementation faithfully realizes ADR-002 with zero behavioral regression.
