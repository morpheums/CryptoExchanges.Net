# Architect Review — TASK-045

## Verdict: APPROVED

---

## K1 Adjudication

**Mandatory ruling on `using CryptoExchanges.Net.Core.Models` in `StreamClient.cs:3`.**

- **DeltaMapper grep result**: EMPTY — no DeltaMapper references found in any `.cs` source file under `src/CryptoExchanges.Net.Http/`. The grep hits are XML doc comment strings only (in generated `.xml` output files and in XML doc comment text strings within source — not `using` directives, not functional code).

- **Model usage in StreamClient**: `Core.Models` types (`Symbol`, `Ticker`, `Trade`, `OrderBook`, `Candlestick`) appear exclusively as:
  1. Method parameter types in the `IStreamClient` interface implementations (e.g. `SubscribeToTickerAsync(Symbol symbol, StreamHandlers<Ticker> handlers, ...)`).
  2. Type arguments in `new StreamHandlers<Ticker>(onUpdate)` convenience-overload wrappers.
  3. A `Symbol` parameter passed to `_symbolMapper.ToWire(symbol)` — a call that converts the model to a wire string, delegating to the injected `ISymbolMapper`. The return value is the wire string, not a model.
  
  `StreamClient` performs **zero decode, zero model construction, zero projection, and zero DeltaMapper invocation**. The engine receives `bytes` in and delivers `object` out; StreamClient never touches those bytes. Core.Models types are present only because `IStreamClient` (defined in `Core`) requires them in its method signatures, and an implementor must reference the parameter types to compile.

- **Ruling**: **ACCEPTABLE — typed-boundary exception confirmed.**

  This is structurally identical to `ExchangeServiceRegistration.AddExchange<TOptions, TMapper>` using a `TMapper` generic parameter to avoid naming DeltaMapper in Http — the model types thread through as transparent signatures. The `using CryptoExchanges.Net.Core.Models` is an unavoidable consequence of implementing `IStreamClient`, which is itself defined in Core and already uses those types in its contract. Http has no architectural obligation to be unaware of Core types; the K1 constraint targets model *construction and projection* (i.e., DeltaMapper and DTO→model mapping logic), not interface-type references. This usage passes K1.

---

## Findings

| # | File | Line | Severity | Confidence | Status | Description |
|---|------|------|----------|------------|--------|-------------|
| 1 | `StreamServiceRegistration.cs` | 94 | MEDIUM | 72 | CONCERN | Engine constructed directly in DI factory; CA2000 suppressed without the ownership-transfer guard present in `StreamClientFactory.Create` |
| 2 | `StreamServiceRegistration.cs` | 87-90 | LOW | 60 | CONCERN | TryAdd sentinel for keyed ISymbolMapper throws at resolution time, not at build time; `ValidateOnStart` does not catch it |
| 3 | `ClientWebSocketConnection.cs` | 42-43 | LOW | 55 | CONCERN | SendPingAsync and SendPongAsync use `WebSocketMessageType.Binary` for both; RFC 6455 distinguishes Ping/Pong control frames from Binary data frames |

---

## Detailed Findings

### Finding 1 — Engine lifetime on exception path in DI factory (CONCERN, non-blocking)

**Severity**: MEDIUM
**Confidence**: 72/100 (below blocking threshold)
**File**: `src/CryptoExchanges.Net.Http/Streaming/StreamServiceRegistration.cs:94-112`
**Category**: Architecture — Captive dependency / resource safety
**Verdict**: CONCERN (non-blocking)

**Issue**: The DI factory lambda (line 94) constructs a `StreamEngine` and immediately passes it to `new StreamClient(...)`. If `StreamClient`'s constructor throws (e.g., argument validation), the engine is leaked — there is no `try/catch` with `DisposeAsync` around this path. The container-free `StreamClientFactory.Create` has this guard (lines 78-88 of `StreamClientFactory.cs`), but the DI factory does not mirror it.

In practice `StreamClient`'s constructor is unlikely to throw after both `engine` and `symbolMapper` are non-null, but the pattern asymmetry is a latent defect. The DECISION-STREAMING-SHARED ruling requires DI path and container-free path to compose with "equivalent composition" (§4, DI path description).

**Fix**: Wrap the `new StreamClient(engine, symbolMapper, exchangeId)` call in the DI lambda with the same `try/catch` pattern from `StreamClientFactory.Create:81-87`:
```csharp
var engine = new StreamEngine(protocol, decoders, engineOpts, connFactory, logger);
try
{
    return new StreamClient(engine, symbolMapper, exchangeId);
}
catch
{
    engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
    throw;
}
```

**Pattern reference**: `src/CryptoExchanges.Net.Http/Streaming/StreamClientFactory.cs:77-88`

---

### Finding 2 — Sentinel TryAdd does not integrate with ValidateOnStart (CONCERN, non-blocking)

**Severity**: LOW
**Confidence**: 60/100
**File**: `src/CryptoExchanges.Net.Http/Streaming/StreamServiceRegistration.cs:87-90`
**Category**: Architecture — DI correctness
**Verdict**: CONCERN (non-blocking)

**Issue**: The `TryAddKeyedSingleton<ISymbolMapper>` fallback throws `InvalidOperationException` at *resolution time*, not at startup. `ValidateOnStart` only validates `IOptions<T>` instances with registered `IValidateOptions<T>` — it does not probe keyed singletons. If a consumer calls `AddXxxStreams` without first calling `AddXxxExchange`, the error surfaces at first use (subscribe), not during `BuildServiceProvider` / startup. The comment at line 84-86 says "TryAdd ensures the exchange's existing keyed mapper is reused (not replaced)" — correct — but the error experience is runtime-deferred.

This is a developer-experience concern for misconfigured hosts, not a correctness defect at runtime (a correctly configured app always registers the mapper first via `AddXxxExchange`).

**Fix (optional)**: Consider adding a startup validation step (e.g., by registering an `IHostedService` or `IStartupFilter` that probes the keyed service, or by documenting the ordering constraint explicitly in the XML doc). Alternatively, accept the runtime-deferred error as consistent with how keyed singletons behave elsewhere in the project (e.g., `ExchangeServiceRegistration` similarly defers keyed-mapper construction to first resolve). Either is acceptable.

**Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:87-91`

---

### Finding 3 — Ping/Pong sent as Binary data frames, not WebSocket control frames (CONCERN, non-blocking)

**Severity**: LOW
**Confidence**: 55/100
**File**: `src/CryptoExchanges.Net.Http/Streaming/ClientWebSocketConnection.cs:42-43` (SendPingAsync) and `45-46` (SendPongAsync)
**Category**: Architecture — Transport correctness
**Verdict**: CONCERN (non-blocking)

**Issue**: `SendPingAsync` and `SendPongAsync` both call `_ws.SendAsync(..., WebSocketMessageType.Binary, ...)`. RFC 6455 defines Ping (opcode `0x9`) and Pong (opcode `0xA`) as distinct control frame types; `Binary` is data opcode `0x2`. `ClientWebSocket` does not expose `Ping`/`Pong` opcodes on the managed `SendAsync` overload that takes `WebSocketMessageType` — the system responds to server Ping frames automatically with a Pong control frame when using `ReceiveAsync`. For venues that expect a JSON or text heartbeat payload (Bitget's `{"op":"pong"}`), the `PingFormat.Json`/`PingFormat.Text` branch in the engine correctly routes to `SendTextAsync`, not `SendPongAsync`. The concern is whether any venue expects a true WebSocket-control Ping (opcode 0x9), which the current implementation cannot send; however, all venues in scope appear to use JSON/text heartbeats for the client-ping direction, and the `ServerPingClientPong` direction relies on the OS-level auto-pong. The impact is limited to any future venue that requires `WebSocketMessageType.Ping`, which the `ClientWebSocket` managed API cannot produce anyway.

**Fix (optional)**: Document in the XML summary of `ClientWebSocketConnection` that true WebSocket control-frame Ping/Pong opcodes are not available via the managed `ClientWebSocket` API, and that `SendPingAsync`/`SendPongAsync` send Binary data frames. Callers (the engine) that use `PingFormat.Text`/`PingFormat.Json` should route to `SendTextAsync`, not `SendPingAsync`. Audit the engine's heartbeat dispatch path to confirm it does not call `SendPingAsync` for text-format venues.

**Pattern reference**: `src/CryptoExchanges.Net.Http/Streaming/HeartbeatPolicy.cs` (PingFormat enum values)

---

## Layering / Conformance Checks

**Dependency direction (Core → Http)**: `StreamServiceRegistration`, `StreamClientFactory`, `StreamClient`, `StreamDecoderRegistry`, and `ClientWebSocketConnection` all reside in `CryptoExchanges.Net.Http.Streaming` and reference only `CryptoExchanges.Net.Core.*`. No Http file references any exchange assembly or DI aggregation package. PASS.

**Factory mirrors ExchangeClientFactory grain**: `StreamClientFactory` is `internal sealed`, primary-constructor form, holds `IServiceProvider`, implements `IStreamClientFactory` with `Available` (lazy), `GetClient` (throws `ExchangeNotRegisteredException`), `TryGet` (out pattern), and a `static Create(...)` container-free factory. Structural match is exact. PASS.

**Registration mirrors ExchangeServiceRegistration grain**: `StreamServiceRegistration` is `internal static`, `AddStreams<TOptions>` registers `IStreamClientFactory` via `TryAddSingleton`, registers `IOptions<TOptions>` with `ValidateOnStart`, resolves keyed `ISymbolMapper` via the existing registration, and registers `IStreamClient` as a keyed singleton. Grain matches. PASS.

**`ClientWebSocketConnection` belongs in Http**: Transport layer; correct per DECISION-STREAMING-SHARED §1. PASS.

**`StreamDecoderRegistry` holds opaque `Func<ReadOnlyMemory<byte>, object>`**: Verified — no Core.Models types in the registry itself. PASS.

**No per-exchange class shipped**: Confirmed — no `BinanceStreamClient`, `BybitStreamClient`, etc. in the diff. PASS.

**`IStreamProtocol` injected, not static**: `IStreamProtocol` and `StreamDecoderRegistry` are injected via constructor/factory. The only `static class` in this diff is `StreamServiceRegistration` — DI-glue extension method body, explicitly permitted under Inv 11. PASS.

**Captive dependency (Inv 9)**: The keyed `IStreamClient` singleton owns its `StreamEngine` directly (engine constructed in-factory, not resolved from DI as a transient). The engine's connection factory (`() => new ClientWebSocketConnection()`) produces a fresh socket per connect attempt. No typed/transient transport captured in singleton. PASS.

**K2 (replay correctness)**: `StreamEngine` (not in this diff but reviewed previously) maintains a subscribe-replay set. `StreamClient` delegates to `_engine.SubscribeAsync` — the engine manages replay. No evidence of regression. PASS.

**K3 (reconnect not via Polly)**: `StreamEngine` owns its own backoff loop. No Polly dependency visible in the streaming files. PASS.

**`ValidateOnStart` present**: Line 80 — `services.AddOptions<TOptions>()...ValidateOnStart()`. PASS.

**Build**: `dotnet build` with `TreatWarningsAsErrors=true` — 0 errors, 0 warnings. PASS.

---

## Summary

The implementation correctly executes the locked DECISION-STREAMING-SHARED design. The K1 adjudication is **ACCEPTABLE**: `using CryptoExchanges.Net.Core.Models` in `StreamClient.cs` is an unavoidable consequence of implementing `IStreamClient` (a Core interface whose method signatures use Core.Models types); no decode, no DeltaMapper, and no model construction exists in `StreamClient` or anywhere else in the Http streaming layer. All five structural checks (factory grain, registration grain, transport placement, opaque decode registry, no per-exchange class) pass. The three non-blocking CONCERNs are: (1) a minor resource-safety asymmetry between the DI factory and the container-free `Create` path on exception, (2) a developer-experience gap in startup validation for the keyed `ISymbolMapper` ordering constraint, and (3) a documentation gap around `SendPingAsync`/`SendPongAsync` sending Binary data frames rather than WebSocket control frames. None meet the blocking threshold (confidence < 80 or severity LOW).
