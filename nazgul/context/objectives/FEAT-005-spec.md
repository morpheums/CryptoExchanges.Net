# FEAT-005 — WebSocket Streaming v1 (Binance-first)

## Objective
Add real-time WebSocket market-data streaming: live ticker, trade, raw order-book, and kline updates feeding the existing canonical `Core.Models`, with auto-reconnect + auto-resubscribe. Built Binance-first on a shared, generic streaming engine so later exchanges add only a small protocol+decode seam.

## Objective type
Feature (new transport capability; net-new interfaces, no change to existing REST surface).

## Authoritative design (already locked — read before planning)
- Full design: `docs/superpowers/specs/2026-06-19-websocket-streaming-v1-design.md` (local).
- Architect rulings (committed): `nazgul/reviews/DESIGN-STREAMING-V1/architect-reviewer.md`, `nazgul/reviews/DECISION-STREAMING-SHARED/architect-reviewer.md`, `nazgul/reviews/DECISION-WEBSOCKET-API/architect-reviewer.md`.

## Scope — In (v1)
- Four PUBLIC streams: ticker, trade, raw order-book updates, klines.
- One exchange: **Binance** (establishes the shared engine + per-exchange seam).
- Auto-reconnect (engine backoff) + auto-resubscribe (replay stored subscribe set), invisible to the consumer.
- Callback API delivering `Core.Models` directly.

## Scope — Out (deferred)
- Order-book maintenance (synchronized local book) → v1.1, as a separate additive interface (no reserved members in v1).
- Private/authenticated streams → later (reuse signing seam).
- Other exchanges (Bybit/OKX/Bitget streaming) → follow-on (clone the seam).
- `IAsyncEnumerable` surface (later, non-breaking). No System.Reactive.

## Architecture (locked)
- **Core** — streaming abstractions over `Core.Models` only: `IStreamClient`, `IStreamSubscription` (`State : StreamConnectionState`), `StreamHandlers<T>`, `StreamLag`, `IStreamClientFactory`.
- **Http** — exchange-agnostic reconnecting byte-engine (connect/backoff/pump/replay/idle-close), ONE generic `internal sealed StreamClient : IStreamClient` (injected `IStreamProtocol` + decode registry + transport + options), `StreamClientFactory`, `StreamServiceRegistration.AddStreams<TOptions>`. **No `Core.Models`/DeltaMapper here (K1).**
- **Binance package** — `BinanceStreamProtocol : IStreamProtocol` (endpoint, BuildSubscribe/Unsubscribe, `Classify→(FrameKind,RoutingKey)`, rich `HeartbeatPolicy`), per-stream decode closures (DTO + existing DeltaMapper profile + `ISymbolMapper` → `Core.Models`), `BinanceStreamOptions`, ~5-line `AddBinanceStreams()`. No per-exchange stream-client class (optional thin `CreateStreams` construction-glue static permitted).
- Delivery: one receive-pump per socket → per-subscription bounded `DropOldest` channel → consumer invokes `OnUpdate`; per-subscription FIFO; callback exceptions isolated+logged; backpressure raises `OnLagged`. Lifecycle via `State` + awaitable callbacks (no events).
- Connection: one multiplexed `ClientWebSocket` per exchange; lazy-open, keep-warm while ≥1 sub, idle-close after window. Reconnect = engine backoff + replay subscribe set (NOT Polly).

## Binding constraints (architect)
- **C1** protocol describes heartbeat (data + `Classify`); engine executes it (timers/watchdog/send) — no timers/threads in the protocol.
- **K1** no `Core.Models`/DeltaMapper under `src/CryptoExchanges.Net.Http/`; engine handles `byte`/`object`/opaque `Func` only.
- **K2** reconnect replays the stored subscribe set; `BuildUnsubscribe` removes from it.
- **K3** socket reconnect is engine backoff, separate from the REST Polly pipeline (retry stays REST-GET-only).
- `IStreamProtocol` + decode registry are injected `internal` types (Inv 11), never static-with-behavior.

## Success criteria
- `IStreamClient` delivers live ticker/trade/order-book/kline `Core.Models` for Binance over a real socket; auto-reconnect + auto-resubscribe verified.
- Shared engine/client/factory are exchange-agnostic; Binance contributes only protocol+decode+options+registration.
- Build 0W/0E (TreatWarningsAsErrors); existing 499 tests stay green; new unit tests via an injected fake transport (no network); one live integration smoke (self-skips without connectivity).
- No `Core.Models`/DeltaMapper references under Http.

## Build approach
Vertical slice, TDD: Core abstractions → Http byte-engine + `IStreamProtocol`/`HeartbeatPolicy`/`StreamFrame` contracts + fake-transport test seam → generic `StreamClient` + factory + `AddStreams<TOptions>` → Binance protocol + 4 decoders + options + `AddBinanceStreams` → the 4 public subscribe methods end-to-end → live integration smoke.
