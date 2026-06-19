# API Review — TASK-044

## Verdict: CHANGES_REQUESTED

---

## Findings

### FINDING-1: PingFormat.ControlFrame sends Pong instead of Ping (semantic bug)
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:631-632`
- **Blocking**: YES

`ClientPingLoopAsync` handles `PingFormat.ControlFrame` by calling `_socket.SendPongAsync(...)`. `IWebSocketConnection.SendPongAsync` is documented as "Used by the engine to respond to server-initiated Ping frames" (RFC 6455 §5.5.3). A client-initiated control-frame ping should be a **Ping** control frame (RFC 6455 §5.5.2), not a Pong. The interface has no `SendPingAsync` method.

Two possible fixes (either acceptable):
1. Add `Task SendPingAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)` to `IWebSocketConnection` and call it in the `ControlFrame` case. This is the correct RFC 6455 approach.
2. Intentionally re-use `SendPongAsync` as the sole control-frame send method and rename it to `SendControlFrameAsync` (or document that it covers both ping and pong), accepting that the underlying `ClientWebSocket` implementation will decide the correct opcode. If this is the deliberate design (because .NET's `ClientWebSocket` does not expose `SendPingAsync`), add a code comment explaining that `SendPongAsync` is used for the initial client ping in `PingFormat.ControlFrame` because `ClientWebSocket` only exposes pong sending, and the ping-pong exchange is handled by the TLS stack. Do not leave it silently calling the wrong semantic method name.

**Pattern reference**: `IWebSocketConnection.cs:48-54` (doc says Pong = "respond to server-initiated Ping").

---

### FINDING-2: MaxSubscriptionsPerSocket is a dead property — implicit reserved member
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngineOptions.cs:53-57`
- **Blocking**: NO

`MaxSubscriptionsPerSocket` is defined in `StreamEngineOptions` with a `[Range(1, int.MaxValue)]` attribute and a default of `1024`, but is never read anywhere in `StreamEngine`. The engine has no sharding logic. Per plan constraints, "no reserved-for-v1.1 members" are allowed on types. This is an unreachable property that misleads consumers into thinking socket sharding is active.

Fix: Either (a) remove the property entirely until sharding is implemented (YAGNI), or (b) add an `#pragma warning` or prominent `/// <remarks>Not yet enforced — sharding is planned for a future release.</remarks>` with a TODO tracking issue. Option (a) is preferred.

---

### FINDING-3: TimeSpan StreamEngineOptions fields lack [Range] validation
- **Severity**: LOW
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngineOptions.cs:25,30,35,49`
- **Blocking**: NO

`IdleCloseDelay`, `BackoffInitial`, `BackoffMax`, and `MaxReconnectAttempts` have no `[Range]` validation attributes, while `ChannelCapacity`, `BackoffMultiplier`, and `MaxSubscriptionsPerSocket` do. The options doc says "All values are validated on container start via `ValidateOnStart`." Without `[Range]`, an operator who sets `BackoffInitial = TimeSpan.Zero` or a negative `MaxReconnectAttempts` would get no validation error at startup — the `BackoffSchedule` constructor would throw at runtime during the first reconnect, which is a poor failure mode.

Fix: Add `[Range(typeof(TimeSpan), "00:00:00.001", "1.00:00:00")]` (or appropriate bounds) to `BackoffInitial`, `BackoffMax`, and `IdleCloseDelay`. Add `[Range(0, int.MaxValue)]` to `MaxReconnectAttempts` if zero is the only valid non-positive sentinel. Note: `[Range]` on `TimeSpan` requires .NET 8+ data annotations — verify target framework before applying.

---

### FINDING-4: SubscriptionEntry inner class has public members inside a private class — minor style inconsistency
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:823-839`
- **Blocking**: NO

`SubscriptionEntry` is `private sealed class` but its three properties (`Channel`, `WriteFrame`, `Handle`) are `public`. Since the containing type is `private`, these are effectively inaccessible outside the engine — no real encapsulation leak — but the `public` modifier on members of a `private sealed` class is misleading. Convention in this codebase is to use `internal` on inner types intended for sibling visibility (`IEngineHandle` at line 843 is correctly `internal`).

Fix: Change `SubscriptionEntry`'s three properties to `internal` or `private get`-only. This is cosmetic but aligns with codebase conventions.

---

### FINDING-5: REST surface — confirmed untouched
- **Severity**: N/A
- **Confidence**: 100
- **File**: N/A
- **Blocking**: NO (PASS)

`grep -rn "IExchangeClient|IMarketDataService|ITradingService|IAccountService"` in the Streaming directory returns empty. No REST interface is referenced or modified by this diff.

---

### FINDING-6: No public type leakage from Http.Streaming
- **Severity**: N/A
- **Confidence**: 100
- **File**: N/A
- **Blocking**: NO (PASS)

`grep "^public class|^public sealed|^public interface|^public record|^public struct"` on all Streaming `.cs` files returns empty. All new types (`StreamEngine`, `StreamSubscriptionChannel<T>`, `StreamSubscriptionHandle<T>`, `BackoffSchedule`, `StreamEngineOptions`) are `internal sealed`. `FakeWebSocketConnection` and `FakeStreamProtocol` are in the test project and are `public` only to test infrastructure — correct.

---

### FINDING-7: IStreamSubscription contract fully implemented
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamSubscriptionHandle.cs`
- **Blocking**: NO (PASS)

`StreamSubscriptionHandle<T>` implements `IStreamSubscription` and `StreamEngine.IEngineHandle`. All required members are present: `State` (line 45), `IsConnected` (line 48), `DisposeAsync` (line 51), `SetState` (line 60), `ReconnectingCallback` (line 64), `ReconnectedCallback` (line 67). `DisposeAsync` is idempotent via `Interlocked.Exchange` on `_disposed`. `State` read is atomic (volatile int cast).

---

### FINDING-8: XML docs coverage
- **Severity**: N/A
- **Confidence**: 100
- **File**: all new Streaming source files
- **Blocking**: NO (PASS)

All public/internal method and class declarations have `<summary>` XML docs. Constructor parameters carry `<param>` entries. `<inheritdoc/>` is used consistently on implementations. `StreamEngine.IEngineHandle` members have `<summary>`. LEAN — no redundant prose observed.

---

## LR-001 Compliance

| Method | String Param | Guard Present |
|--------|-------------|---------------|
| `StreamEngine.UnsubscribeAsync(string routingKey)` | `routingKey` | `ArgumentException.ThrowIfNullOrWhiteSpace(routingKey)` — line 241 PASS |
| `StreamSubscriptionHandle<T>` ctor `(string routingKey, ...)` | `routingKey` | `ArgumentException.ThrowIfNullOrWhiteSpace(routingKey)` — line 32 PASS |
| `StreamEngine.BuildRoutingKey(StreamRequest request)` | no string param (takes record, null-guarded with `ThrowIfNull`) | PASS |
| `StreamEngine` ctor | no string params | PASS |
| `StreamSubscriptionChannel<T>` ctor | no string params | PASS |
| `BackoffSchedule` ctor | no string params | PASS |

LR-001 compliance is complete. All non-optional string parameters have `ThrowIfNullOrWhiteSpace` as their first guard statement.

---

## Summary

TASK-044 delivers a well-structured, fully internal byte-engine with strong test coverage, correct encapsulation, zero REST surface modifications, and full LR-001 compliance. One blocking issue is raised: `PingFormat.ControlFrame` in `ClientPingLoopAsync` calls `SendPongAsync` (a Pong control frame) rather than a Ping frame — the semantics are backwards per RFC 6455 §5.5.2 vs §5.5.3, and `IWebSocketConnection` has no `SendPingAsync`. This must be resolved either by adding `SendPingAsync` to the interface or by explicitly documenting that the design intentionally reuses `SendPongAsync` as the single control-frame send path. Two non-blocking concerns are noted: `MaxSubscriptionsPerSocket` is a dead property that violates the no-reserved-member rule, and `TimeSpan`-typed `StreamEngineOptions` fields lack `[Range]` validation attributes that would enforce startup-time validation.
