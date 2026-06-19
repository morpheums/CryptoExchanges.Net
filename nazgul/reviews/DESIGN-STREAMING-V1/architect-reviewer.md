# Architect Review — WebSocket Streaming v1 (Design Validation)

- **Type**: Decision/validation request (locked-design ruling), NOT a code diff review.
- **Reviewer**: architect-reviewer
- **Date**: 2026-06-19
- **Scope**: Stress-test and refine the concrete architecture for the streaming feature; issue a final locked design. Grounded in the live codebase (Core interfaces/models, Http engine/registration, OKX per-exchange package, ADR-001). Generic terms only (committed artifact — no competitor names/types).

## Verdict summary

**APPROVED with five binding refinements.** The proposed architecture faithfully extends the established REST seam/composer/factory/per-exchange-DI patterns into streaming with no layering, public-surface, package-coupling, or DIP violations. Refinements R1–R5 tighten lifecycle, mapping boundary, and connection ownership. One forward-looking non-blocking CONCERN raised on N-copy duplication (same trigger that surfaced ADR-001).

## Binding refinements (R1–R5)

- **R1 — Lifecycle = status enum + awaitable callbacks, NOT `event Action`.** `event Action Reconnecting/Reconnected` rejected: async-handler exception-swallow, mutable shared state touched from the pump thread, and inconsistency with the `Func<T,ValueTask>` data idiom. Lock `IStreamSubscription.State : StreamConnectionState { Connecting, Live, Reconnecting, Closed }` (source of truth), `IsConnected => State == Live` convenience. Lifecycle delivered via optional awaitable callbacks (R2), run through the same isolation wrapper as data.
- **R2 — Collapse per-stream lifecycle args into one `StreamHandlers<T>` record** (`OnUpdate`, optional `OnReconnecting`, `OnReconnected`, `OnLagged`). Keeps four subscribe signatures lean; bare-`Func<T,ValueTask>` convenience overload allowed (non-breaking).
- **R3 — Model-agnostic engine + exchange-side decode.** Http engine routes bytes only; DTO deserialize + DeltaMapper -> `Core.Models` happens in the EXCHANGE package via a registered decode closure. Any DeltaMapper / `Core.Models` projection under `src/CryptoExchanges.Net.Http/` is an invariant-2 REJECT. `IStreamProtocol` is an injected `internal sealed` interface (invariant 11 / ADR conv. 8), never a `static class`.
- **R4 — `Lagged` is a handle signal via `OnLagged(StreamLag)`,** never injected into the data `T` stream and never thrown. DropOldest evicts; pump tracks per-subscription dropped-count and raises `OnLagged`.
- **R5 — Socket lifetime = lazy-open, keep-warm while >=1 sub, idle-close after configurable window.** Not close-on-last-sub (thrash + venue connection-rate risk). Reconnect = engine backoff + replay stored subscribe frames (NOT Polly; Polly retry stays REST GET-only per invariant 8). Bounded channel is NOT torn down across reconnect; no gap-fill guarantee in v1 (`OrderBook.LastUpdateId` exposes sequence for consumer-side gap detection).

## Invariant conformance (validated)

- **Inv 1 (Core has no exchange/transport knowledge)**: PASS — Core gains only abstractions over its own `Core.Models`; `ClientWebSocket` lives in Http.
- **Inv 2 (Http exchange-agnostic)**: PASS — engine is byte/route only; decode+map exchange-side (R3).
- **Inv 3 (internals stay internal)**: PASS — only `XxxStreamClient` public; engine/strategy/composer/decode internal.
- **Inv 4 (no mutation of public REST interfaces)**: PASS — `IStreamClient` is NEW; `IExchangeClient`/`IMarketDataService` untouched.
- **Inv 5 (interfaces as extension points, no new members)**: PASS — new capability = new interface.
- **Inv 6 (DeltaMapper for DTO->model)**: PASS — reuse per-exchange `XxxResponseProfile`; `ISymbolMapper` stays bespoke, resolves wire symbol before the engine.
- **Inv 8 (retry GET-only)**: PASS — socket reconnect is engine backoff, separate from the resilience pipeline.
- **Inv 9 (no captive dependency)**: PASS — long-lived transport owned inside the keyed singleton; DI path `ownsTransport:false`.
- **Inv 10 (package coupling)**: PASS — `AddXxxStreams` in exchange assembly; `StreamClientFactory` + `StreamServiceRegistration` in Http; aggregation `AddCryptoExchangeStreams` thin opt-in delegator.
- **Inv 11 (interfaces over static for swappable behavior)**: PASS — `IStreamProtocol` injected interface.

## CONCERN (non-blocking, confidence 60/100)

Replicating the per-exchange composer + `AddXxxStreams` will produce N near-identical copies, exactly the latent DRY/OCP smell ADR-001 addressed for REST. The `StreamServiceRegistration.AddStreams<TOptions>` shared-helper + composer-shape mandate (HR-S3/S4) keeps it inside the ADR-001-blessed pattern, but recommend a macro-architecture pass at the close of the first multi-exchange streaming milestone (same trigger that surfaced ADR-001). Do NOT block design work on this; name it now so it is caught cheaply.

## House rules + component list

House rules HR-S1..HR-S8 and the Core/Http/per-exchange/aggregation/testing component list are recorded in the full ruling delivered to the requester (transcript). Key locks: Core owns streaming abstractions only; Http owns engine+transport+factory+registration (no mapping); per-exchange supplies `IStreamProtocol`+decode+composer (`Create`/`ComposeForDi`) reusing keyed `ISymbolMapper`+`IMapper`; per-exchange opt-in DI; lifecycle via status-enum+awaitable-callbacks; one bounded `DropOldest` single-reader/single-writer channel per subscription with per-subscription FIFO and swallowed+logged callback exceptions; keep-warm socket with idle-close and replay-on-reconnect; OrderBook delivered as per-frame snapshots (no Core delta type); public-only in v1.

## Final Verdict
APPROVED
