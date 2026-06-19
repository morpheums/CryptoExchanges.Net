---
id: TASK-045
status: PLANNED
depends_on: [TASK-044]
---
# TASK-045: Generic `StreamClient` + `StreamClientFactory` + `AddStreams<TOptions>` + decode-registry

## Metadata
- **ID**: TASK-045
- **Group**: 4
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-044
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Http/Streaming/StreamClient.cs, src/CryptoExchanges.Net.Http/Streaming/StreamSubscription.cs, src/CryptoExchanges.Net.Http/Streaming/StreamDecoderRegistry.cs, src/CryptoExchanges.Net.Http/Streaming/StreamClientFactory.cs, src/CryptoExchanges.Net.Http/Streaming/StreamServiceRegistration.cs, tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamClientTests.cs]
- **Wave**: 4
- **Traces to**: FEAT-005 spec §Architecture "Http" (generic StreamClient/factory/registration); design §"Shared-generic client" + §"Decode registry" + §"DI / composition"; DECISION-STREAMING-SHARED §1/§3/§4
- **Created at**: 2026-06-19T17:20:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Wrap the TASK-044 engine in the **one generic** `internal sealed StreamClient : IStreamClient` (no
per-exchange client class — ratified DECISION-STREAMING-SHARED §1), plus the factory, the shared DI
registration body, and the decode-registry plumbing. Mirror the existing
`ExchangeClientFactory` / `ExchangeServiceRegistration` split exactly. All under
`src/CryptoExchanges.Net.Http/Streaming/`. One type per file. K1 still binding — Http sees only
`byte`/`object`/opaque `Func`.

Types to create:

- **`StreamClient : IStreamClient`** (`internal sealed`, generic over no `T` — `T` lives in the decode
  closures, not the client). Constructed with an injected `IStreamProtocol` + a `StreamDecoderRegistry`
  + the transport (`IWebSocketConnection` factory) + `StreamEngineOptions`. Implements the four subscribe
  methods + the bare-`Func` convenience overloads: each resolves the wire symbol/params into a
  `StreamRequest`, registers handlers with the engine, and returns a `StreamSubscription`. The engine
  invokes the decode-registry's opaque `Func<ReadOnlyMemory<byte>, object>` for the routed stream-type
  and delivers the resulting `object` (boxed `Core.Models`) to the matching subscription, which casts to
  `T` inside the consumer-callback wrapper. `ExchangeId` carried as construction data.
- **`StreamSubscription : IStreamSubscription`** (`internal sealed`) — exposes engine `State`;
  `IsConnected => State == Live`; `DisposeAsync` unsubscribes (removes from the engine's replay set, K2).
- **`StreamDecoderRegistry`** — maps the stream-type discriminator (ticker/trade/orderbook/kline — the
  same token the consumer requests and `RoutingKey` resolves to) → `Func<ReadOnlyMemory<byte>, object>`.
  Populated per-exchange (TASK-046); held opaquely by the engine/client (K1 — Http never sees `T`).
- **`StreamClientFactory : IStreamClientFactory`** (`internal sealed`) — resolve keyed `IStreamClient`
  by `ExchangeId`; `Available`/`GetClient`/`TryGet`. Mirror `ExchangeClientFactory` line-for-line. Add a
  container-free `StreamClientFactory.Create(...)` parity entry (mirror the REST container-free path).
- **`StreamServiceRegistration.AddStreams<TOptions>(...)`** (`internal static`) — the shared DI body:
  `AddStreams<TOptions>(services, exchangeId, protocolFactory, decoderRegistryFactory, optionsConfig)`.
  Registers `IStreamClient` as a **keyed singleton** by `ExchangeId`, **reusing the SAME keyed
  `ISymbolMapper`/`IMapper` already registered by `AddXxxExchange`**, transport owned inside the singleton
  (`ownsTransport: false` — no captive dependency, Inv 9), `ValidateOnStart` on `TOptions`. Register
  `IStreamClientFactory → StreamClientFactory` via `TryAddSingleton`. Mirror `ExchangeServiceRegistration.AddExchange<TOptions,TMapper>` grain — Http takes the decoder-registry as a `Func` so it never references DeltaMapper.

Tests (fake transport, no network): a fake `IStreamProtocol` + a trivial decode closure returning a
sentinel `object`; assert `StreamClient` subscribe → engine route → decode → callback delivery; subscription
`State`/`IsConnected`; dispose-unsubscribes; factory `Available`/`GetClient`/`TryGet`/throws-when-missing;
`AddStreams<TOptions>` registers a keyed singleton reusing an existing keyed `ISymbolMapper`/`IMapper` and
honors `ValidateOnStart`.

## Acceptance Criteria
- [ ] One generic `internal sealed StreamClient : IStreamClient` (NO per-exchange client class), `StreamSubscription`, `StreamDecoderRegistry` (opaque `Func<ReadOnlyMemory<byte>,object>`), `StreamClientFactory` (+ container-free `Create`), and `StreamServiceRegistration.AddStreams<TOptions>` (keyed singleton, reuse keyed `ISymbolMapper`/`IMapper`, `ownsTransport:false`, `ValidateOnStart`) — mirroring `ExchangeClientFactory`/`ExchangeServiceRegistration`.
- [ ] `StreamClientTests` (fake protocol + fake transport, no network) pass: subscribe→route→decode→deliver, `State`/dispose-unsubscribe, factory resolution, and DI keyed-singleton registration; solution builds 0W/0E.
- [ ] **K1 verified**: zero `Core.Models`/DeltaMapper references under `src/CryptoExchanges.Net.Http/`; no captive dependency (transport owned inside the keyed singleton); existing 499 tests stay green.

## Pattern Reference
- Factory to mirror line-for-line: `src/CryptoExchanges.Net.Http/ExchangeClientFactory.cs` (full file, keyed-resolution + `Available`/`GetClient`/`TryGet`).
- Shared DI body + keyed-singleton + reuse-keyed-mapper + `ValidateOnStart` + no-captive-dependency: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs` (esp. lines 66-127; named-vs-typed/captive note lines 92-96 and 122-127).
- Container-free composition parity reference: `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs` (`Create` vs `ComposeForDi`, `ownsHttpClient:false` invariant lines 57-66).
- Generic-client + decode-registry mandate: design §"Shared-generic client" (lines 61-62), §"Decode registry" (lines 89-90); DECISION-STREAMING-SHARED §1, §3, §4.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Http/Streaming/StreamClient.cs
- src/CryptoExchanges.Net.Http/Streaming/StreamSubscription.cs
- src/CryptoExchanges.Net.Http/Streaming/StreamDecoderRegistry.cs
- src/CryptoExchanges.Net.Http/Streaming/StreamClientFactory.cs
- src/CryptoExchanges.Net.Http/Streaming/StreamServiceRegistration.cs
- tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamClientTests.cs

**Modifies**:
- none

## Traceability
- **PRD Acceptance Criteria**: n/a — FEAT-005 spec Success criterion "shared engine/client/factory are exchange-agnostic"
- **TRD Component**: generic `StreamClient` + `StreamClientFactory` + `StreamServiceRegistration` (design §"Shared-generic client", §"DI / composition")
- **ADR Reference**: DECISION-STREAMING-SHARED §1 (shared-generic client, no per-exchange class), §3 (opaque-`Func` decode registry, model boundary exchange-side), §4 (DI parity: keyed singleton, reuse keyed mapper, `ValidateOnStart`, factory parity); Inv 9/11; ADR-001 (per-exchange-DI grain)

## Implementation Log

### Attempt 1
<!-- implementer fills this in -->

## Review Results

### Attempt 1
<!-- review-gate fills this in -->
