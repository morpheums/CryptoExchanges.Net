---
id: TASK-022
status: PLANNED
---

# TASK-022: Bitget services + mapping + error + time + tests + AddBitgetExchange DI (closes M-BITGET)

**Milestone**: M-BITGET
**Wave**: 15
**Group**: 15
**Status**: PLANNED
**Depends on**: TASK-019, TASK-021
**Retry count**: 0/3
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
