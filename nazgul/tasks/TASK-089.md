---
status: PLANNED
---
# TASK-089: Bitget multi-symbol L2 order-book streaming integration smoke test (+ Bitget PR to main)

## Metadata
- **ID**: TASK-089
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-088
- **Delegates to**: none
- **Files modified**: [tests/CryptoExchanges.Net.Bitget.Tests.Integration/Streaming/BitgetStreamingSmokeTests.cs]
- **Wave**: 15
- **Traces to**: PRD AC#3 + Success Metrics; TRD §"Bitget Public WebSocket"; ADR-009-005 (PR per exchange); K2/K3
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Add `BitgetStreamingSmokeTests.cs` in `tests/CryptoExchanges.Net.Bitget.Tests.Integration/Streaming/`,
mirroring `KucoinStreamingSmokeTests.cs`. `[Trait("Category","Integration")]`, 8s reachability
self-skip against `wss://ws.bitget.com/v2/ws/public` (offline → skip, not fail).

Test cases (TEST-PLAN §File 4), driving the real public Bitget WS through
`AddBitgetExchange()+AddBitgetStreams()`:
- `Ticker_LiveStream_DeliversAtLeastOneUpdate` (≥1 / 20 s)
- `Trade_LiveStream_DeliversAtLeastOneUpdate` (≥1 / 20 s)
- `Kline_LiveStream_DeliversAtLeastOneUpdate` (≥1 / 20 s)
- `OrderBook_MultiSymbol_DeliversAtLeastOneUpdate` — ≥8 symbols (BTC/ETH/SOL/XRP/ADA/DOGE/AVAX/LTC vs USDT in Bitget `BTCUSDT` wire form); OrderBook callback ≥1 / 30 s, NO reconnect loop. Critical FEAT-008 regression gate for Bitget, exercising the confirmed Bitget heartbeat under load.

Completes the Bitget group and the FEAT-009 objective. Per ADR-009-005, Bitget ships as its OWN PR
to `main` (re-branched from main after OKX) after review approval. This is the final exchange — once
merged, the objective's three-PR sequence (Bybit → OKX → Bitget) is complete and post-loop runs.

**Implementor note:** report live verification — run the Bitget Integration suite + report pass/update
counts (or explicit unreachable-skip); confirm no reconnect loop and stable heartbeat during the
multi-symbol run.

### Steps
1. Read `KucoinStreamingSmokeTests.cs` (probe/self-skip/await harness) + the just-built OKX smoke as the immediate intra-objective reference.
2. Implement the four cases with the Bitget endpoint + ≥8-symbol set.
3. Run live: `dotnet test --filter 'Category=Integration'` for the Bitget integration project; report counts.
4. Confirm `dotnet test --filter 'Category!=Integration'` green + build 0W/0E.

## Acceptance Criteria
- [ ] `BitgetStreamingSmokeTests.cs` exists with the four cases (Ticker/Trade/Kline live + multi-symbol OrderBook ≥8 symbols / ≥1 update / 30s / no reconnect loop), `[Trait("Category","Integration")]`, 8s reachability self-skip — mirroring `KucoinStreamingSmokeTests.cs`.
- [ ] Live verification reported: Bitget Integration suite run with pass + update counts (or explicit unreachable-skip); multi-symbol case confirms ≥1 OrderBook, no reconnect loop, stable heartbeat.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green. (Bitget PR to `main` opened after review approval — final exchange.)

## Pattern Reference
- Integration smoke + reachability self-skip: `tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs`
- OKX smoke as immediate intra-objective reference: `tests/CryptoExchanges.Net.Okx.Tests.Integration/Streaming/OkxStreamingSmokeTests.cs`
- Multi-symbol L2 regression shape: `tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs`

## File Scope
**Creates**:
- tests/CryptoExchanges.Net.Bitget.Tests.Integration/Streaming/BitgetStreamingSmokeTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #3 + Success Metrics (≥1 OrderBook/30s)
- **TRD Component**: Bitget end-to-end smoke
- **ADR Reference**: ADR-009-005 (PR per exchange, strict order, final); K2, K3

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
