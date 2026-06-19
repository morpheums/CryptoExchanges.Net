# Code Review — TASK-045

## Verdict: APPROVED

## Findings

| # | File | Line | Severity | Confidence | Status | LR | Description |
|---|------|------|----------|------------|--------|----|-------------|
| 1 | ClientWebSocketConnection.cs | 41-46 | MEDIUM | 70 | CONCERN | — | SendPingAsync/SendPongAsync use `WebSocketMessageType.Binary`; interface doc claims RFC 6455 control frames |
| 2 | ClientWebSocketConnection.cs | 41-46 | LOW | 85 | CONCERN | — | Spurious async: single-expression `async Task => await … .ConfigureAwait(false)` on both methods |
| 3 | StreamServiceRegistration.cs | 94 | LOW | 75 | CONCERN | — | Engine allocated inside keyed-singleton factory with no try/finally; leaked if `StreamClient` ctor throws |
| 4 | StreamClientFactory.cs | 84 | LOW | 60 | CONCERN | — | `catch {}` in `Create` uses blocking `.GetAwaiter().GetResult()` to dispose async resource |

---

## Detailed Findings

### Finding 1: SendPingAsync / SendPongAsync — semantic mismatch between interface contract and implementation

- **Severity**: MEDIUM
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Http/Streaming/ClientWebSocketConnection.cs:41-46`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — confidence 70, implementation may be intentionally application-level binary)
- **Issue**: The `IWebSocketConnection` interface documents `SendPingAsync` as "a WebSocket Ping control frame (RFC 6455 §5.5.2)" and `SendPongAsync` as "a WebSocket Pong control frame (RFC 6455 §5.5.3)". The implementation sends `WebSocketMessageType.Binary` for both. `System.Net.WebSockets.ClientWebSocket` does not support sending RFC 6455 Ping/Pong control frames via `SendAsync` at all — the managed WebSocket stack handles those automatically. This means:
  - If the engine is wired for `HeartbeatDirection.ClientPingServerPong` and calls `SendPingAsync`, the exchange receives a regular binary data frame, not a control-frame ping. Most crypto exchanges do not interpret a binary data frame as a heartbeat ping.
  - The interface-level contract (RFC 6455 §5.5.2) is misleading. Callers relying on the doc comment will think a true control-frame Ping is being sent.
- **Fix**: Either (a) update the interface and implementation docs to state that "application-level binary pings" are used (correct for most crypto WebSocket APIs that define their own heartbeat JSON text), or (b) if true control-frame pings are needed, `ClientWebSocket` does not support sending them — document that limitation in the implementation. The broader concern is whether the engine ever calls `SendPingAsync` with `WebSocketMessageType.Binary` as the correct transport for any registered exchange.
- **Pattern reference**: Interface contract at `src/CryptoExchanges.Net.Http/Streaming/IWebSocketConnection.cs:49-63`

---

### Finding 2: Spurious async on SendPingAsync / SendPongAsync

- **Severity**: LOW
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Http/Streaming/ClientWebSocketConnection.cs:41-46`
- **Category**: Code Quality
- **Verdict**: CONCERN (non-blocking — low severity)
- **Issue**: Both `SendPingAsync` and `SendPongAsync` are declared `async Task` with a single expression-body `await … .ConfigureAwait(false)`. This generates a state machine unnecessarily. `SendTextAsync` (line 33-38) correctly avoids this by returning the Task directly without `async/await`. The pattern is inconsistent.
- **Fix**: Remove `async` and return the `Task` directly, identical to how `ConnectAsync` and `SendTextAsync` are written:
  ```csharp
  public Task SendPingAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
      => _ws.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken: ct);
  ```
- **Pattern reference**: `ClientWebSocketConnection.cs:33-38` (SendTextAsync — correct pattern)

---

### Finding 3: StreamEngine leaked in AddStreams keyed-singleton factory if StreamClient constructor throws

- **Severity**: LOW
- **Confidence**: 75
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamServiceRegistration.cs:94-112`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — StreamClient constructor is effectively infallible in practice)
- **Issue**: In the `AddKeyedSingleton<IStreamClient>` factory lambda (lines 94-112), the `StreamEngine` is allocated at line 109 and handed directly to `new StreamClient(engine, symbolMapper, exchangeId)` at line 111. There is no `try/catch` or `try/finally` around this. If `StreamClient`'s constructor throws (after the engine is created), the engine is leaked. The `StreamClientFactory.Create` static path (lines 80-88) correctly wraps this pattern in a `try/catch` that disposes the engine. The DI path has no equivalent guard.
- **Fix**: Mirror the `Create` method's pattern in the keyed-singleton factory lambda:
  ```csharp
  var engine = new StreamEngine(protocol, decoders, engineOpts, connFactory, logger);
  try { return new StreamClient(engine, symbolMapper, exchangeId); }
  catch { engine.DisposeAsync().AsTask().GetAwaiter().GetResult(); throw; }
  ```
  Or extract a shared helper. In practice `StreamClient`'s ctor guards are `ThrowIfNull` — they would only throw on a null-providing DI factory, which is already broken. The risk is low but the asymmetry between the two construction paths is notable.
- **Pattern reference**: `StreamClientFactory.cs:80-88`

---

### Finding 4: Blocking async-dispose in exception handler of StreamClientFactory.Create

- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamClientFactory.cs:86`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — exception path is unreachable in practice, low confidence)
- **Issue**: `engine.DisposeAsync().AsTask().GetAwaiter().GetResult()` inside a `catch` block synchronously blocks on an async dispose. This risks deadlock on runtimes with a synchronization context (e.g. ASP.NET 4.x, Blazor WASM). On .NET 10 with the default thread-pool context this is safe, but the pattern is not idiomatic. The comment says "ownership transfer" but the engine's `DisposeAsync` could block the caller thread if the channel drain takes time.
- **Fix**: This code path is only reached when `StreamClient`'s constructor throws, which cannot happen in practice (it only guards for null, not thrown by DI). If you add the guard to `StreamServiceRegistration` (Finding 3), the parallel pattern is acceptable as-is given the unreachability. No urgent fix required.
- **Pattern reference**: `StreamClientFactory.cs:74-88`

---

## Summary

Build passes clean with zero warnings under `TreatWarningsAsErrors=true`. All 71 unit tests pass (20 new tests cover the 5 new types across happy-path and lifecycle scenarios). Guard coverage is thorough: all public/internal methods check `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrWhiteSpace` where appropriate (LR-001). Every new `async` method uses `.ConfigureAwait(false)`. `StreamDecoderRegistry` is populated in a single-writer construction phase before the engine starts, so the `Dictionary<StreamKind, Func<…>>` concurrency is safe. Test coverage meets LR-005: all four subscribe methods and both factory paths are tested with delivery assertions, not just smoke tests.

The two substantive concerns are (1) a semantic mismatch between the interface contract ("RFC 6455 control-frame Ping") and the Binary-type implementation — non-blocking because crypto exchanges typically use application-level heartbeats, not control-frame pings, and the engine's `FakeStreamProtocol` in tests uses a 30-second heartbeat that may never trigger in the test suite; and (2) the spurious `async` on `SendPingAsync`/`SendPongAsync` that should be cleaned up for consistency with `SendTextAsync`. No blocking REJECT items found.
