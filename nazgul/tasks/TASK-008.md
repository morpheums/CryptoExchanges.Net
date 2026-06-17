---
id: TASK-008
status: PLANNED
---

# TASK-008: Bybit tests + AddBybitExchange DI (closes M-BYBIT)

**Milestone**: M-BYBIT
**Wave**: 5
**Group**: 5
**Status**: PLANNED
**Depends on**: TASK-003, TASK-006, TASK-007
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (Bybit is the first milestone and must be fully shippable before OKX)
**Blast radius**: MEDIUM — modifies the shared DI `ServiceCollectionExtensions`/`CryptoExchangesOptions` (architect + api review); adds test projects.

## Description
Wire `AddBybitExchange(this IServiceCollection, Action<BybitOptions>?)` into the DI project: keyed-by-`ExchangeId.Bybit` singletons (`long[]` offset holder, `ISymbolMapper`, `IMapper`, `IExchangeClient`), a NAMED HttpClient, and `http.ApplyResiliencePipeline(...)` with `translatorFactory: _ => new BybitErrorTranslator()`, `gateFactory: _ => new ReactiveRateLimitGate()`, and a `requestFinalizerFactory` that returns `PassThroughHandler` when secretless else `BybitSigningHandler`. Extend `CryptoExchangesOptions` + `AddCryptoExchanges` to include Bybit. Create the Bybit unit test project and integration test project (Category=Integration), covering signature vectors, sign-string assembly, signing-header presence + re-sign-on-retry, symbol round-trip, parsers, validation, service mapping, error translation, time sync, and DI resolution. This task closes the Bybit milestone.

## File Scope
### Creates
- `tests/CryptoExchanges.Net.Bybit.Tests.Unit/CryptoExchanges.Net.Bybit.Tests.Unit.csproj`
- `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitSigningTests.cs`
- `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitMappingAndServiceTests.cs`
- `tests/CryptoExchanges.Net.Bybit.Tests.Integration/CryptoExchanges.Net.Bybit.Tests.Integration.csproj`
- `tests/CryptoExchanges.Net.Bybit.Tests.Integration/BybitPipelineEndToEndTests.cs`
### Modifies
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs`
- `CryptoExchanges.Net.sln`

## Files modified
- tests/CryptoExchanges.Net.Bybit.Tests.Unit/CryptoExchanges.Net.Bybit.Tests.Unit.csproj
- tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitSigningTests.cs
- tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitMappingAndServiceTests.cs
- tests/CryptoExchanges.Net.Bybit.Tests.Integration/CryptoExchanges.Net.Bybit.Tests.Integration.csproj
- tests/CryptoExchanges.Net.Bybit.Tests.Integration/BybitPipelineEndToEndTests.cs
- src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs
- CryptoExchanges.Net.sln

## Pattern Reference
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:35-114` (AddBinanceExchange: keyed singletons, named client, ApplyResiliencePipeline, secret-gated finalizer)
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:131-165` (CryptoExchangesOptions + AddCryptoExchanges)
- `tests/CryptoExchanges.Net.Binance.Tests.Integration/BinancePipelineEndToEndTests.cs:40-50` (SeqStub, re-sign-on-retry, TestContext.Current.CancellationToken)

## Acceptance Criteria
1. `dotnet test --filter 'Category!=Integration'` passes; integration tests carry `Category=Integration` and are excluded by default, runnable explicitly.
2. `AddBybitExchange` and `AddCryptoExchanges` resolve a working `IExchangeClient` keyed by `ExchangeId.Bybit`; secretless registration yields a `PassThroughHandler` finalizer (no signing).
3. The Binance registration and its tests are unaffected (no regression); full solution builds clean under TreatWarningsAsErrors.

## Test Requirements
- This IS the test task for Bybit. Coverage: signature hex vector, GET/POST sign-string, header signing + re-sign-on-retry (stub handler), symbol round-trip, value parsers, request validation, per-service DeltaMapper mapping, error-code → exception mapping, time-sync offset, and DI resolution (both `AddBybitExchange` and `AddCryptoExchanges`).
