---
status: PLANNED
---
# TASK-083: AddOkxStreams() DI extension + DI wiring tests

## Metadata
- **ID**: TASK-083
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-081, TASK-082
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Okx/StreamServiceCollectionExtensions.cs, tests/CryptoExchanges.Net.Okx.Tests.Unit/Streaming/OkxStreamDiTests.cs]
- **Wave**: 9
- **Traces to**: PRD AC#2 + Feature "DI opt-in"; TRD §"Per-Exchange Variation Points" §5; ADR-009-001
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Add the public opt-in `AddOkxStreams()` in `src/CryptoExchanges.Net.Okx/StreamServiceCollectionExtensions.cs`
(public static class, OKX root namespace), cloning the Binance/Bybit DI extension. Delegates to the
shared `StreamServiceRegistration.AddStreams<OkxStreamOptions>(services, ExchangeId.Okx, protocolFactory,
decoderRegistryFactory, configure)` — supply only the OKX protocol + decoder factories. Resolve the
keyed `IMapper`/`ISymbolMapper` for `ExchangeId.Okx` from `sp`. Must be called after `AddOkxExchange`.
Opt-in (REST-only consumers pay nothing). Full XML docs. Zero change to `IStreamClient`/`IStreamClientFactory`.

Tests (`OkxStreamDiTests.cs`, mirroring `BinanceStreamDiTests.cs`, TEST-PLAN §File 3) with in-process
`AddOkxExchange(...)+AddOkxStreams()`, no network:
- `AddOkxStreams_ResolvesStreamClientFactory` — factory not null.
- `AddOkxStreams_FactoryGetClient_ReturnsOkxClient` — `factory.GetClient(ExchangeId.Okx).ExchangeId == ExchangeId.Okx`.
- `AddOkxStreams_AvailableExchanges_ContainsOkx` — `factory.Available` contains `ExchangeId.Okx`.

### Steps
1. Read `BinanceStreamServiceCollectionExtensions` + Bybit's (just-built) and the shared `AddStreams<TOptions>()` signature (read-only).
2. Implement `AddOkxStreams` delegating to the shared body; resolve keyed `IMapper`/`ISymbolMapper` for `ExchangeId.Okx`.
3. Write `OkxStreamDiTests.cs` (3 cases).
4. `dotnet build` 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Acceptance Criteria
- [ ] `AddOkxStreams(this IServiceCollection, Action<OkxStreamOptions>?)` (public, XML-documented) delegates to `StreamServiceRegistration.AddStreams<OkxStreamOptions>(...)` supplying only protocol + decoder factories; resolves keyed `IMapper`/`ISymbolMapper` for `ExchangeId.Okx`; no change to `IStreamClient`/`IStreamClientFactory`.
- [ ] `OkxStreamDiTests.cs` (3 cases) passes using in-process `AddOkxExchange()+AddOkxStreams()`, mirroring `BinanceStreamDiTests.cs`.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- DI extension: `src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs`, `src/CryptoExchanges.Net.Bybit/StreamServiceCollectionExtensions.cs`
- Shared registration body (read-only): `src/CryptoExchanges.Net.Http/Streaming/StreamServiceRegistration.cs`
- DI tests: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDiTests.cs`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Okx/StreamServiceCollectionExtensions.cs
- tests/CryptoExchanges.Net.Okx.Tests.Unit/Streaming/OkxStreamDiTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #2 + "DI opt-in" core feature
- **TRD Component**: OKX variation §5 (StreamServiceCollectionExtensions)
- **ADR Reference**: ADR-009-001

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
