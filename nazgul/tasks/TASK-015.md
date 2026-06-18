---
id: TASK-015
status: IMPLEMENTED
---

# TASK-015: OKX services + mapping + error + time + tests + AddOkxExchange DI (closes M-OKX)

**Milestone**: M-OKX
**Wave**: 10
**Group**: 10
**Status**: PLANNED
**Depends on**: TASK-012, TASK-014
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#okx (spot REST parity; DeltaMapper mandate); research#architectural-implication (OKX validates the generalized abstraction)
**Blast radius**: HIGH — modifies shared DI `ServiceCollectionExtensions`/`CryptoExchangesOptions` (architect + api review); defines public `OkxExchangeClient`; closes the OKX milestone.

## Description
Complete OKX to spot-REST parity and ship it. Implement the three services, `OkxMappingProfiles` (DeltaMapper DTO→model), `OkxClientComposer`, public `OkxExchangeClient` (Create/CreateFromEnvironment/SyncServerTimeAsync), `OkxErrorTranslator` (OKX `code`/`msg` + sCode envelope → typed exceptions; 401/403/429 mapping) and `OkxTimeSync`. Wire `AddOkxExchange` into the DI project (keyed-by-`ExchangeId.Okx` singletons, named HttpClient, `ApplyResiliencePipeline` with Okx translator/gate, and a `requestFinalizerFactory` returning `PassThroughHandler` when secret/passphrase absent else `OkxSigningHandler`). Extend `CryptoExchangesOptions` + `AddCryptoExchanges` for OKX (including passphrase). Author OKX unit + integration test projects. This closes the OKX milestone.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Okx/Services/OkxMarketDataService.cs`
- `src/CryptoExchanges.Net.Okx/Services/OkxTradingService.cs`
- `src/CryptoExchanges.Net.Okx/Services/OkxAccountService.cs`
- `src/CryptoExchanges.Net.Okx/Mapping/OkxMappingProfiles.cs`
- `src/CryptoExchanges.Net.Okx/Internal/OkxClientComposer.cs`
- `src/CryptoExchanges.Net.Okx/OkxExchangeClient.cs`
- `src/CryptoExchanges.Net.Okx/Resilience/OkxErrorTranslator.cs`
- `src/CryptoExchanges.Net.Okx/Resilience/OkxTimeSync.cs`
- `tests/CryptoExchanges.Net.Okx.Tests.Unit/CryptoExchanges.Net.Okx.Tests.Unit.csproj`
- `tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxSigningTests.cs`
- `tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxMappingAndServiceTests.cs`
- `tests/CryptoExchanges.Net.Okx.Tests.Integration/CryptoExchanges.Net.Okx.Tests.Integration.csproj`
- `tests/CryptoExchanges.Net.Okx.Tests.Integration/OkxPipelineEndToEndTests.cs`
### Modifies
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs`
- `CryptoExchanges.Net.sln`

## Files modified
- src/CryptoExchanges.Net.Okx/Services/OkxMarketDataService.cs
- src/CryptoExchanges.Net.Okx/Services/OkxTradingService.cs
- src/CryptoExchanges.Net.Okx/Services/OkxAccountService.cs
- src/CryptoExchanges.Net.Okx/Mapping/OkxMappingProfiles.cs
- src/CryptoExchanges.Net.Okx/Internal/OkxClientComposer.cs
- src/CryptoExchanges.Net.Okx/OkxExchangeClient.cs
- src/CryptoExchanges.Net.Okx/Resilience/OkxErrorTranslator.cs
- src/CryptoExchanges.Net.Okx/Resilience/OkxTimeSync.cs
- tests/CryptoExchanges.Net.Okx.Tests.Unit/CryptoExchanges.Net.Okx.Tests.Unit.csproj
- tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxSigningTests.cs
- tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxMappingAndServiceTests.cs
- tests/CryptoExchanges.Net.Okx.Tests.Integration/CryptoExchanges.Net.Okx.Tests.Integration.csproj
- tests/CryptoExchanges.Net.Okx.Tests.Integration/OkxPipelineEndToEndTests.cs
- src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs
- CryptoExchanges.Net.sln

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/Services/*`, `Mapping/BinanceMappingProfiles.cs`, `Internal/BinanceClientComposer.cs:16-87`
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceErrorTranslator.cs:19-24`, `BinanceTimeSync.cs`
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:35-165` (AddBinanceExchange + secret-gated finalizer + CryptoExchangesOptions); finalizer must also gate on Passphrase presence
- `tests/CryptoExchanges.Net.Binance.Tests.Integration/BinancePipelineEndToEndTests.cs:40-50`

## Acceptance Criteria
1. `dotnet test --filter 'Category!=Integration'` passes; `AddOkxExchange`/`AddCryptoExchanges` resolve a working `IExchangeClient` keyed by `ExchangeId.Okx`; finalizer is `PassThroughHandler` when SecretKey or Passphrase is missing.
2. Services return DeltaMapper-mapped domain models (`AssertConfigurationIsValid` passes); OKX error codes + 401/403/429 map to the correct typed exceptions; only `OkxExchangeClient`/`OkxOptions` are public.
3. Binance and Bybit registrations/tests are unaffected; full solution builds clean under TreatWarningsAsErrors.

## Test Requirements
- This IS the test task for OKX. Coverage: base64 signature vector, prehash assembly (GET path+query vs POST body, ISO timestamp), four-header signing + re-sign-on-retry (stub handler), passphrase-missing fast-fail, hyphen symbol round-trip, parsers, validation, per-service DeltaMapper mapping, error mapping, time sync, and DI resolution.

## Base SHA
- f03c2092318332a0dc16215fdae7e7b9ac25cc43

## Implementation Notes

### Services + DTOs
- **OkxMarketDataService** (`/api/v5/market/tickers`, `/ticker`, `/books`, `/candles`, `/trades`, `/public/instruments`). Single-symbol tickers use `/ticker?instId=`; full universe uses `/tickers?instType=SPOT` with per-row skip on unresolvable symbols (mirrors Bybit `TryMapTicker`). Lazy cached supported-symbol set for opt-in `IsSupportedAsync`/`ResolveSymbolAsync`. Order book/candles/trades built directly (Binance/Bybit-precedent exception to DeltaMapper). Book/candles/trades limits clamped (books≤400, candles≤100, trades≤500).
- **OkxTradingService** (`/api/v5/trade/order` POST place + GET single, `/cancel-order`, `/cancel-batch-orders`, `/orders-pending`, `/orders-history`). Spot uses `tdMode=cash`. Place/cancel return id-only acks → re-fetch full order via GET `/api/v5/trade/order` (by ordId, falling back to clOrdId when the ack omits ordId — TASK-006 cancel-by-clientId precedent). `CancelAllOrdersAsync` enumerates open orders then batch-cancels (OKX has no symbol-scoped cancel-all).
- **OkxAccountService** (`/api/v5/account/balance` → `data[].details[]`, `/api/v5/trade/fills`). Balances trimmed to non-zero. Fills→Trade built directly; IsBuyerMaker derived from side + execType (M=maker/T=taker). History limit clamped to 100.

### Place-order POST-body decision (TASK-014 carry)
OKX `/api/v5/trade/order` body is a JSON OBJECT whose spot fields are ALL scalar strings (instId, tdMode, side, ordType, sz, px, clOrdId, tgtCcy). The existing `IOkxHttpClient.PostAsync(Dictionary<string,string>)` serializes exactly that flat object as the verbatim wire body the signer reads back, so it is sufficient for single-order placement — no nested/typed body needed there. HOWEVER `/api/v5/trade/cancel-batch-orders` takes a JSON ARRAY of `{instId,ordId}`; to send that verbatim I ADDED a typed object-body overload `PostAsync<T>(string endpoint, object body, bool signed, ct)` (factored both POST paths through a shared `PostJsonAsync`). Market orders send NO `px` (OKX rejects px on market); market-buy-by-quote sets `sz`=quote value + `tgtCcy=quote_ccy`.

### DeltaMapper profile (OkxResponseProfile)
DTO→domain maps for OkxOrder→Order, OkxTicker→Ticker, OkxInstrument→SymbolInfo, OkxBalanceDetail→AssetBalance. All decimal/enum/timestamp conversions via `OkxValueParsers` + `ISymbolMapper.FromWire`/`FromComponents`. `AssertConfigurationIsValid()` invoked in `OkxClientComposer.CreateMapper` (unit-tested). Notable: `CumulativeQuoteQuantity` = accFillSz × avgPx (OKX exposes no direct quote-filled field); ticker `PriceChangePercent` computed from `(last-open24h)/open24h*100` (OKX has no fractional-change field), with a divide-by-zero guard; `TimeInForce` and `Type` both key off `ordType` (so both parsers accept every value incl. "market"). No AutoMapper, no hand-rolled mapping in services except the documented Trade/OrderBook/Candlestick direct-build precedent.

### Error-code map (OkxErrorTranslator, INTERNAL)
OKX success is `code == "0"` (STRING) → never an error. Inspects top-level `code`/`msg` AND per-order `data[0].sCode`/`sMsg` (used when top-level code is "0"/absent — OKX rejects individual orders that way). Mappings (real OKX V5 codes, conservative): 429 / 50011 / 50013 → RateLimitExceeded (RetryAfter from headers); 401/403 + 50100–50105 / 50111–50114 → Authentication (passphrase/timestamp/signature family); 51008/51119/51131 → InsufficientBalance; 51000/51001/51002/51005/51006/51020/51400/51402/51503 → InvalidOrder; everything else → ExchangeApiException. ValueKind-guarded JSON reads (no InvalidOperationException escape, per code-reviewer rule); numeric `code` is parsed to the typed exception's int? where possible.

### Time sync (OkxTimeSync, INTERNAL)
`ComputeOffset`/`ApplyOffset` (server-local ms) with the zero-length holder guard (TASK-007 precedent). `OkxExchangeClient.SyncServerTimeAsync` reads `/api/v5/public/time` `data[0].ts` (epoch-ms string) and Interlocked-writes the shared `long[]` holder. The handler does `UtcNow.AddMilliseconds(offset)` then `OkxSignatureService.FormatTimestamp` (ISO-8601) — offset stays in ms, consistent with Bybit.

### DI wiring + AddOkxExchange location (ADR-001)
`AddOkxExchange` lives IN the OKX assembly (`src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs`), mirroring `AddBybitExchange`. Keyed-by-`ExchangeId.Okx` singletons (offset holder, ISymbolMapper, IMapper, IExchangeClient), named HttpClient "okx" with host-only BaseAddress, `ApplyResiliencePipeline` (Okx translator + ReactiveRateLimitGate). **Secret+passphrase-gated finalizer**: returns `PassThroughHandler` when SecretKey OR Passphrase is missing/empty, else `OkxSigningHandler(apiKey, passphrase, new OkxSignatureService(secret), () => Interlocked.Read(holder))` — strings passed directly, so `OkxOptions.ToCredentials()` (throws on empty passphrase) is NEVER called in the signing path (resolves TASK-010 carry-in). The thin DI aggregator `CryptoExchanges.Net.DependencyInjection` was extended: `AddCryptoExchanges` now calls `AddOkxExchange`, and `CryptoExchangesOptions` gained `OkxBaseUrl/OkxApiKey/OkxSecretKey/OkxPassphrase`. Added ProjectReference DI→Okx and `InternalsVisibleTo` Http→Okx (for internal `ExchangeClientFactory`).

### Public surface
Confirmed via reflection on the compiled assembly: ONLY `OkxExchangeClient`, `OkxOptions`, and `ServiceCollectionExtensions` (the AddOkxExchange host) are public. OkxErrorTranslator, OkxTimeSync, all services/DTOs, the composer, value parsers, validation, symbol format, signing types — all internal. (OKX ships cleaner than Bybit, whose translator/timesync are public for legacy reasons; ADR-001 conv #2.)

## Verification
- `dotnet build CryptoExchanges.Net.sln` → **0 warnings, 0 errors** (Debug, TreatWarningsAsErrors).
- `dotnet test --filter 'Category!=Integration'` → **ALL pass**. OKX unit = **91** (new). Others unchanged: Core 93, Http 12, Bybit 80, DI 11, Binance(unit) 45. No Binance/Bybit regression.
- `dotnet test --filter 'Category=Integration'` → **ALL pass**. OKX integration = **6** (new), Bybit 5/5 unchanged.
- Public surface confirmed (reflection): only `OkxExchangeClient` + `OkxOptions` (+ the AddOkxExchange `ServiceCollectionExtensions`) among new OKX types.

### Test counts
- OKX Unit (OkxSigningTests + OkxMappingAndServiceTests): 91 tests — base64 sig vector + Core-HMAC agreement, GET/POST prehash assembly, ISO-8601 timestamp (incl. tz→UTC), BTC-USDT round-trip, parsers + validation, error-code→exception (incl. code=="0" not-an-error + per-order sCode), time-sync + zero-length guard, per-service DeltaMapper mapping over mocked IOkxHttpClient, MARKET-ORDER round-trip regression, DI resolution (keyed client; secretless OR passphraseless → PassThrough/still-resolves; AddCryptoExchanges resolves OKX+Bybit+Binance).
- OKX Integration (OkxPipelineEndToEndTests, [Trait Category=Integration]): 6 tests — four OK-ACCESS-* headers on signed GET+POST with base64 sign, re-sign-on-retry (SeqStub 500→200) single fresh header set, passphrase-missing fast-fail, secretless/passphraseless → PassThrough emits no OK-ACCESS-SIGN, unsigned adds no auth headers.

## Commits
- (to be appended after commit)
