---
id: TASK-015
status: PLANNED
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
