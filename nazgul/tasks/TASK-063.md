---
id: TASK-063
status: PLANNED
depends_on: [TASK-060, TASK-062]
---
# TASK-063: Live integration smokes — REST + one streaming (self-skip without credentials)

## Metadata
- **ID**: TASK-063
- **Group**: 7
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-060, TASK-062
- **Delegates to**: none
- **Files modified**: [tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinRestSmokeTests.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs]
- **Wave**: 7
- **Traces to**: PRD-FEAT-006 AC-1, AC-3, AC-4, AC-8; TRD-FEAT-006 §"Dependency Impact"; FEAT-006 spec §"Build approach" step 8, §"Success criteria"; TEST-PLAN-FEAT-006 §"Integration Test Areas"
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**:
- **Base SHA**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Add live integration smokes that self-skip when credentials/connectivity are absent, following the
existing OKX/Binance integration pattern (`Skip.If` on missing env vars, `[Trait("Category",
"Integration")]`). These are excluded from the default `dotnet test --filter 'Category!=Integration'`
gate, so they never break CI but verify the real pipeline when env vars are present.

Create:
- **`KucoinRestSmokeTests.cs`** — uses `KucoinExchangeClient.CreateFromEnvironment()` (or the DI
  equivalent); self-skip when `KUCOIN_API_KEY` is unset:
  - `GetServerTime_ReturnsTimestamp` (public).
  - `GetTicker_BtcUsdt_ReturnsTicker` (public market data).
  - `GetOrderBook_BtcUsdt_ReturnsOrderBook` (public market data).
  - `GetBalances_WithCredentials_ReturnsBalances` (signed — exercises passphrase-v2).
  - `PlaceAndCancelOrder_LimitBuy_Roundtrip` (signed — far-out-of-market limit order, then cancel).
- **`KucoinStreamingSmokeTests.cs`** — uses `AddKucoinStreams` DI; self-skip without connectivity:
  - `StreamTicker_BtcUsdt_ReceivesUpdate` — subscribe ticker, await one frame within ~30s.
  - `StreamReconnect_TokenRenegotiated` — force-close the socket; assert reconnect re-calls
    bullet-public and the callback resumes (AC-4 evidence).

No opsec leakage in test names/comments — strictly technical. No `Thread.Sleep` (use awaitable
timeouts/`TaskCompletionSource`). Reuse `KUCOIN_SECRET_KEY` / `KUCOIN_PASSPHRASE` env vars for signed
calls.

## Acceptance Criteria
- [ ] `KucoinRestSmokeTests` cover server-time/ticker/order-book (public) + balances + place/cancel order (signed, passphrase-v2), all `[Trait("Category","Integration")]` and self-skipping via `Skip.If` when `KUCOIN_API_KEY` is absent.
- [ ] `KucoinStreamingSmokeTests` cover one ticker subscribe (await a live frame) + a forced-disconnect reconnect that re-negotiates the bullet-public token and resumes the callback (AC-4), self-skipping without connectivity; no `Thread.Sleep`.
- [ ] Default gate `dotnet test --filter 'Category!=Integration'` is unaffected and stays 100% green; solution builds 0W/0E; test names/comments contain no opsec leakage.

## Pattern Reference
- REST integration smoke + self-skip: `tests/CryptoExchanges.Net.Okx.Tests.Integration/OkxPipelineEndToEndTests.cs` (`CreateFromEnvironment`, `Skip.If`, Category trait).
- Streaming integration smoke: the FEAT-005 Binance streaming integration test under `tests/CryptoExchanges.Net.Binance.Tests.Integration/` (subscribe + await frame, forced-disconnect reconnect).
- Env-var convention: `BinanceExchangeClient.CreateFromEnvironment` (→ `KUCOIN_*` analog from TASK-059).

## File Scope

**Creates**:
- tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinRestSmokeTests.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs

**Modifies**:
- (none — additive)

## Traceability
- **PRD Acceptance Criteria**: AC-1 (live REST → Core.Models), AC-3 (live streams), AC-4 (forced disconnect → reconnect + token re-negotiation + resubscribe), AC-8 (self-skip without env vars)
- **TRD Component**: §"Dependency Impact" (Integration test project)
- **ADR Reference**: FEAT-006 spec §"Success criteria" (live integration smokes self-skip); no opsec leakage

## Commits

<!-- implementer fills SHAs -->

## Implementation Log

## Review Results
