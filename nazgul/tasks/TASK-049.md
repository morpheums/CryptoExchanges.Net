---
id: TASK-049
status: IMPLEMENTED
commit: 5195052
claimed_at: 2026-06-19
base_sha: 72dc25f
---

# TASK-049: Fix streaming routing-key keyspace mismatch + liveness reset (consolidated-review)

**Status**: IMPLEMENTED

**Blast radius**: MEDIUM — corrects a real defect where live Binance frames are dropped; touches the engine routing-key source + Binance protocol + a test. No public API change.

## Findings (consolidated review)
1. **BLOCKING** — the engine registers subscriptions under a canonical routing key (e.g. `BTCUSDT@TICKER`) but `BinanceStreamProtocol.Classify` returns the venue-native key (`btcusdt@ticker`); they never match → every live data frame is discarded. The fake protocol echoed the canonical key, masking it.
2. **non-blocking** — server-ping liveness flag only resets on `FrameKind.Pong`, which never surfaces under `ClientWebSocket` auto-pong; reset liveness on ANY received frame.

## Fix
- Single-source the routing keyspace: add `string RoutingKeyFor(StreamRequest)` to `IStreamProtocol`; the engine uses `protocol.RoutingKeyFor(req)` to REGISTER a subscription and `Classify(frame).RoutingKey` to LOOK IT UP — same (venue-native) keyspace on both sides. `BinanceStreamProtocol` produces matching keys on both paths.
- Update `FakeStreamProtocol` to implement `RoutingKeyFor` consistently; add a real-`Classify`-through-engine routing test (frame classified with the venue key reaches the subscription registered via `RoutingKeyFor`) so the bug cannot recur.
- Reset the liveness watchdog on any received frame, not only `Pong`.

## Acceptance
- Build 0W/0E; new routing test proves end-to-end frame delivery with the venue keyspace; full suite green; K1/C1/K3 still hold; no competitor names.

## Implementation Log

### Files changed
- `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs` — added `RoutingKeyFor(StreamRequest)` method with full XML doc explaining the single-source keyspace contract
- `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs` — `SubscribeAsync` now calls `_protocol.RoutingKeyFor(request)` instead of `BuildRoutingKey(request)`; pump resets `_livenessFlag` on any received frame (before classification); `BuildRoutingKey` doc updated to reflect it's no longer on the hot path
- `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs` — implemented `RoutingKeyFor` delegating to `BuildStreamToken` (same venue-native lowercase tokens that `Classify` reads from the `"stream"` field)
- `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeStreamProtocol.cs` — implemented `RoutingKeyFor` returning `NextRoutingKey` (same value `Classify` returns); updated default to `"btcusdt@ticker"` (venue-style); `BuildSubscribe`/`BuildUnsubscribe` now use `NextRoutingKey` directly
- `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs` — added `Engine_RoutingKey_VenueNativeKeyspace_FrameReachesSubscription` (routing regression) and `Engine_Watchdog_DoesNotTriggerReconnect_WhenDataFramesArriveRegularly` (liveness regression); fixed existing tests to use venue-style routing keys
- `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamClientTests.cs` — removed stale `protocol.NextRoutingKey = StreamEngine.BuildRoutingKey(...)` setup lines (no longer needed; single-sub tests work with any consistent `NextRoutingKey`)
- `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamProtocolTests.cs` — added four `RoutingKeyFor_*_MatchesClassifyRoutingKey` contract tests (Ticker, Trade, OrderBook, Kline) proving keyspace alignment

### Test results
- Build: 0W/0E
- Http.Tests.Unit: 78 passed (was 76; +2 new regression tests)
- Binance.Tests.Unit: 21 passed (was 17; +4 new RoutingKeyFor contract tests)
- Full non-integration suite: all passed

### K1 result
Engine (`StreamEngine.cs`, `IStreamProtocol.cs`) references no `Core.Models` or `DeltaMapper`. CLEAN.

## Commits

- `5195052` feat(FEAT-005): fix streaming routing-key keyspace mismatch + liveness reset (TASK-049)
