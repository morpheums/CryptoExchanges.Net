---
id: TASK-006
status: PLANNED
---

# TASK-006: Bybit services + mapping + composer + ExchangeClient

**Milestone**: M-BYBIT
**Wave**: 4
**Group**: 4
**Status**: PLANNED
**Depends on**: TASK-005
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (spot REST parity: market data, trading, account); DeltaMapper mandate
**Blast radius**: MEDIUM — multiple new files; defines the public `BybitExchangeClient` API surface (api-reviewer).

## Description
Implement the three services (`BybitMarketDataService`, `BybitTradingService`, `BybitAccountService`) against `IMarketDataService`/`ITradingService`/`IAccountService`, the DeltaMapper `BybitMappingProfiles` (Bybit DTO → domain models via `IMapper`, using `BybitValueParsers` + `ISymbolMapper.FromWire`), the `BybitClientComposer` (`ComposeForDi`, `ComposeWith`, `Create`, `BuildResilientHttpClient`, `CreateMapper`), and the public `BybitExchangeClient` (`Create(BybitOptions)`, `CreateFromEnvironment()`, `SyncServerTimeAsync()`). Mapping MUST use DeltaMapper — no AutoMapper, no manual mapping.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bybit/Services/BybitMarketDataService.cs`
- `src/CryptoExchanges.Net.Bybit/Services/BybitTradingService.cs`
- `src/CryptoExchanges.Net.Bybit/Services/BybitAccountService.cs`
- `src/CryptoExchanges.Net.Bybit/Mapping/BybitMappingProfiles.cs`
- `src/CryptoExchanges.Net.Bybit/Internal/BybitClientComposer.cs`
- `src/CryptoExchanges.Net.Bybit/BybitExchangeClient.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bybit/Services/BybitMarketDataService.cs
- src/CryptoExchanges.Net.Bybit/Services/BybitTradingService.cs
- src/CryptoExchanges.Net.Bybit/Services/BybitAccountService.cs
- src/CryptoExchanges.Net.Bybit/Mapping/BybitMappingProfiles.cs
- src/CryptoExchanges.Net.Bybit/Internal/BybitClientComposer.cs
- src/CryptoExchanges.Net.Bybit/BybitExchangeClient.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/Services/BinanceMarketDataService.cs` (+ Trading/Account services)
- `src/CryptoExchanges.Net.Binance/Mapping/BinanceMappingProfiles.cs` (DeltaMapper profile bound to ISymbolMapper)
- `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs:16-87` (CreateMapper, Create, ComposeOver/With/ForDi, BuildResilientHttpClient)
- `BinanceExchangeClient` public factory shape (architecture-map.md:43-44)

## Acceptance Criteria
1. All three services implement their Core interfaces and return mapped domain models (`Ticker`, `Order`, `AssetBalance`, etc.); `MapperConfiguration.AssertConfigurationIsValid()` passes in `CreateMapper`.
2. `BybitExchangeClient.Create(BybitOptions)` composes a working client over a secret-gated signing finalizer (PassThrough when secretless), mirroring Binance's `BuildResilientHttpClient`.
3. Only `BybitExchangeClient` and `BybitOptions` are public; all internals are non-public; build is clean under TreatWarningsAsErrors with full XML docs.

## Test Requirements
- Unit tests (TASK-008) mock `IBybitHttpClient` to verify each service maps a representative DTO payload correctly via DeltaMapper.
