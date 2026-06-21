---
id: TASK-059
status: IMPLEMENTED
depends_on: [TASK-057, TASK-058]
---
# TASK-059: REST client — market-data / account / trading service methods + HTTP client + composer

## Metadata
- **ID**: TASK-059
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-057, TASK-058
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Kucoin/IKucoinHttpClient.cs, src/CryptoExchanges.Net.Kucoin/KucoinHttpClient.cs, src/CryptoExchanges.Net.Kucoin/Services/KucoinMarketDataService.cs, src/CryptoExchanges.Net.Kucoin/Services/KucoinAccountService.cs, src/CryptoExchanges.Net.Kucoin/Services/KucoinTradingService.cs, src/CryptoExchanges.Net.Kucoin/Internal/KucoinClientComposer.cs, src/CryptoExchanges.Net.Kucoin/Internal/KucoinRequestValidation.cs, src/CryptoExchanges.Net.Kucoin/KucoinExchangeClient.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinServiceTests.cs]
- **Wave**: 3
- **Traces to**: PRD-FEAT-006 AC-1, AC-2; TRD-FEAT-006 §"Project Layout", §"Signing"; FEAT-006 spec §"REST" (market/account/trading surface), §"Build approach" step 4; TEST-PLAN-FEAT-006 §"stub-HTTP service methods"
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**: 2026-06-21T00:00:00Z
- **Base SHA**: b0bbb902c3c16f3ccd73aa1ae070959bfd3448a5
- **Implemented at**: 2026-06-21T00:30:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Wire the full REST surface, TDD with a stub HTTP handler (no network), cloning OKX. Implements the same
`IExchangeClient` / `IMarketDataService` / `IAccountService` / `ITradingService` members the other four
exchanges expose, returning canonical `Core.Models` via the TASK-058 DeltaMapper profiles and signing
via the TASK-057 handler. Signed methods mark requests with `KucoinSigningRequest`; retry stays GET-only.

Create:
- **`IKucoinHttpClient.cs` / `KucoinHttpClient.cs`** — clone OKX: the typed HTTP client wrapping the
  resilient `HttpClient`, deserializing the `ResponseDto<T>` envelope (success code `"200000"`),
  applying `KucoinErrorTranslator` on failure.
- **`Services/KucoinMarketDataService.cs`** — tickers, order book, candlesticks, recent trades,
  exchange info (symbols), price, symbol resolve / `IsSupported`, ping/server-time. Public (unsigned)
  GET endpoints.
- **`Services/KucoinAccountService.cs`** — balances (per-asset + all). Signed GET.
- **`Services/KucoinTradingService.cs`** — place / cancel / cancel-by-client-id / cancel-all / get /
  open / history orders + fills / trade history. Signed; place/cancel are POST/DELETE (NOT retried),
  reads are signed GET.
- **`Internal/KucoinRequestValidation.cs`** — clone OKX pre-flight validation (symbol supported,
  quantities/prices positive, required fields present).
- **`Internal/KucoinClientComposer.cs`** — clone `OkxClientComposer`: single composition root building
  the mapper (`KucoinMappingProfiles`), keyed `ISymbolMapper`, services, and the http client — used by
  BOTH the factory entry and DI (`ComposeForDi`). Factory-free + DI parity.
- **`KucoinExchangeClient.cs`** — clone `OkxExchangeClient`: public entry `Create(KucoinOptions)` +
  `CreateFromEnvironment()` (reads `KUCOIN_API_KEY`/`KUCOIN_SECRET_KEY`/`KUCOIN_PASSPHRASE`), exposing
  `MarketData`/`Account`/`Trading` and `ExchangeId.Kucoin`.

Tests (`KucoinServiceTests.cs`) — stub HTTP handler returning canned KuCoin JSON, no network:
- Each market-data method maps the stub response to the right `Core.Models` type.
- A signed method (e.g. GetBalances) emits a `KucoinSigningRequest`-marked request.
- Place/cancel order build the correct request path/body and map the order ack.
- Error envelope (`code != "200000"`) surfaces a typed exception via `KucoinErrorTranslator`.

## Acceptance Criteria
- [ ] `KucoinMarketDataService` / `KucoinAccountService` / `KucoinTradingService` implement the same `Core` service interfaces the other exchanges expose (market data + balances + place/cancel/cancel-by-client-id/cancel-all/get/open/history orders + fills/trade history), each returning canonical `Core.Models` via DeltaMapper; signed methods mark `KucoinSigningRequest`; retry stays GET-only; full XML docs (`<inheritdoc/>` on impls).
- [ ] `KucoinExchangeClient.Create(...)` + `CreateFromEnvironment()` + `KucoinClientComposer` (shared by factory + DI) + typed `KucoinHttpClient` (ResponseDto envelope, success `"200000"`, error translation) exist, one type per file, mirroring OKX.
- [ ] `KucoinServiceTests` drive every service method through a stub HTTP handler asserting mapped `Core.Models` + signed-request marking + error translation — NO network; solution builds 0W/0E; existing non-integration suite stays green.

## Pattern Reference
- Service implementations: `src/CryptoExchanges.Net.Okx/Services/OkxMarketDataService.cs`, `OkxAccountService.cs`, `OkxTradingService.cs`.
- Typed http client + envelope: `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs` + `IOkxHttpClient.cs`.
- Composer (factory + DI parity): `src/CryptoExchanges.Net.Okx/Internal/OkxClientComposer.cs`. Validation: `src/CryptoExchanges.Net.Okx/Internal/OkxRequestValidation.cs`.
- Public entry + env factory: `src/CryptoExchanges.Net.Okx/OkxExchangeClient.cs`.
- Stub-HTTP service tests: `tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxMappingAndServiceTests.cs`.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Kucoin/IKucoinHttpClient.cs
- src/CryptoExchanges.Net.Kucoin/KucoinHttpClient.cs
- src/CryptoExchanges.Net.Kucoin/Services/KucoinMarketDataService.cs
- src/CryptoExchanges.Net.Kucoin/Services/KucoinAccountService.cs
- src/CryptoExchanges.Net.Kucoin/Services/KucoinTradingService.cs
- src/CryptoExchanges.Net.Kucoin/Internal/KucoinClientComposer.cs
- src/CryptoExchanges.Net.Kucoin/Internal/KucoinRequestValidation.cs
- src/CryptoExchanges.Net.Kucoin/KucoinExchangeClient.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinServiceTests.cs

**Modifies**:
- (none — additive)

## Traceability
- **PRD Acceptance Criteria**: AC-1 (REST parity → Core.Models), AC-2 (signed endpoints authenticate), AC-7 (stub-HTTP no-network tests)
- **TRD Component**: §"Project Layout" (Services/, composer, http client), §"Signing" (signed request marking)
- **ADR Reference**: FEAT-006 spec §"REST" surface; retry-only-on-GET; DeltaMapper mandate

## Commits

- `95a6066` — feat(FEAT-006): TASK-059 — KuCoin REST services + HTTP client + composer + entry point

## Implementation Log

### 2026-06-21

- Created `IKucoinHttpClient` / `KucoinHttpClient` (GET/POST/DELETE, signed flag, ResponseDto<T> envelope).
- Created `KucoinMarketDataService` (tickers, orderbook, candles, price, trades, exchangeInfo, isSupportedAsync, resolveSymbolAsync).
- Created `KucoinAccountService` (getBalances, getBalance, getTradeHistoryAsync via fills/ListDto).
- Created `KucoinTradingService` (place/cancel/cancelByClientId/cancelAll/get/openOrders/orderHistory).
- Created `KucoinRequestValidation` (limit 1–500, ordered time window).
- Created `KucoinClientComposer` (factory-free + DI composition root, BuildResilientHttpClient).
- Created `KucoinExchangeClient` (Create/CreateFromEnvironment, PingAsync, SyncServerTimeAsync).
- Added `InternalsVisibleTo` for CryptoExchanges.Net.Kucoin in Http project.
- Fixed carry-over #1: `SyncServerTimeAsync` uses `ResponseDto<long>` directly (no double-wrap).
- Fixed carry-over #2: Added `IsSupported_UnresolvableSymbol_ReturnsFalse` test; renamed two misnamed `_ReturnsFalse` tests.
- Fixed carry-over #3: `KucoinSymbolMapper.FromWire` now propagates `FormatException` directly (matches ISymbolMapper contract).
- Added `KucoinServiceTests.cs` with 40 new stub-HTTP tests.
- Build: 0W/0E. Tests: 149/149 KuCoin unit pass; full suite 726+ pass.

## Review Results
