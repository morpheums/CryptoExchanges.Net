---
status: PLANNED
---
# TASK-084: OKX multi-symbol L2 order-book streaming integration smoke test (+ OKX PR to main)

## Metadata
- **ID**: TASK-084
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-083
- **Delegates to**: none
- **Files modified**: [tests/CryptoExchanges.Net.Okx.Tests.Integration/Streaming/OkxStreamingSmokeTests.cs]
- **Wave**: 10
- **Traces to**: PRD AC#3 + Success Metrics; TRD §"OKX Public WebSocket"; ADR-009-005 (PR per exchange); K2/K3
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Add `OkxStreamingSmokeTests.cs` in `tests/CryptoExchanges.Net.Okx.Tests.Integration/Streaming/`,
mirroring `KucoinStreamingSmokeTests.cs`. `[Trait("Category","Integration")]`, 8s reachability
self-skip against `wss://ws.okx.com:8443/ws/v5/public` (offline → skip, not fail).

Test cases (TEST-PLAN §File 4), driving the real public OKX WS through `AddOkxExchange()+AddOkxStreams()`:
- `Ticker_LiveStream_DeliversAtLeastOneUpdate` (≥1 / 20 s)
- `Trade_LiveStream_DeliversAtLeastOneUpdate` (≥1 / 20 s)
- `Kline_LiveStream_DeliversAtLeastOneUpdate` (≥1 / 20 s)
- `OrderBook_MultiSymbol_DeliversAtLeastOneUpdate` — ≥8 symbols (BTC/ETH/SOL/XRP/ADA/DOGE/AVAX/LTC vs USDT in OKX `BTC-USDT` wire form); OrderBook callback ≥1 / 30 s, NO reconnect loop. Critical FEAT-008 regression gate for OKX, additionally exercising the client text-ping / bare-`pong` heartbeat path under load.

Completes the OKX group. Per ADR-009-005, OKX ships as its OWN PR to `main`, merged BEFORE Bitget
begins. PR opened for `feat/FEAT-009-...` → `main` (re-branched from main after Bybit) after review
approval; Bitget (TASK-085) is gated on that merge.

**Implementor note:** report live verification — run the OKX Integration suite + report pass/update
counts (or explicit unreachable-skip); confirm no reconnect loop and that the text ping/pong keeps
the connection alive during the multi-symbol run.

### Steps
1. Read `KucoinStreamingSmokeTests.cs` (probe/self-skip/await harness) + the Binance multi-symbol regression shape.
2. Implement the four cases with the OKX endpoint + ≥8-symbol set.
3. Run live: `dotnet test --filter 'Category=Integration'` for the OKX integration project; report counts.
4. Confirm `dotnet test --filter 'Category!=Integration'` green + build 0W/0E.

## Acceptance Criteria
- [ ] `OkxStreamingSmokeTests.cs` exists with the four cases (Ticker/Trade/Kline live + multi-symbol OrderBook ≥8 symbols / ≥1 update / 30s / no reconnect loop), `[Trait("Category","Integration")]`, 8s reachability self-skip — mirroring `KucoinStreamingSmokeTests.cs`.
- [ ] Live verification reported: OKX Integration suite run with pass + update counts (or explicit unreachable-skip); multi-symbol case confirms ≥1 OrderBook, no reconnect loop, text ping/pong stable.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green. (OKX PR to `main` opened after review approval — gates Bitget.)

## Pattern Reference
- Integration smoke + reachability self-skip: `tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs`
- Bybit smoke as immediate intra-objective reference: `tests/CryptoExchanges.Net.Bybit.Tests.Integration/Streaming/BybitStreamingSmokeTests.cs`
- Multi-symbol L2 regression shape: `tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs`

## File Scope
**Creates**:
- tests/CryptoExchanges.Net.Okx.Tests.Integration/Streaming/OkxStreamingSmokeTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #3 + Success Metrics (≥1 OrderBook/30s)
- **TRD Component**: OKX end-to-end smoke
- **ADR Reference**: ADR-009-005 (PR per exchange, strict order); K2, K3

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
