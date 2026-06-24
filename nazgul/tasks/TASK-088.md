---
status: PLANNED
---
# TASK-088: AddBitgetStreams() DI extension + DI wiring tests

## Metadata
- **ID**: TASK-088
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-086, TASK-087
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Bitget/StreamServiceCollectionExtensions.cs, tests/CryptoExchanges.Net.Bitget.Tests.Unit/Streaming/BitgetStreamDiTests.cs]
- **Wave**: 14
- **Traces to**: PRD AC#2 + Feature "DI opt-in"; TRD §"Per-Exchange Variation Points" §5; ADR-009-001
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Add the public opt-in `AddBitgetStreams()` in `src/CryptoExchanges.Net.Bitget/StreamServiceCollectionExtensions.cs`
(public static class, Bitget root namespace), cloning the Binance/Bybit/OKX DI extension. Delegates to
the shared `StreamServiceRegistration.AddStreams<BitgetStreamOptions>(services, ExchangeId.Bitget,
protocolFactory, decoderRegistryFactory, configure)` — supply only the Bitget protocol + decoder
factories. Resolve the keyed `IMapper`/`ISymbolMapper` for `ExchangeId.Bitget`. Must be called after
`AddBitgetExchange`. Opt-in (REST-only consumers pay nothing). Full XML docs. Zero change to
`IStreamClient`/`IStreamClientFactory`.

Tests (`BitgetStreamDiTests.cs`, mirroring `BinanceStreamDiTests.cs`, TEST-PLAN §File 3) with
in-process `AddBitgetExchange(...)+AddBitgetStreams()`, no network:
- `AddBitgetStreams_ResolvesStreamClientFactory` — factory not null.
- `AddBitgetStreams_FactoryGetClient_ReturnsBitgetClient` — `factory.GetClient(ExchangeId.Bitget).ExchangeId == ExchangeId.Bitget`.
- `AddBitgetStreams_AvailableExchanges_ContainsBitget` — `factory.Available` contains `ExchangeId.Bitget`.

### Steps
1. Read `BinanceStreamServiceCollectionExtensions` + the just-built OKX one, and the shared `AddStreams<TOptions>()` signature (read-only).
2. Implement `AddBitgetStreams` delegating to the shared body; resolve keyed `IMapper`/`ISymbolMapper` for `ExchangeId.Bitget`.
3. Write `BitgetStreamDiTests.cs` (3 cases).
4. `dotnet build` 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Acceptance Criteria
- [ ] `AddBitgetStreams(this IServiceCollection, Action<BitgetStreamOptions>?)` (public, XML-documented) delegates to `StreamServiceRegistration.AddStreams<BitgetStreamOptions>(...)` supplying only protocol + decoder factories; resolves keyed `IMapper`/`ISymbolMapper` for `ExchangeId.Bitget`; no change to `IStreamClient`/`IStreamClientFactory`.
- [ ] `BitgetStreamDiTests.cs` (3 cases) passes using in-process `AddBitgetExchange()+AddBitgetStreams()`, mirroring `BinanceStreamDiTests.cs`.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- DI extension: `src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs`, `src/CryptoExchanges.Net.Okx/StreamServiceCollectionExtensions.cs`
- Shared registration body (read-only): `src/CryptoExchanges.Net.Http/Streaming/StreamServiceRegistration.cs`
- DI tests: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDiTests.cs`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Bitget/StreamServiceCollectionExtensions.cs
- tests/CryptoExchanges.Net.Bitget.Tests.Unit/Streaming/BitgetStreamDiTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #2 + "DI opt-in" core feature
- **TRD Component**: Bitget variation §5 (StreamServiceCollectionExtensions)
- **ADR Reference**: ADR-009-001

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
