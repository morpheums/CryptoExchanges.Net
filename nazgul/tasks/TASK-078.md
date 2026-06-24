---
status: IMPLEMENTED
---
# TASK-078: AddBybitStreams() DI extension + DI wiring tests

## Metadata
- **ID**: TASK-078
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-076, TASK-077
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Bybit/StreamServiceCollectionExtensions.cs, tests/CryptoExchanges.Net.Bybit.Tests.Unit/Streaming/BybitStreamDiTests.cs]
- **Wave**: 4
- **Traces to**: PRD AC#1/#2 + Feature "DI opt-in"; TRD §"Per-Exchange Variation Points" §5; ADR-009-001
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**: 2026-06-24T08:00:00Z
- **Base SHA**: c34ce57aeb3b1797f429cbb7f7a490323285da31
- **Implemented at**: 2026-06-24T08:10:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Add the public opt-in DI extension `AddBybitStreams()` in
`src/CryptoExchanges.Net.Bybit/StreamServiceCollectionExtensions.cs` (public static class in
the Bybit root namespace), cloning `Binance/StreamServiceCollectionExtensions.cs` exactly. It
delegates to the shared `StreamServiceRegistration.AddStreams<BybitStreamOptions>(...)` body —
do NOT reimplement registration; supply only the protocol factory and decoder-registry factory:

```
public static IServiceCollection AddBybitStreams(
    this IServiceCollection services,
    Action<BybitStreamOptions>? configure = null) =>
    StreamServiceRegistration.AddStreams<BybitStreamOptions>(
        services, ExchangeId.Bybit,
        protocolFactory: sp => new BybitStreamProtocol(/* options + BybitSymbolFormat */),
        decoderRegistryFactory: sp => BybitStreamDecoders.Build(/* keyed IMapper */, /* keyed ISymbolMapper */),
        configure: configure);
```

Must be called AFTER `AddBybitExchange` so the keyed `IMapper` and `ISymbolMapper` are already
in the container (resolve them from `sp` keyed by `ExchangeId.Bybit`, matching how Binance does
it). Opt-in: REST-only consumers who never call this pay nothing. Full XML docs on the public
method. Zero change to `IStreamClient`/`IStreamClientFactory`.

Tests (`BybitStreamDiTests.cs`, mirroring `BinanceStreamDiTests.cs`, TEST-PLAN §File 3) use an
in-process `ServiceCollection` with `AddBybitExchange(...)` + `AddBybitStreams()`, no network:
- `AddBybitStreams_ResolvesStreamClientFactory` — `sp.GetService<IStreamClientFactory>()` not null.
- `AddBybitStreams_FactoryGetClient_ReturnsBybitClient` — `factory.GetClient(ExchangeId.Bybit).ExchangeId == ExchangeId.Bybit`.
- `AddBybitStreams_AvailableExchanges_ContainsBybit` — `factory.Available` contains `ExchangeId.Bybit`.

### Steps
1. Read `src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs` + `src/CryptoExchanges.Net.Kucoin/StreamServiceCollectionExtensions.cs` and the shared `StreamServiceRegistration.AddStreams<TOptions>()` signature (read-only) to copy the factory wiring + keyed resolution exactly.
2. Implement `AddBybitStreams` delegating to the shared body; resolve keyed `IMapper`/`ISymbolMapper` for `ExchangeId.Bybit`.
3. Write `BybitStreamDiTests.cs` (3 cases) mirroring `BinanceStreamDiTests.cs`.
4. `dotnet build` 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Acceptance Criteria
- [x] `AddBybitStreams(this IServiceCollection, Action<BybitStreamOptions>?)` (public, XML-documented) delegates to `StreamServiceRegistration.AddStreams<BybitStreamOptions>(...)` supplying only protocol + decoder factories; resolves keyed `IMapper`/`ISymbolMapper` for `ExchangeId.Bybit`; no change to `IStreamClient`/`IStreamClientFactory`.
- [x] `BybitStreamDiTests.cs` (3 cases: factory resolves, GetClient returns Bybit client, Available contains Bybit) passes using in-process `AddBybitExchange()+AddBybitStreams()`, mirroring `BinanceStreamDiTests.cs`.
- [x] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- DI extension: `src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs`, `src/CryptoExchanges.Net.Kucoin/StreamServiceCollectionExtensions.cs`
- Shared registration body (read-only): `src/CryptoExchanges.Net.Http/Streaming/StreamServiceRegistration.cs`
- DI tests: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDiTests.cs`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Bybit/StreamServiceCollectionExtensions.cs
- tests/CryptoExchanges.Net.Bybit.Tests.Unit/Streaming/BybitStreamDiTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #1, #2 + "DI opt-in" core feature
- **TRD Component**: Bybit variation §5 (StreamServiceCollectionExtensions)
- **ADR Reference**: ADR-009-001

## Implementation Log

### Attempt 1

**Files created:**
- `src/CryptoExchanges.Net.Bybit/StreamServiceCollectionExtensions.cs` — public `AddBybitStreams()` delegating to `StreamServiceRegistration.AddStreams<BybitStreamOptions>`. Mirrors Binance pattern exactly: resolves `BybitStreamOptions` for `BybitStreamProtocol`; resolves keyed `IMapper`/`ISymbolMapper` for `ExchangeId.Bybit` to call `BybitStreamDecoders.Build`. Full XML docs on the public method.
- `tests/CryptoExchanges.Net.Bybit.Tests.Unit/Streaming/BybitStreamDiTests.cs` — 3 DI wiring tests mirroring `BinanceStreamDiTests.cs`: factory resolves, GetClient returns keyed Bybit client, Available contains ExchangeId.Bybit.

**Build:** 0W/0E (`dotnet build CryptoExchanges.Net.sln`)
**Tests:** 127 Bybit unit tests passed (3 new DI tests + 124 pre-existing)

## Commits

- `f9515ce` — feat(FEAT-009): TASK-078 AddBybitStreams() DI extension + DI wiring tests

## Review Results

### Attempt 1
