---
reviewer: api-reviewer
task: TASK-061
verdict: APPROVE
confidence: 97
---
# API Review — TASK-061

## Verdict: APPROVE

## Learned Rule Checks

### LR-004: Array parameter guards
No new array-accepting methods were introduced. `ResolveConnectionAsync(CancellationToken ct)` takes a single `CancellationToken` parameter. `StreamConnectionInfo` is a positional record with `Uri` and `HeartbeatPolicy` parameters — neither is an array. The pre-existing `Classify(ReadOnlySpan<byte> frame)` on `IStreamProtocol` is unchanged. LR-004 does not apply to this diff.

## Findings

### Finding 1: IStreamClient was NOT modified — public contract intact
- **Severity**: N/A (confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Interfaces/IStreamClient.cs`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. `IStreamClient` (`SubscribeToTickerAsync`, `SubscribeToTradesAsync`, `SubscribeToOrderBookAsync`, `SubscribeToKlinesAsync`, `ExchangeId`) was not touched. Public API surface is unchanged.

### Finding 2: IStreamProtocol is internal — breaking change scoped correctly
- **Severity**: N/A (confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:21`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. The `internal interface IStreamProtocol` declaration is unchanged in access modifier. Removing `Uri Endpoint { get; }` and `HeartbeatPolicy Heartbeat { get; }` and replacing with `ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct)` is a breaking change only for `internal` implementers within the assembly, which is controlled. All implementers (Binance, test fakes) have been updated.

### Finding 3: StreamConnectionInfo is internal sealed record — not exported
- **Severity**: N/A (confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs:23`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. `internal sealed record StreamConnectionInfo(Uri Endpoint, HeartbeatPolicy Heartbeat)` carries the `internal` modifier and lives in `CryptoExchanges.Net.Http.Streaming`. It is not re-exported through any public type.

### Finding 4: HeartbeatPolicy remains internal
- **Severity**: N/A (confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/HeartbeatPolicy.cs:31`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. `HeartbeatPolicy` is declared `internal sealed record` and is referenced only within `CryptoExchanges.Net.Http.Streaming`. The diff does not change its access modifier.

### Finding 5: ValueTask<T> return type — correct choice for hot path
- **Severity**: N/A (confirmation)
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:51`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. `ValueTask<StreamConnectionInfo>` avoids a heap allocation on the Binance synchronous cached path (`new ValueTask<StreamConnectionInfo>(_connectionInfo)`). Async I/O implementations for KuCoin can return a genuine `Task<StreamConnectionInfo>` wrapped in `ValueTask`. This is the correct choice per .NET guidelines for interfaces that may have both sync and async implementers.

### Finding 6: ResolveConnectionAsync naming and CancellationToken placement
- **Severity**: N/A (confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:51`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. Method is named with the `Async` suffix, parameter is named `ct` (consistent with the rest of the codebase per `IExchangeClient.cs:18`), and positioned as the sole/last parameter.

### Finding 7: Doc comment references ValueTask.FromResult{TResult} — API is valid in .NET 7+
- **Severity**: LOW
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:33`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: The XML doc comment states "For static venues the implementation returns a pre-built `StreamConnectionInfo` via `ValueTask.FromResult{TResult}` with negligible overhead." The actual Binance implementation uses `new ValueTask<StreamConnectionInfo>(_connectionInfo)` rather than `ValueTask.FromResult`. Both are valid: `ValueTask.FromResult<T>` is a static method on the non-generic `ValueTask` struct introduced in .NET 7 and used elsewhere in this codebase (`ExchangeResiliencePipeline.cs:38`). However the doc suggests a pattern not used by the reference implementation, which could confuse future KuCoin implementers who copy from the Binance example rather than the interface doc. This is a non-blocking documentation inconsistency.
- **Fix**: Either change the doc to say "via `new ValueTask<StreamConnectionInfo>(info)`" or update the Binance implementation to use `ValueTask.FromResult` for consistency with the documented pattern. Recommending the latter: `=> ValueTask.FromResult(_connectionInfo);` is marginally more readable and matches the doc.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeResiliencePipeline.cs:38` (existing use of `ValueTask.FromResult` in this codebase)

### Finding 8: StreamEngine.OpenSocketAsync — ResolveConnectionAsync called before socket created
- **Severity**: N/A (design confirmation)
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs` (diff lines 135-145)
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. The ordering `var info = await _protocol.ResolveConnectionAsync(ct); _socket = _connectionFactory(); await _socket.ConnectAsync(info.Endpoint, ct)` is correct. The endpoint URI is resolved before the socket factory is called, which is the right ordering for KuCoin-style token negotiation: the fresh token is in the URI before the WebSocket handshake.

### Finding 9: Duplicate code elimination via StartPump helper
- **Severity**: N/A (positive confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs` (diff lines 583-588)
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. The `StartPump(HeartbeatPolicy heartbeat)` private helper correctly consolidates the four-step sequence (`_pumpCts`, `_pumpTask`, `StartHeartbeat`, `_backoff.Reset()`) that was previously duplicated in both `OpenSocketAsync` and `ReconnectCoreAsync`. This is a clean refactor.

### Finding 10: Test coverage for the new seam
- **Severity**: N/A (positive confirmation)
- **Confidence**: 100
- **File**: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineResolveConnectionTests.cs`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. Four unit tests cover: (1) resolve called on first connect, (2) resolve called on each reconnect, (3) resolve count increments across multiple disconnects (anti-caching regression), (4) `OperationCanceledException` propagation from resolve. `BinanceStreamProtocolTests` adds two tests for `ResolveConnectionAsync`: heartbeat direction and cached-instance identity. Test coverage is thorough.

### Finding 11: FakeStreamProtocol.ResolveCount instrumentation
- **Severity**: N/A (positive confirmation)
- **Confidence**: 100
- **File**: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeStreamProtocol.cs:45`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. The `ResolveCount` counter on the test double allows tests to assert the engine calls `ResolveConnectionAsync` the expected number of times, directly testing the KuCoin enablement story (fresh token per connect).

### Finding 12: Static field renamed from HeartbeatPolicyInstance to s_heartbeatPolicy
- **Severity**: N/A (positive confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs:14`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. The rename from `HeartbeatPolicyInstance` to `s_heartbeatPolicy` aligns with .NET static field naming convention (`s_` prefix) and is an internal-only change with no external impact.

## Summary
- PASS: `IStreamClient` public contract — not modified, fully backward-compatible
- PASS: `IStreamProtocol` breaking change scoped to `internal` only — all implementers updated
- PASS: `StreamConnectionInfo` — `internal sealed record`, not exported
- PASS: `HeartbeatPolicy` — remains `internal sealed record`, not affected
- PASS: `ValueTask<StreamConnectionInfo>` return type — correct zero-alloc choice for sync-cached path
- PASS: `ResolveConnectionAsync` naming and `CancellationToken ct` placement — conforms to codebase conventions
- PASS: `StreamEngine` ordering — resolve before socket creation is correct for token-negotiated venues
- PASS: `StartPump` helper — eliminates duplication correctly
- PASS: Test coverage — `StreamEngineResolveConnectionTests` + `BinanceStreamProtocolTests` thoroughly verify the new seam
- CONCERN: Doc comment says `ValueTask.FromResult{TResult}` but Binance uses constructor form — minor doc/impl inconsistency (confidence: 85/100, non-blocking)

The interface change is clean, scoped entirely within `internal` types, and correctly enables async token negotiation for future exchanges (KuCoin) without any further interface changes. The public API is unchanged.
