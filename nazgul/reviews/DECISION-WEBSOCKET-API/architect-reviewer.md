# Architecture Decision — WebSocket Streaming Subscription API

**Type:** DECISION request (not a per-task code review; no diff exists).
**Scope:** Foundational API-shape ruling for upcoming WebSocket streaming support
(ticker/trade/order-book/kline) feeding canonical `Core.Models`, with auto-reconnect +
auto-resubscribe, across Binance/Bybit/OKX/Bitget.

## Conventions verified against the code
- Strict layering Core -> Http -> Exchange -> DI confirmed.
- REST services (`IMarketDataService`/`ITradingService`/`IAccountService`) are async
  `Task<T>` with `CancellationToken ct`; results are direct typed values, NO result/event
  wrapper (no result/event-envelope type). Errors surface as `ExchangeException` hierarchy.
- Canonical typed models (`Ticker`, `Symbol`, etc.) live in Core.
- Zero `ClientWebSocket` / `System.Threading.Channels` / `System.Reactive` usage in `src` today.
- Per-exchange DI lives in each exchange assembly (ADR-001); aggregation package is a thin
  opt-in (`AddCryptoExchanges`) that delegates — never forced on every consumer.
- Composition pattern: single `BinanceClientComposer` with `Create()` + `ComposeForDi()`.

## Ruling (decisive)
1. **API shape:** Callback + `IAsyncDisposable` subscription handle is the canonical surface.
   `Func<T,ValueTask> onUpdate` delivering canonical models directly (NO envelope).
   NOT `IAsyncEnumerable` at launch (reconnect semantics force a control-envelope that
   re-introduces the rejected event-envelope wrapper). NOT Rx (violates zero-Rx identity).
   Channels used INTERNALLY (bounded, drop-oldest + lagged signal). An `IAsyncEnumerable`
   adapter MAY be added later, non-breaking; not part of v1.
2. **Location:** NEW `IStreamClient` in `Core/Interfaces`, implemented per exchange package
   (mirrors REST services). REST `IExchangeClient`/`IMarketDataService` UNTOUCHED (growing
   them = breaking change + Invariant 5 violation). Registered as a SEPARATE keyed singleton;
   opt-in (`AddXxxStreams`), so REST-only consumers pay nothing for socket machinery.
   Generic reconnecting-socket plumbing -> Http (exchange-agnostic, like the resilience
   pipeline); subscribe-message formatting + frame->model mapping -> exchange package
   (reuse DeltaMapper profiles; reuse bespoke `ISymbolMapper`).
3. **Connection:** One multiplexed `ClientWebSocket` per exchange (per connection bucket),
   sharded only when per-socket subscription cap is hit. Auto-reconnect + auto-resubscribe
   live BELOW the public API; reconnect is invisible to the data callback and surfaced as
   `OnReconnecting`/`OnReconnected`/lagged on the HANDLE, never inside `T`. Reuse the
   `long[] _offsetHolder` time-sync seam and the existing signing handler for private streams.
4. **House rule:** captured in the response and reproduced in the spec-ready block below.
5. **Traps flagged:** envelope creep via IAsyncEnumerable; growing existing interfaces;
   layering inversion (no frame parsing in Http); disposal ownership of sockets (stream
   client DOES own its transport — do not blindly copy `ownsHttpClient:false`); ADR-001
   aggregation coupling / opt-in streaming; `async void` callbacks (use `Func<T,ValueTask>`);
   mutable static routing tables (follow `SymbolMapper._wireToSymbol` volatile + atomic-swap);
   no second signing path.

## House-rule statement (spec-ready)
WebSocket streaming is exposed through `IStreamClient` (Core abstraction, implemented per
exchange; REST `IExchangeClient` untouched). Canonical API is callback + `IAsyncDisposable`
handle: `await streams.SubscribeToXxxAsync(symbol, Func<T,ValueTask> onUpdate, ct)`; dispose to
unsubscribe. Callbacks deliver `Core.Models` directly — no event/result envelope. Lifecycle
(reconnecting/reconnected/lagged) is surfaced on the handle, never inside the data type.
Internally: one multiplexed `ClientWebSocket` per exchange (reconnecting + auto-resubscribing
plumbing in Http, exchange-agnostic; subscribe formatting + frame->model via DeltaMapper in the
exchange package), backed by bounded Channels with drop-oldest + lagged signal. No
System.Reactive. An `IAsyncEnumerable<T>` adapter may be added later; not part of v1.

## Final Verdict
APPROVED — Recommendation issued. This is an advisory architecture decision, not a code-change
gate; "APPROVED" denotes the ruling is final and ready to feed the WebSocket objective's spec.
No blocking issues (no diff under review). Recorded for future implementation tasks.
