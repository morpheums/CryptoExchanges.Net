---
id: TASK-022
status: DONE
---

> **PR #16 review fixes (GitHub Copilot):** (1) BUG — `ExchangeTimeSync.ApplyOffset` now rejects non-positive serverTimeMs (was writing ~-localNow offset, breaking signed requests); fixed at Core altitude → also protects OKX/Bybit/Binance (all shared the unguarded pattern). +2 Core tests. (2) removed a `<remarks>` essay violating ADR-001 conv #7. (3) BitgetErrorTranslator success-code/null branch now messages by HTTP status, not the misleading "00000". Full suite green.

> **Gate PASSED (round 1)** — all 4 APPROVE (architect 97, code 95, security 94, api 95), zero blocking. +2 simplifier edits folded (ServerTimeMs/ParseTimestamp → BitgetValueParsers.ParseMs). Bitget unit 92 + integration 6. ZERO Core change; only the standard per-exchange Http InternalsVisibleTo line — proves the TASK-009 generalization held. **CLOSES MILESTONE M-BITGET (25/25).**

# TASK-022: Bitget services + mapping + error + time + tests + AddBitgetExchange DI (closes M-BITGET)

**Milestone**: M-BITGET
**Wave**: 15
**Group**: 15
**Status**: IMPLEMENTED
**Depends on**: TASK-019, TASK-021
**Retry count**: 0/3
**Base SHA**: 2456a1be28c6fe317140348e37609876609be353
**Delegates to**: none
**Traces to**: research#bitget (spot REST parity; DeltaMapper mandate); research#architectural-implication (Bitget validates the OKX-era abstraction holds with minimal new code)
**Blast radius**: HIGH — modifies shared DI `ServiceCollectionExtensions`/`CryptoExchangesOptions` (architect + api review); defines public `BitgetExchangeClient`; closes the final milestone.

## Description
Complete Bitget to spot-REST parity and ship it. Implement the three services, `BitgetMappingProfiles` (DeltaMapper), `BitgetClientComposer`, public `BitgetExchangeClient`, `BitgetErrorTranslator` (Bitget `code`/`msg` envelope + 401/403/429 → typed exceptions) and `BitgetTimeSync`. Wire `AddBitgetExchange` into DI (keyed-by-`ExchangeId.Bitget` singletons, named HttpClient, `ApplyResiliencePipeline` with Bitget translator/gate, finalizer returning `PassThroughHandler` when secret/passphrase absent else `BitgetSigningHandler`). Extend `CryptoExchangesOptions` + `AddCryptoExchanges` for Bitget. Author Bitget unit + integration test projects. This closes the final milestone.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bitget/Services/BitgetMarketDataService.cs`
- `src/CryptoExchanges.Net.Bitget/Services/BitgetTradingService.cs`
- `src/CryptoExchanges.Net.Bitget/Services/BitgetAccountService.cs`
- `src/CryptoExchanges.Net.Bitget/Mapping/BitgetMappingProfiles.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetClientComposer.cs`
- `src/CryptoExchanges.Net.Bitget/BitgetExchangeClient.cs`
- `src/CryptoExchanges.Net.Bitget/Resilience/BitgetErrorTranslator.cs`
- `src/CryptoExchanges.Net.Bitget/Resilience/BitgetTimeSync.cs`
- `tests/CryptoExchanges.Net.Bitget.Tests.Unit/CryptoExchanges.Net.Bitget.Tests.Unit.csproj`
- `tests/CryptoExchanges.Net.Bitget.Tests.Unit/BitgetSigningTests.cs`
- `tests/CryptoExchanges.Net.Bitget.Tests.Unit/BitgetMappingAndServiceTests.cs`
- `tests/CryptoExchanges.Net.Bitget.Tests.Integration/CryptoExchanges.Net.Bitget.Tests.Integration.csproj`
- `tests/CryptoExchanges.Net.Bitget.Tests.Integration/BitgetPipelineEndToEndTests.cs`
### Modifies
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs`
- `CryptoExchanges.Net.sln`

## Files modified
- src/CryptoExchanges.Net.Bitget/Services/BitgetMarketDataService.cs
- src/CryptoExchanges.Net.Bitget/Services/BitgetTradingService.cs
- src/CryptoExchanges.Net.Bitget/Services/BitgetAccountService.cs
- src/CryptoExchanges.Net.Bitget/Mapping/BitgetMappingProfiles.cs
- src/CryptoExchanges.Net.Bitget/Internal/BitgetClientComposer.cs
- src/CryptoExchanges.Net.Bitget/BitgetExchangeClient.cs
- src/CryptoExchanges.Net.Bitget/Resilience/BitgetErrorTranslator.cs
- src/CryptoExchanges.Net.Bitget/Resilience/BitgetTimeSync.cs
- tests/CryptoExchanges.Net.Bitget.Tests.Unit/CryptoExchanges.Net.Bitget.Tests.Unit.csproj
- tests/CryptoExchanges.Net.Bitget.Tests.Unit/BitgetSigningTests.cs
- tests/CryptoExchanges.Net.Bitget.Tests.Unit/BitgetMappingAndServiceTests.cs
- tests/CryptoExchanges.Net.Bitget.Tests.Integration/CryptoExchanges.Net.Bitget.Tests.Integration.csproj
- tests/CryptoExchanges.Net.Bitget.Tests.Integration/BitgetPipelineEndToEndTests.cs
- src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs
- CryptoExchanges.Net.sln

## Pattern Reference
- `src/CryptoExchanges.Net.Okx/*` (OKX is the closest sibling — base64/passphrase/header-signing established TASK-010..015)
- `src/CryptoExchanges.Net.Binance/Services/*`, `Mapping/BinanceMappingProfiles.cs`, `Internal/BinanceClientComposer.cs:16-87`, `Resilience/BinanceErrorTranslator.cs:19-24`, `BinanceTimeSync.cs`
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:35-165` (AddBinanceExchange + secret/passphrase-gated finalizer + CryptoExchangesOptions)
- `tests/CryptoExchanges.Net.Binance.Tests.Integration/BinancePipelineEndToEndTests.cs:40-50`

## Acceptance Criteria
1. `dotnet test --filter 'Category!=Integration'` passes; `AddBitgetExchange`/`AddCryptoExchanges` resolve a working `IExchangeClient` keyed by `ExchangeId.Bitget`; finalizer is `PassThroughHandler` when SecretKey or Passphrase is missing.
2. Services return DeltaMapper-mapped domain models (`AssertConfigurationIsValid` passes); Bitget error codes + 401/403/429 map to correct typed exceptions; only `BitgetExchangeClient`/`BitgetOptions` are public.
3. Binance, Bybit, and OKX registrations/tests are unaffected; full solution builds clean under TreatWarningsAsErrors. The Bitget implementation reuses the TASK-009 abstraction with NO new Core/Http changes (proves the generalization held).

## Test Requirements
- This IS the test task for Bitget. Coverage: base64 signature vector, prehash assembly (GET path+`?`query vs POST body, epoch-ms timestamp), four-header signing + re-sign-on-retry (stub handler), passphrase-missing fast-fail, symbol round-trip, parsers, validation, per-service DeltaMapper mapping, error mapping, time sync, and DI resolution.

## Commits
- `a9caa69623fae9f88276e7bbdac2f9fd447b0e6d` — feat(M4): TASK-022 Bitget services, mapping, composer, client, error translator + AddBitgetExchange DI + tests (closes M-BITGET)

## Implementation Notes

### Files created (src)
- `Services/BitgetMarketDataService.cs` — V2 spot market data + DTOs (`BitgetResponse<T>`, `BitgetObjectResponse<T>`, `BitgetTicker`, `BitgetOrderBook`, `BitgetTrade`, `BitgetSymbol`). Endpoints: `/api/v2/spot/market/{tickers,orderbook,candles,fills}`, `/api/v2/spot/public/symbols`. Limit clamps: orderbook 150, candles 1000, fills 500.
- `Services/BitgetTradingService.cs` — V2 trading + DTOs (`BitgetOrderAck`, `BitgetOrder`). place/cancel/orderInfo/unfilled-orders/history-orders/batch-cancel.
- `Services/BitgetAccountService.cs` — V2 account + DTOs (`BitgetBalance`, `BitgetFill`). `/api/v2/spot/account/assets`, `/api/v2/spot/trade/fills`.
- `Mapping/BitgetMappingProfiles.cs` — DeltaMapper `BitgetResponseProfile` (Order/Ticker/SymbolInfo/AssetBalance); `AssertConfigurationIsValid()` in composer.
- `Internal/BitgetClientComposer.cs` — CreateMapper/Create/ComposeOver/ComposeWith/ComposeForDi/BuildResilientHttpClient + `NormalizeHostRoot` host-root guard.
- `BitgetExchangeClient.cs` — public client; Create/CreateFromEnvironment (BITGET_API_KEY/SECRET_KEY/PASSPHRASE)/SyncServerTimeAsync/PingAsync/ExchangeId.Bitget/IAsyncDisposable + `BitgetServerTime`.
- `Resilience/BitgetErrorTranslator.cs` — internal `IExchangeErrorTranslator`.
- `ServiceCollectionExtensions.cs` — public `AddBitgetExchange` (in the Bitget assembly per ADR-001).

### Files created (tests)
- Unit: `BitgetSigningTests.cs` (42 facts/theory-cases), `BitgetMappingAndServiceTests.cs`. Total Bitget unit tests: **92**.
- Integration: `BitgetPipelineEndToEndTests.cs` — **6** tests `[Trait("Category","Integration")]`.
- Both projects added to `CryptoExchanges.Net.sln`.

### Files modified
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs` — `AddCryptoExchanges` calls `AddBitgetExchange`; added Bitget* options. csproj ProjectReference to Bitget added.
- `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj` — **one** line: `InternalsVisibleTo CryptoExchanges.Net.Bitget` (the standard per-exchange grant identical to Binance/Bybit/OKX). NO Http `.cs`/abstraction change. NO Core change at all — the TASK-009 generalization held.

### Key decisions / deviations
- **DEVIATION (BitgetTimeSync NOT created)**: the manifest File Scope lists `Resilience/BitgetTimeSync.cs`, but REF-001 moved time-sync to Core (`IExchangeTimeSync`/`ExchangeTimeSync`). Bitget reuses the Core one via DI (injected, like OKX post-REF-002) — no exchange-specific time-sync type. `SyncServerTimeAsync` calls the injected `IExchangeTimeSync.ApplyOffset`.
- **AddBitgetExchange via shared helper**: delegates to `ExchangeServiceRegistration.AddExchange<BitgetOptions, IMapper>(...)` (mirrors AddOkxExchange exactly) — ExchangeId.Bitget, client name "bitget", default `ResilienceOptions()` (no usage header — Bitget V2 exposes no documented usage-fraction header), symbol mapper from `BitgetSymbolFormat`, mapper from `BitgetClientComposer.CreateMapper`, secret+passphrase-gated finalizer, `ComposeForDi` exchangeClientFactory.
- **Secret+passphrase-gated finalizer**: PassThrough when SecretKey OR Passphrase empty; else `BitgetSigningHandler(apiKey, passphrase, new BitgetSignatureService(secret), () => Interlocked.Read(ref holder[0]))`. `ToCredentials()` is never on the signing path (resolves TASK-010/017 carry-in).
- **BaseAddress host-root guard (TASK-021 CONCERN#1)**: `BitgetClientComposer.NormalizeHostRoot` validates the BaseUrl has no path segment (throws otherwise) then trims a trailing slash. Used by BOTH the container-free `BuildResilientHttpClient` and the DI `baseUrlSelector`, so the sign-consistency invariant (RequestUri.AbsolutePath/Query == signed prehash path/query) is self-enforcing. Unit test `Di_AddBitgetExchange_BaseUrlWithPath_FailFast` covers it.
- **Error-code map** (real Bitget V2 codes; `ValueKind`-guarded reads per ADR-001 conv 3): SUCCESS `"00000"` → NEVER an error (returns plain `ExchangeApiException`, asserted by `ErrorTranslator_SuccessCode_IsNotAnError`). Auth (401/403 + `40006/40009/40011/40012/40014/40018/40037/40002/40008`) → `AuthenticationException`. Rate limit (429 + `429/30007/40404`) → `RateLimitExceededException` w/ RetryAfter. Insufficient balance (`43012/43011`) → `InsufficientBalanceException`. Order errors (`40808/43001/43002/43025/45110/400172`) → `InvalidOrderException`. Else `ExchangeApiException` fallback (mapped conservatively; commented where coverage is partial).
- **Place-order POST body**: single-order place/cancel/cancel-by-client use the flat `Dictionary<string,string>` overload (all Bitget spot single-order fields are scalar → serialized JSON is the verbatim signed body). Batch cancel (`/api/v2/spot/trade/batch-cancel-order`) uses the object-body overload (JSON array). Documented in `BitgetTradingService` remarks.
- **DeltaMapper mandate**: all DTO→domain via `BitgetResponseProfile`; `AssertConfigurationIsValid()` in `CreateMapper` (asserted by `MapperConfiguration_IsValid`). Trade/OrderBook/Candlestick direct-build precedent matches OKX. Bitget reports cumulative quote (`quoteVolume`) and fractional 24h change (`change24h`) directly, so no avgPx multiplication. Domain `Locked` = Bitget `frozen + locked`.
- **Public surface**: only `BitgetExchangeClient` + `BitgetOptions` are public domain types (plus the public `ServiceCollectionExtensions` DI entry-point, public-by-necessity exactly like OKX). Everything else internal.

### Verification
- `dotnet build CryptoExchanges.Net.sln` → **0 warnings, 0 errors** (TreatWarningsAsErrors).
- `dotnet test --filter 'Category!=Integration'` → ALL pass. Bitget unit = 92; no Binance/Bybit/OKX/Core/Http/DI regression.
- `dotnet test --filter 'Category=Integration'` → Bitget **6**, OKX **6**, Bybit **5** — all pass, no regression.
- Confirmed NO Core changes and Http change is the single per-exchange `InternalsVisibleTo` line only (no abstraction/.cs change) — proves the TASK-009 generalization held.
