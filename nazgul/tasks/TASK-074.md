---
id: TASK-074
status: DONE
depends_on: [TASK-073]
claimed_at: 2026-06-24T00:00:00Z
implemented_at: 2026-06-24T00:00:00Z
---
# TASK-074: Fix Binance combined-stream decode (missing `data`-envelope unwrap) + activate the Binance multi-symbol regression test

## Metadata
- **ID**: TASK-074
- **Group**: 4
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-073
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs, tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs, tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDecodeTests.cs]
- **Wave**: 4
- **Traces to**: User-approved scope addition to FEAT-008 — Binance order books never reach the callback because the combined-stream `data` envelope is not unwrapped before decode (separate, pre-existing bug since streaming-v1 PR #26, surfaced while verifying TASK-073).

## Description

The Binance combined-stream pump delivers each frame as the full envelope
`{"stream":"btcusdt@depth20","data":{ ... }}`. `BinanceStreamDecoders` deserializes those raw
envelope bytes **directly** into the leaf DTO (`StreamDepthDto`/`StreamTickerDto`/`StreamTradeDto`/
`StreamKlineDto`), so the DTO fields never populate — the order-book decoder then throws
"Order-book depth frame does not carry a symbol field." Every Binance stream kind is affected.
KuCoin already does this correctly via `DeserializeData<T>` / `DeserializeSnapshotData<T>` helpers in
`KucoinStreamDecoders` that extract the `data` element first. This task mirrors that pattern for
Binance.

This is the second of the two independent causes of the user's "no Binance order book ever delivered"
symptom (the first — the unpaced subscribe burst → PolicyViolation close — is fixed by TASK-071/072).

### Steps
1. In `BinanceStreamDecoders`, add a small private helper (one type per file rule is satisfied — this
   is a private static method on the existing static class, mirroring KuCoin's `DeserializeData<T>`):
   parse the frame with `JsonDocument`, read the `data` property, and deserialize **that** element into
   the leaf DTO with the existing case-sensitive `JsonOpts`. If `data` is absent, fall back to treating
   the whole frame as the payload (defensive — keeps any non-combined raw-stream shape working) OR throw
   a clear decode error; pick the behavior that matches the engine's combined-stream `/stream` endpoint
   (it is ALWAYS combined-stream, so `data` is always present — prefer a clear failure if missing).
2. Route all four decoders (Ticker, Trade, OrderBook, Kline) through the helper so each deserializes the
   unwrapped `data` object. Update the class `<summary>`/`<remarks>` only if now inaccurate.
3. Activate the Binance multi-symbol live regression test added in TASK-073: remove the
   decode-bug self-skip workaround so `OrderBook_MultiSymbol_LiveStream_DeliversAtLeastOneUpdate` runs
   for real and asserts ≥1 book delivered + ≥1 subscription `Live`. Keep the genuine
   endpoint-reachability self-skip (offline → skip), and keep `[Trait("Category","Integration")]`.
4. Add/extend FAST unit decode tests in `BinanceStreamDecodeTests.cs` that feed a realistic combined-
   stream envelope (`{"stream":"btcusdt@depth20","data":{...}}`) through each decoder and assert the
   model is populated (symbol resolved, bids/asks parsed, ticker/trade/kline fields set). These prove
   the unwrap with no network. Confirm the previously-passing tests still pass (they may have been
   feeding `data`-level JSON directly — update them to envelope-level if they were exercising the wrong
   shape, and note it).

## Acceptance Criteria
- [x] `BinanceStreamDecoders` unwraps the combined-stream `data` element before deserializing for all
      four stream kinds; the order-book decoder no longer throws on live combined-stream frames.
- [x] Fast unit tests in `BinanceStreamDecodeTests.cs` feed full combined-stream envelopes and assert
      populated models for ticker/trade/orderbook/kline.
- [x] The Binance `OrderBook_MultiSymbol_LiveStream_DeliversAtLeastOneUpdate` test no longer self-skips
      on the decode path; run live it delivers ≥1 book and ≥1 subscription reaches `Live`.
- [x] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'`
      stays green.
- [x] Live verification reported: run the Binance + KuCoin multi-symbol Integration tests and report
      pass + update counts (or explicit skip reason if a venue is unreachable).

## File Scope
**Modifies**:
- src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs (unwrap `data` for all 4 kinds)
- tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs (activate multi-symbol regression test)
- tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDecodeTests.cs (envelope-level decode unit tests)

**Creates / Deletes**: (none)

## Commits
- **Base SHA**: 840e4fdc00372113bec18df54ca8d44415a3c21f
- **Impl SHA**: da818dbee3f27634ec2cc9983bcbdb61e9197a0e — `feat(FEAT-008): unwrap Binance combined-stream `data` envelope before decode (TASK-074)`

## Implementation Log

### 2026-06-24 — Implementation (impl-1)

**Root cause confirmed.** The `/stream` combined-stream pump (`StreamEngine.cs:236`) passes the FULL
envelope `{"stream":"<token>","data":{...}}` (the whole `frame.Value`) to `decoder(bytes)`.
`BinanceStreamDecoders` deserialized those envelope bytes DIRECTLY into the leaf DTO, so every field
stayed unset and the order-book decoder threw "does not carry a symbol field". KuCoin already unwraps
`data` first via `DeserializeData<T>` — mirrored here.

**Decoder fix (`BinanceStreamDecoders.cs`).** Added a private static `DeserializeData<T>` helper:
`JsonDocument.ParseValue` → read `data` property → `Deserialize<T>` of that element with the existing
case-sensitive `JsonOpts`. Throws `InvalidOperationException` if `data` is missing (the `/stream`
endpoint is always combined-stream, so a missing `data` is malformed — no silent envelope pass-through).
Routed Ticker, Trade, Kline through it. OrderBook needed special handling: the live test subscribes at
`depth: 20`, i.e. the partial-book `@depthN` stream, whose `data` payload carries NO `s` symbol field
(per `StreamDepthDto` docs + Binance API). Added `DeserializeDepth` (returns the unwrapped
`StreamDepthDto` plus the envelope `stream` token) and `WireSymbolFromStreamToken`
(`"btcusdt@depth20"` → `"BTCUSDT"`, upper-cased to match `FromWire`'s warm table). OrderBook resolves
the symbol from `data.s` when present (diff-depth) and falls back to the stream token otherwise
(partial-book) — clear throw if neither resolves. Class `<summary>`/`<remarks>` were already accurate
("deserializes the combined-stream `data` payload") — now actually true; left as-is.

**Integration test (`BinanceStreamSmokeTests.cs`).** The masking lived in `CheckReachabilityAsync`,
which TASK-073 had rewritten to drive a full subscribe + REQUIRE a delivered `OrderBook` before
declaring the venue reachable. Under the decode bug that always failed → ALL Binance integration tests
(not just multi-symbol) silently self-skipped. Restored it to a clean TLS-handshake probe mirroring
KuCoin (`ClientWebSocket.ConnectAsync` to `wss://stream.binance.com:9443/stream`). The multi-symbol
test body itself had no separate workaround — once the probe is clean it runs for real. Kept the
genuine offline self-skip and the `[Trait("Category","Integration")]` tag.

**Unit tests (`BinanceStreamDecodeTests.cs`) — were testing the WRONG contract.** The existing four
decode tests fed bare `data`-LEVEL JSON directly (e.g. `{"s":"BTCUSDT","c":"67000.00",...}`), which
the OLD decoder deserialized directly — so they were green while the engine, which delivers the
ENVELOPE, was broken. This is exactly the hidden-bug case the task flagged. Updated all four to
envelope-level via a new `Envelope(streamToken, dataJson)` helper, and added a fifth test
`OrderBook_PartialBookEnvelope_ResolvesSymbolFromStreamToken` covering the `@depth20` (no-`s`) shape
the live test actually uses — the precise frame the bug threw on. Decode-test count 4 → 5 (+DI test = 6).

**Verification.**
- Build: `dotnet build CryptoExchanges.Net.sln` → **0 Warning / 0 Error**.
- Non-integration: `dotnet test --filter 'Category!=Integration'` → **all green** (Binance.Unit 28,
  Http.Unit 96, Kucoin.Unit 203, etc.; 0 failed / 0 skipped). Binance decode tests: 6/6 pass.
- Live Binance multi-symbol: `OrderBook_MultiSymbol_LiveStream_DeliversAtLeastOneUpdate` → **PASSED**
  (executed, not skipped; 10 s) — real book delivered + ≥1 subscription `Live`. Confirms the order
  book now reaches the callback end-to-end (18-symbol throttled burst → decode → delivery).
- Live KuCoin multi-symbol: same test → **PASSED** (5 s, 14 symbols). No regression.

**Conventions.** LEAN comments (only the combined-stream `data`-envelope + partial-book symbol-source
quirks); no file-level `using System.Text.Json` (already a global using — avoids duplicate-using
analyzer warning, unlike KuCoin's file which predates the global); `TreatWarningsAsErrors` +
`AnalysisLevel=latest-all` clean. Applied LR-010 (no self-evident `<remarks>`, no dead `_ = x` in the
test changes); LR-009 awareness (verified the live `@depth20` payload omits `s` against the DTO docs
before relying on the stream-token fallback).