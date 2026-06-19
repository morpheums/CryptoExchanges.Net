# Architect Ruling — Streaming v1: Shared-Generic StreamClient (DECISION-STREAMING-SHARED)

- **Type**: Decision/ratification request (not a code-diff review). Generic terms only; no competitor/library/person/type names.
- **Reviewer**: architect-reviewer
- **Date**: 2026-06-19
- **Supersedes**: the non-blocking CONCERN in `DESIGN-STREAMING-V1/architect-reviewer.md` (N-copy duplication, "revisit at first multi-exchange milestone"). Resolved NOW, deliberately, as requested.

## Headline verdict

**RATIFIED — shared-generic `StreamClient` in Http, with the per-exchange surface reduced to the irreducible minimum.** My earlier per-exchange-composer lean was correct for REST and is now explicitly overridden for streaming, on the strength of the 4-exchange uniformity evidence. The two are not in conflict: REST services are *semantically* divergent (different endpoints, params, response shapes per call), so a per-exchange service class earns its existence; the streaming client is *mechanically* uniform (subscribe -> route -> decode -> deliver), so the divergence collapses into data (a protocol + a decode table), not behavior. When divergence is data, you inject data, not subclasses. This is the same reasoning that already produced `ExchangeServiceRegistration.AddExchange<TOptions,TMapper>` as a shared body with per-exchange factories — streaming follows that grain, it does not invent a new one.

One binding correction (C1) and three binding constraints (K1-K3) attach to the ratification.

---

## 1. Ratify the shared-generic StreamClient — final ownership

**RATIFIED.** Final ownership lock:

**Http (shared, built once, all `internal sealed` except the factory/registration entry points):**
- The reconnecting byte-engine (connect -> pump -> backoff -> replay-subscribes -> idle-close). Owns `ClientWebSocket` and the per-subscription bounded `DropOldest` channels.
- `StreamClient : IStreamClient` — ONE generic class, constructed with an injected `IStreamProtocol`, a decode registry, the transport, and `TOptions`-derived stream options. No `T`-typed exchange knowledge.
- `StreamClientFactory` (container-free `Create` parity) + generic `StreamServiceRegistration.AddStreams<TOptions>(...)` (DI parity). Mirror the existing `ExchangeClientFactory` / `ExchangeServiceRegistration` split exactly.

**Per-exchange (irreducible minimum):**
- `XxxStreamProtocol : IStreamProtocol` — `internal sealed`, injected (Inv 11 / ADR conv. 8). Never a `static class`.
- The per-stream decode closures (DTO deserialize + reuse the existing keyed `IMapper`/`XxxResponseProfile` -> `Core.Models`). Exchange-side, per K1.
- `XxxStreamOptions`.
- A ~5-line `AddXxxStreams()` in the exchange assembly (Inv 10).

**Residual per-exchange public class:** **None required, and that is the recommendation.** Do NOT ship a per-exchange `XxxStreamClient` public type. The public streaming surface is `IStreamClient` + `IStreamSubscription` + the handler/option records in Core/Http; the consumer obtains an `IStreamClient` via the factory or keyed DI resolution, exactly as they obtain `IExchangeClient`. An optional thin `Xxx.CreateStreams(...) -> IStreamClient` static *convenience* wrapper is **permitted** (DI-glue/pure-construction, the explicitly-allowed `static` carve-out in Inv 11), but it must return the shared `IStreamClient` and add zero behavior. If it ever grows a branch, it has become behavior and must become an injected type. **Lock: no per-exchange streaming class carries behavior.**

Structural win over REST: REST has `BinanceExchangeClient`/`OkxExchangeClient`/etc. as public per-exchange types because they compose divergent services; streaming has exactly one public client type for all exchanges.

---

## 2. Final `IStreamProtocol` surface

The protocol is the *entire* per-exchange behavioral seam. It must FULLY own heartbeat (server-pong vs client-ping-json vs client-ping-text are genuinely divergent — a bare interval is insufficient and is rejected) and must CLASSIFY frames so the engine skips control frames generically. Lock these members (semantic contract):

- `string BuildSubscribe(StreamRequest request)` — absorbs flat-topic-string vs object bodies. Returns the wire text to send. (Plus `BuildUnsubscribe` — see K2.)
- `StreamFrame Classify(ReadOnlySpan<byte> frame)` — returns `{ FrameKind Kind, string? RoutingKey }` where `FrameKind in { Data, Ack, Pong, Error }`. Replaces a bare `TryRoute`: the engine acts on `Kind` (skip Ack/Pong, surface Error, route Data by `RoutingKey`) without venue knowledge. RoutingKey is the key into the decode registry (point 3).
- `HeartbeatPolicy Heartbeat { get; }` — a rich value, not an interval. Shape:
  - `Direction in { ServerPingClientPong, ClientPing }`
  - `TimeSpan Interval` (client-ping cadence) and `TimeSpan Timeout` (liveness watchdog before forced reconnect)
  - `ReadOnlyMemory<byte> ClientPingPayload` + `PingFormat in { ControlFrame, Text, Json }` — covers client-ping-text (`"ping"`) and client-ping-json (`{op:ping}`); `ControlFrame`/`ServerPingClientPong` makes payload irrelevant (engine pongs the control frame).
  - Pong recognition: prefer folding into `Classify` returning `FrameKind.Pong` (single frame-inspection entry point); a separate `bool IsPong(...)` is acceptable.

**Binding C1 (correction to the engine/protocol boundary):** heartbeat *send* timing and the liveness watchdog are ENGINE responsibilities driven by `HeartbeatPolicy` *data*; the protocol must not own timers or threads. The protocol describes heartbeat; the engine performs it. Keeps the protocol a pure, trivially-testable data+classification object and keeps all timing/lifecycle in the engine. Do not let the protocol acquire a `StartHeartbeat()`-style behavioral method.

`IStreamProtocol` is `internal sealed`, injected, no `static`. (Inv 3, 11.)

---

## 3. Decode registry shape (the byte->model boundary stays exchange-side)

Load-bearing invariant (Inv 2/6, R3): **no DeltaMapper or `Core.Models` projection under `src/CryptoExchanges.Net.Http/`.** Lock:

- A decode entry is a closure `ReadOnlyMemory<byte> -> object` (preferred: engine carries target `T`, registry keyed by stream-type returns `T`). The closure does: deserialize venue DTO -> reuse keyed `IMapper` -> `Core.Models.{Ticker,Trade,Candlestick,OrderBook}`. Built in the exchange package, CAPTURED by the engine as an opaque delegate. Http sees `Func<ReadOnlyMemory<byte>, object>` — never `Core.Models`, never DeltaMapper.
- Registration: the per-exchange composer populates a `StreamDecoderRegistry` mapping the protocol's stream-type discriminator (the same token the consumer requests: ticker/trade/orderbook/kline) -> decode closure. Engine looks up by decoded stream-type derived from `RoutingKey`/`StreamRequest`, invokes the closure, delivers `T` to the matching subscription's channel.
- `ISymbolMapper` resolution stays bespoke, exchange-side: wire-symbol resolved before `BuildSubscribe` (outbound); decode closure maps wire->domain symbol (inbound), reusing keyed `ISymbolMapper`. Never via DeltaMapper (Inv 6).

Http holds: protocol (classify/route) + registry of opaque decoders + transport. Model boundary entirely exchange-side. Identical in spirit to `ExchangeServiceRegistration` taking `Func<ISymbolMapper,TMapper>` so Http never references DeltaMapper.

---

## 4. DI / composition — and does this retire the N-copy concern?

Parity with REST:

- **DI path:** `StreamServiceRegistration.AddStreams<TOptions>(services, exchangeId, ..., protocolFactory, decoderRegistryFactory, optionsConfig)` in Http — shared body. Registers `IStreamClient` as a **keyed singleton** by `ExchangeId`, reusing the SAME keyed `ISymbolMapper`/`IMapper` already registered by `AddXxxExchange`. Transport owned inside the singleton (Inv 9, `ownsTransport:false` in DI path per R5). `ValidateOnStart` on `XxxStreamOptions`.
- **`AddXxxStreams()`** in the exchange assembly is the ~5-line delegator supplying `protocolFactory` + `decoderRegistryFactory` (Inv 10 — exchange's own assembly; aggregation `AddCryptoExchangeStreams` is a thin opt-in delegator, never forced).
- **Container-free:** `StreamClientFactory.Create(...)` mirrors `ExchangeClientFactory`, same effective composition as DI.

**Does this retire the N-copy CONCERN? — Substantially YES.** The earlier CONCERN feared N near-identical *composer + client* copies. By making the client generic and pushing divergence into a protocol (data+classification) + a decode table (data), the per-exchange footprint drops to: one protocol class, N decode closures, one options record, one 5-line registration. **No per-exchange client class, no per-exchange composer of substance** — the thing that would have been duplicated N times no longer exists. What remains per-exchange is genuinely irreducible (the wire protocol and DTO shapes ARE the per-exchange differences; cannot be shared without a universal wire format we do not control).

**Residual rule-of-three, named for the record (CONCERN, confidence 55/100, non-blocking, DEFER):** the ~5-line `AddXxxStreams` bodies and the decode-registry *wiring boilerplate* will be structurally similar across 4 exchanges. Same shape as the REST `AddXxxExchange` delegators that ADR-001 already accepted as worth-it thin glue. Below the threshold that justified extraction for REST; does not warrant a further abstraction now. Re-examine ONLY if a 5th exchange or a change to the registration signature forces editing all N delegators in lockstep (the true rule-of-three pain signal). Do not pre-abstract the glue.

---

## 5. Forward hook for v1.1 order-book maintenance

**CONFIRMED non-breaking.** v1 delivers `Core.Models.OrderBook` as per-frame snapshots; `OrderBook.LastUpdateId` (verified present, `long?`) already exposes sequence for consumer-side gap detection. v1.1 maintenance (seq/checksum-driven local book) attaches as a **separate new interface** (e.g. `IOrderBookMaintainer`), since venues differ only in seq/checksum *fields* which live in the venue DTO the decode closure owns. It does NOT reshape `IStreamProtocol`, `IStreamClient`, or any v1 type:
- New behavior = new interface (Inv 5). Do NOT add maintenance members to `IStreamProtocol` in v1 or later — introduce `IOrderBookMaintainer` and resolve it optionally.
- The decode closure already returns the full DTO-derived model, so checksum/seq fields are available exchange-side without an engine change.
- **Lock:** v1 must not add any "reserved for v1.1" member to a v1 interface. The hook is *additive*, not *reserved*. Reserved members are speculative API surface — rejected.

---

## 6. Traps given our layering/invariants

Binding constraints:

- **K1 (Inv 2/6, hard REJECT line):** any `using CryptoExchanges.Net.Core.Models` or DeltaMapper reference under `src/CryptoExchanges.Net.Http/` in the streaming engine/client is a blocking violation. The engine handles `byte`/`object`/opaque `Func` only. Verified: Http source is clean of transport/model-projection types; keep it that way.
- **K2 (replay correctness):** reconnect replays stored subscribe frames (R5). Engine stores the *subscribe wire text* (or `StreamRequest` + re-invoke `BuildSubscribe`) per live subscription and replays on reconnect; `BuildUnsubscribe` removes from the replay set so unsubscribe-then-reconnect does not resurrect a dead stream. Engine concern; protocol only builds frames.
- **K3 (Inv 8, retry stays REST-GET-only):** socket reconnect is the engine's own bounded backoff loop, NOT the Polly resilience pipeline. Do not route reconnect through `ExchangeResiliencePipeline`.
- **C1 (restated):** protocol owns heartbeat *description* (policy data + frame classification); engine owns heartbeat *execution* (timers, watchdog, send). No timers/threads in the protocol.
- **Inv 11 trap:** `XxxStreamProtocol` and the decode registry are injected `internal` types, NOT `static class`es holding behavior. The only permitted `static` is the optional thin `Xxx.CreateStreams` construction-glue wrapper (point 1) and the registration extension methods.
- **Captive-dependency trap (Inv 9):** `IStreamClient` keyed singleton owns a long-lived transport. Do not resolve a transient/typed transport into it. Mirror the named-resource pattern from `ExchangeServiceRegistration.cs:122-127`.

---

## Summary

- **PASS / RATIFIED:** Shared-generic `StreamClient` in Http with protocol+decode-table injection — overrides the earlier per-exchange-composer lean for streaming; justified by mechanical uniformity across all 4 venues and consistent with the existing `AddExchange<TOptions,TMapper>` shared-body grain.
- **PASS:** No per-exchange streaming class required or recommended; optional thin construction-glue static permitted (zero behavior).
- **PASS:** `IStreamProtocol` finalized — `BuildSubscribe`/`BuildUnsubscribe`, `Classify -> (FrameKind, RoutingKey)`, rich `HeartbeatPolicy` (direction + interval + timeout + ping payload/format + pong recognition).
- **PASS:** Decode registry = exchange-side opaque `Func` closures; model/DeltaMapper boundary stays exchange-side (Inv 2/6).
- **PASS:** DI parity locked (keyed singleton, reuse keyed mapper, `ValidateOnStart`, factory parity).
- **PASS:** v1.1 order-book hook attaches as a separate additive interface — confirmed non-breaking; no reserved members in v1.
- **CONCERN (55/100, non-blocking, DEFER):** residual ~5-line `AddXxxStreams` + decode-wiring glue similarity — below rule-of-three, same accepted shape as REST delegators; revisit only if a signature change forces lockstep edits across all N.
- **Binding:** C1 (protocol describes / engine executes heartbeat), K1 (no Core.Models/DeltaMapper in Http), K2 (replay set tied to subscribe/unsubscribe), K3 (reconnect != Polly).

## Final Verdict
**APPROVED** — shared-generic streaming client ratified with correction C1 and constraints K1-K3 binding on implementation.
