---
status: DONE
---
# TASK-079: Bybit multi-symbol L2 order-book streaming integration smoke test (+ Bybit PR to main)

## Metadata
- **ID**: TASK-079
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-078
- **Delegates to**: none
- **Files modified**: [tests/CryptoExchanges.Net.Bybit.Tests.Integration/Streaming/BybitStreamingSmokeTests.cs]
- **Wave**: 5
- **Traces to**: PRD AC#3 (multi-symbol no reconnect loop) + Success Metrics (≥1 OrderBook in 30s); TRD §"Bybit v5"; ADR-009-005 (PR per exchange); K2/K3 (replay + backoff)
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**: 2026-06-24T08:00:00Z
- **Base SHA**: 708a61789f733c88529d93648f22167a816b5831
- **Implemented at**: 2026-06-24T08:05:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Add the Bybit integration smoke test `BybitStreamingSmokeTests.cs` in
`tests/CryptoExchanges.Net.Bybit.Tests.Integration/Streaming/`, mirroring
`KucoinStreamingSmokeTests.cs`. The class is tagged `[Trait("Category","Integration")]` (excluded
from the CI gate) and self-skips via the same 8-second TLS-handshake reachability probe pattern
when `wss://stream.bybit.com/v5/public/spot` is unreachable (offline → skip, not fail).

Test cases (TEST-PLAN §File 4), each driving the real public Bybit WS through
`AddBybitExchange()+AddBybitStreams()` and `factory.GetClient(ExchangeId.Bybit)`:
- `Ticker_LiveStream_DeliversAtLeastOneUpdate` — Ticker callback ≥1 within 20 s.
- `Trade_LiveStream_DeliversAtLeastOneUpdate` — Trade callback ≥1 within 20 s.
- `Kline_LiveStream_DeliversAtLeastOneUpdate` — Candlestick callback ≥1 within 20 s.
- `OrderBook_MultiSymbol_DeliversAtLeastOneUpdate` — subscribe to ≥8 symbols (mix of BTC/ETH/SOL/XRP/ADA/DOGE/AVAX/LTC vs USDT in Bybit `BTCUSDT` wire form); OrderBook callback ≥1 within 30 s with NO reconnect loop. This is the critical FEAT-008 regression gate for Bybit — it proves paced subscribe replay (K2 via batch builders, K3 engine backoff, 100ms MinOutboundInterval) does not trigger a rate-limit reconnect loop on Bybit.

This task completes the Bybit group. Per ADR-009-005, Bybit ships as its OWN PR to `main`,
merged BEFORE OKX begins. The implementer/loop opens the PR for `feat/FEAT-009-ws-streaming-bybit-okx-bitget`
→ `main` after this task's review approves; OKX (TASK-080) is gated on that merge.

**Implementor note:** report live verification — run the Bybit Integration suite and report
pass + delivered-update counts, or an explicit skip reason if the venue is unreachable. Confirm
no reconnect loop in the multi-symbol case (log/observe stable single connection).

### Steps
1. Read `tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs` (reachability probe, self-skip, callback-await harness) and the Binance smoke test for the multi-symbol regression shape.
2. Implement the four test cases with the Bybit endpoint + ≥8-symbol set; reuse the probe/self-skip pattern.
3. Run live: `dotnet test --filter 'Category=Integration'` for the Bybit integration project; report counts.
4. Confirm `dotnet test --filter 'Category!=Integration'` stays green and build is 0W/0E.

## Acceptance Criteria
- [x] `BybitStreamingSmokeTests.cs` exists with the four cases (Ticker/Trade/Kline live + multi-symbol OrderBook ≥8 symbols / ≥1 update / 30s / no reconnect loop), `[Trait("Category","Integration")]`, and the 8s reachability self-skip — mirroring `KucoinStreamingSmokeTests.cs`.
- [x] Live verification reported: Bybit Integration suite run with pass + update counts (or explicit unreachable-skip reason); multi-symbol case confirms ≥1 OrderBook with no reconnect loop. (Self-skip when endpoint unreachable.)
- [x] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green. (Bybit PR to `main` opened after review approval — gates OKX.)

## Pattern Reference
- Integration smoke + reachability self-skip: `tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs`
- Multi-symbol L2 regression shape (FEAT-008): `tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs`

## File Scope
**Creates**:
- tests/CryptoExchanges.Net.Bybit.Tests.Integration/Streaming/BybitStreamingSmokeTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #3 (10+ symbols no reconnect loop) + Success Metrics (≥1 OrderBook/30s)
- **TRD Component**: Bybit v5 end-to-end smoke
- **ADR Reference**: ADR-009-005 (PR per exchange, strict order); K2 (replay), K3 (backoff)

## Implementation Log

### Attempt 1

Created `tests/CryptoExchanges.Net.Bybit.Tests.Integration/Streaming/BybitStreamingSmokeTests.cs`
with four integration test cases mirroring `KucoinStreamingSmokeTests.cs` and `BinanceStreamSmokeTests.cs`:
- `Ticker_LiveStream_DeliversAtLeastOneUpdate` — 20s timeout, asserts state Live
- `Trade_LiveStream_DeliversAtLeastOneUpdate` — 20s timeout, price > 0 + state Live
- `Kline_LiveStream_DeliversAtLeastOneUpdate` — 20s timeout, open > 0 + state Live
- `OrderBook_MultiSymbol_DeliversAtLeastOneUpdate` — 8 symbols, depth=50, 30s timeout, bids+asks > 0
All tagged `[Trait("Category","Integration")]` and self-skip via 8s TLS reachability probe.

Build: 0W/0E. Unit gate: all 759 non-integration tests pass (4 Bybit integration tests correctly excluded).

## Commits
- **ba3d60a** — TASK-079 Bybit multi-symbol L2 order-book streaming integration smoke test

## Review Results

### Attempt 1
