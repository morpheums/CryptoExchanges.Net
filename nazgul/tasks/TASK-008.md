---
id: TASK-008
status: IMPLEMENTED
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

## Implementation Notes

### Part A — DI wiring
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs`: added `AddBybitExchange(this IServiceCollection, Action<BybitOptions>?)` mirroring `AddBinanceExchange` exactly — keyed-by-`ExchangeId.Bybit` singletons (`long[]` offset holder, `ISymbolMapper` from `BybitSymbolFormat.Instance`, `IMapper` from `BybitClientComposer.CreateMapper`, `IExchangeClient` via `BybitClientComposer.ComposeForDi`), a NAMED HttpClient (`"bybit"`), and `http.ApplyResiliencePipeline(...)` with `UsageHeaderName = "X-Bapi-Limit-Status"`, `translatorFactory: _ => new BybitErrorTranslator()`, `gateFactory: _ => new ReactiveRateLimitGate()`, and a `requestFinalizerFactory` returning `PassThroughHandler` when secretless else `BybitSigningHandler(o.ApiKey, new BybitSignatureService(o.SecretKey), o.ReceiveWindow.ToString(CultureInfo.InvariantCulture), () => Interlocked.Read(ref holder[0]))`.
- Added `ApplyEnvDefaults(BybitOptions)` (BYBIT_API_KEY / BYBIT_SECRET_KEY) and extended `CryptoExchangesOptions` (+ `AddCryptoExchanges`) with `BybitBaseUrl`/`BybitApiKey`/`BybitSecretKey`.
- `src/CryptoExchanges.Net.DependencyInjection/CryptoExchanges.Net.DependencyInjection.csproj`: added a `ProjectReference` to `CryptoExchanges.Net.Bybit` (the DI project previously referenced only Binance/Core/Http; the Bybit csproj already grants it `InternalsVisibleTo`, so internal signing/composer/symbol-format types resolve).

### Part B — Test projects
- `tests/CryptoExchanges.Net.Bybit.Tests.Unit/` (csproj + `BybitSigningTests.cs` + `BybitMappingAndServiceTests.cs`): 77 tests.
  - `BybitSigningTests.cs`: HMAC-SHA256 fixed vectors, GET/POST sign-string assembly + ordering, blank/null guards, symbol round-trip via `new SymbolMapper(BybitSymbolFormat.Instance)` (ToWire(BTC/USDT)=="BTCUSDT" + FromWire round-trip), `BybitValueParsers` invariants + malformed-input rejection, `BybitRequestValidation` (limit<1, limit>50, inverted window, 7-day window edge), `BybitTimeSync` offset sign/magnitude + ApplyOffset holder write + zero-length-holder `ArgumentException`, and `BybitErrorTranslator` retCode/HTTP-status → exception mapping (auth, rate-limit w/ RetryAfter, insufficient-balance, invalid-order, unknown→ExchangeApiException, retCode==0 not a typed error).
  - `BybitMappingAndServiceTests.cs`: per-service DeltaMapper mapping over NSubstitute-mocked `IBybitHttpClient` (Order/Ticker/Instrument/Balance profiles + MarketData/Account/Trading representative V5 payloads); the two ROUND-1 REGRESSION tests — (a) `GetOrderHistoryAsync`/`GetTradeHistoryAsync` with the default limit (500) do NOT throw and send `limit=50` (clamped); (b) `CancelOrderByClientIdAsync` when the cancel ACK omits orderId re-fetches by `orderLinkId` and returns an `Order` with a non-empty id; plus DI resolution tests (`AddBybitExchange` keyed client, secretless still resolves, keyed-singleton mapper, fail-fast on invalid options, scope-clean graph, and `AddCryptoExchanges` resolving both Bybit and Binance).
- `tests/CryptoExchanges.Net.Bybit.Tests.Integration/` (csproj + `BybitPipelineEndToEndTests.cs`): 5 tests, all marked at the class level with `[Trait("Category", "Integration")]`. Covers signed GET/POST X-BAPI-* header presence/values, unsigned request gets api-key header only, re-sign-on-retry producing a fresh timestamp with a SINGLE (not doubled) X-BAPI-* header set via a `SeqStub` (500→200) over `HttpClientPipelineBuilder.Build`, and the secretless `PassThroughHandler` path emitting NO `X-BAPI-SIGN`. Uses `TestContext.Current.CancellationToken`.
- `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj`: added `InternalsVisibleTo` for `CryptoExchanges.Net.Bybit.Tests.Unit` (the unit project needs internal signature service/services/value parsers/request validation/symbol format/composer) and for `DynamicProxyGenAssembly2` (so NSubstitute/Castle DynamicProxy can mock the internal `IBybitHttpClient`).
- `CryptoExchanges.Net.sln`: registered both test projects with full Debug/Release (Any CPU/x64/x86) configs and nested them under the `tests` solution folder.

### Integration-category mechanism
- xUnit `[Trait("Category", "Integration")]` at the class level on `BybitPipelineEndToEndTests`; `dotnet test --filter 'Category!=Integration'` excludes them, `--filter 'Category=Integration'` runs only them. (The existing Binance integration tests are not category-marked and continue to run under the default filter — unchanged, no regression.)

### Verification
- `dotnet build CryptoExchanges.Net.sln` → **0 Warning(s), 0 Error(s)** (Debug and Release, TreatWarningsAsErrors).
- `dotnet test CryptoExchanges.Net.sln --filter 'Category!=Integration'` → **all pass, 0 failures**: Core 68, Http 12, Bybit.Unit 77, DI.Unit 10, Binance.Integration 45 = **212 total** (baseline 135 + 77 new Bybit unit).
- `dotnet test CryptoExchanges.Net.sln --filter 'Category=Integration'` → **Bybit.Integration 5/5 pass**, all other assemblies report "No test matches".
- No Binance regression: Binance integration (45) and DI unit (10) tests unchanged and passing.

## Commits
- **Commit**: f60bd18 feat(M2): TASK-008 Bybit tests + AddBybitExchange DI (closes M-BYBIT)
