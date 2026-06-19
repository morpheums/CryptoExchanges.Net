# Consolidated Architecture Review — FEAT-005 WebSocket Streaming

Branch: feat/FEAT-005-websocket-streaming (main..HEAD, tasks 042-048)
Scope: full branch diff, single consolidated gate (replaces per-task boards).

## Verdict: CHANGES_REQUESTED

One HIGH blocking defect (routing-key contract violation that breaks live Binance
data delivery) plus one MEDIUM correctness concern (server-ping liveness watchdog).
All architectural invariants (K1/K3, layering, shared-generic client, DI, package
coupling) PASS.

---

## Finding 1 — Binance Classify routing key never matches engine subscription key
- **Severity**: HIGH
- **Confidence**: 92
- **File**: src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs:71 (Classify) vs
  src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:817-829 (BuildRoutingKey) and :384-398 (pump lookup)
- **Category**: Architecture / Correctness
- **Verdict**: REJECT (blocking)
- **Issue**: The engine STORES subscriptions under `BuildRoutingKey(request)` which yields an
  UPPERCASE canonical key: `{WireSymbol}@{KIND}` where KIND is the enum name upper-cased —
  e.g. `BTCUSDT@TICKER`, `BTCUSDT@ORDERBOOK/20`, `BTCUSDT@KLINE/OneMinute`. The pump looks up
  `_subscriptions.TryGetValue(classified.RoutingKey, ...)` on a default (ordinal) ConcurrentDictionary.
  `BinanceStreamProtocol.Classify` returns the venue `stream` field verbatim — lowercase native
  tokens: `btcusdt@ticker`, `btcusdt@depth20`, `btcusdt@kline_1m`. These never match. In production
  every Binance data frame falls into the `else` branch (StreamEngine.cs:397) and is discarded with
  "No subscription for routing key 'btcusdt@ticker'". No data is ever delivered to a callback.
  `BuildRoutingKey`'s own XML doc (StreamEngine.cs:811-816) states the key "must match what
  IStreamProtocol.Classify returns" — the Binance protocol violates this contract.
- **Why tests are green / why this slipped**: The engine suite drives `FakeStreamProtocol`
  (tests/.../Http.Tests.Unit/Streaming/FakeStreamProtocol.cs:29-38), whose `BuildSubscribe` is
  `StreamEngine.BuildRoutingKey(request)` and whose `Classify` returns the SAME `NextRoutingKey`
  (default `BTCUSDT@TICKER`). The fake echoes the engine's own key, so routing matches in tests.
  `BinanceStreamProtocolTests.Classify_*` asserts the real output `btcusdt@ticker` (line 35) in
  ISOLATION. Nothing wires real `Classify` output through the engine lookup, so the mismatch is
  invisible to the suite. This is a genuine end-to-end gap, not a test artifact.
- **Fix**: Make the routing keyspace single-sourced. Recommended: the protocol owns the routing-key
  convention on BOTH sides — add a `BuildRoutingKey(StreamRequest)` to `IStreamProtocol` (so subscribe
  side and classify side are produced by the same venue code), OR have `BinanceStreamProtocol.Classify`
  return the canonical key the engine expects, OR normalize on both sides (e.g. case-insensitive
  comparer + a shared token format). Do NOT just lowercase one side — `@depth20` vs `@ORDERBOOK/20`
  and `@kline_1m` vs `@KLINE/OneMinute` differ structurally, not only in case. Add a test that feeds a
  real `BinanceStreamProtocol.Classify` result into the engine subscription map (or asserts
  `Classify(realFrame).RoutingKey == BuildRoutingKey(theMatchingRequest)`).
- **Pattern reference**: StreamEngine.cs:811-816 (the contract doc that is being violated);
  FakeStreamProtocol.cs:30 (how the fake keeps both sides identical — production must do the same).

## Finding 2 — Server-ping liveness watchdog only reset by Pong frames, not by data frames
- **Severity**: MEDIUM
- **Confidence**: 70
- **File**: src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:375-377 (only FrameKind.Pong sets
  `_livenessFlag`), :664-685 (ServerPingClientPong path -> WatchdogAsync)
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking)
- **Issue**: For `HeartbeatDirection.ServerPingClientPong` (Binance), the venue heartbeat is a
  WebSocket control-frame Ping, which `ClientWebSocket` auto-replies to and never surfaces as a
  received message — so the engine never sees a `FrameKind.Pong` and `_livenessFlag` is only ever set
  at connect (StartHeartbeat:584). The watchdog (Binance Timeout = 60s) then exchanges the flag to 0
  and, on the next 60s tick with no Pong, forces a reconnect (:677-682) even on a perfectly healthy
  socket that is actively delivering ticker/trade frames. Liveness should be proven by ANY received
  frame, not only Pong. Active high-frequency streams may mask this in practice, but a quiet stream
  (or a quiet window) would churn reconnects. Note: a received WS control Ping does NOT pass through
  `ReceiveAsync` here (ClientWebSocketConnection.ReceiveAsync only returns Text/Binary/Close), so the
  watchdog cannot observe venue liveness at all in the auto-pong model.
- **Fix**: Reset `_livenessFlag` on every successful `ReceiveAsync` (any Data/Ack/Error frame), not
  only on Pong. Set the flag in the pump right after a non-null frame is received (around
  StreamEngine.cs:358), before classification. Add a test: a `ServerPingClientPong` policy with a
  short Timeout that keeps receiving Data frames must NOT trigger a reconnect.
- **Pattern reference**: StreamEngine.cs:376 (current Pong-only reset).

---

## Invariants verified — PASS

- **K1 (engine model-free)** — PASS (confidence 95). Grep of src/.../Http/Streaming shows
  `Core.Models`/`DeltaMapper` appear ONLY in comments/XML-doc `<see>` refs in StreamEngine.cs:35,
  StreamServiceRegistration.cs:17, StreamClientFactory.cs:56. No DeltaMapper type or decode logic in
  Http. `StreamClient.cs` uses `Core.Models` solely as transparent generic params/casts at the typed
  boundary (pre-adjudicated OK) — confirmed: no decode, no mapping there.
- **C1 (heartbeat data vs execution)** — PASS. `BinanceStreamProtocol`/`IStreamProtocol` carry only
  `HeartbeatPolicy` data + Classify; all timers/watchdog/send live in the engine
  (StreamEngine.cs:579-685). (See Finding 2 for a correctness nuance in the execution.)
- **K3 (engine backoff, not Polly)** — PASS. Reconnect uses `BackoffSchedule` (own jittered exponential,
  System.Security.Cryptography only). "Polly" appears only in NOT-Polly comments. REST resilience
  pipeline untouched.
- **Shared-generic client** — PASS. One generic `StreamClient`; no per-exchange client subclass.
  Per-exchange footprint = protocol + decoders + options + `AddBinanceStreams`. Matches
  DECISION-STREAMING-SHARED.
- **Layering** — PASS. Core streaming abstractions reference nothing in Http/Binance/DeltaMapper.
  Http.csproj references Core only. Binance owns DTO+decode+ISymbolMapper->Core.Models. REST
  `IExchangeClient` untouched.
- **DI / no captive dependency (Inv 9)** — PASS. Keyed singletons, `ValidateOnStart`, fresh
  `IWebSocketConnection` per connect via connection factory (StreamServiceRegistration.cs:94-112);
  reuses keyed `ISymbolMapper`/`IMapper` from `AddBinanceExchange` (StreamServiceCollectionExtensions.cs:36-40).
- **Package coupling (Inv 10)** — PASS. `AddBinanceStreams` lives in the Binance assembly; opt-in;
  REST-only consumers pay nothing. No aggregation package forced to reference streaming.
- **Async correctness (engine spot-check)** — PASS overall. Pump/consumer lifecycle sound; disposal
  awaits pump+heartbeat+channels; bounded channel DropOldest with `OnLagged` (StreamSubscriptionChannel);
  callback exceptions isolated (CA1031-guarded, logged) in both pump and consumer; concurrent-reconnect
  guard via Interlocked.CompareExchange (:418).
- **Binance protocol correctness** — PARTIAL: subscribe/unsubscribe tokens (@ticker/@trade/@depthN/
  @kline_<wire>) and Classify routing of combined-stream `stream`/`result`-ack/`code+msg`-error are
  individually correct; decoders map to the right Core.Models. BUT the Classify routing key does not
  reconcile with the engine subscription map — see Finding 1 (blocking).
- **No competitor / roadmap leakage** — PASS. docs/streaming.md clean. README hits (Bybit/OKX/Bitget
  "Supported", Coinbase/Kraken/KuCoin "Coming soon") are pre-existing first-party exchange listings,
  not introduced by this diff and not competitor/strategy leakage.

---

## Milestone architecture note (streaming pattern as it will be cloned next)
- The per-exchange streaming footprint (protocol + decoders + options + AddXxxStreams) is small and
  clean; cloning for the next venue is low-cost and does NOT touch a shared per-exchange-edited file.
- Recommendation: bake the routing-key reconciliation (Finding 1 fix) into the SHARED contract — e.g.
  hoist routing-key construction into `IStreamProtocol` — BEFORE a second exchange clones the pattern,
  so the next venue cannot reintroduce the same subscribe-vs-classify key drift. Fixing it once at the
  seam is materially cheaper than fixing it N times.

## Required before merge
1. Fix Finding 1 (routing-key contract) + add a real-Classify-through-engine routing test. (blocking)
2. Address Finding 2 (liveness reset on any frame) or document why Binance's frame cadence makes it
   moot; add a watchdog-with-data-traffic test. (recommended; promote to blocking if streams can be quiet)
