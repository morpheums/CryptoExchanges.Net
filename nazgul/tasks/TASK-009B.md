---
id: TASK-009B
status: PLANNED
---

# TASK-009B: Per-exchange DI re-homing (ADR-001)

**Milestone**: M-OKX
**Wave**: 6
**Group**: 6
**Status**: PLANNED
**Depends on**: TASK-008 (M-BYBIT shipped)
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: ADR-001 (`nazgul/docs/ADR-001-per-exchange-di-and-conventions.md`) — maintainer-raised DI coupling decision, architect-confirmed.
**Blast radius**: HIGH — restructures the shared DI package + touches Binance/Bybit assemblies. Public-API/namespace move (breaking, acceptable pre-v1.0). REQUIRES architect + api review.

## Description
Adopt per-exchange DI registration (ADR-001 option b). Move `AddBinanceExchange` and `AddBybitExchange` out of `CryptoExchanges.Net.DependencyInjection` and INTO their respective exchange assemblies (`CryptoExchanges.Net.Binance`, `CryptoExchanges.Net.Bybit`), each as its own `ServiceCollectionExtensions`. Reduce the DI package to a thin `AddCryptoExchanges` aggregator + `CryptoExchangesOptions` that delegates to the per-exchange extensions. Establish the pattern so OKX/Bitget ship `AddOkxExchange`/`AddBitgetExchange` in-assembly from day one. Fold in two pre-tracked follow-ups while in this area.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs` (AddBinanceExchange moved here)
- `src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs` (AddBybitExchange moved here)
### Modifies
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs` (reduce to thin AddCryptoExchanges aggregator + CryptoExchangesOptions; delegate to per-exchange methods; optional `[Obsolete]` forwarders for one minor version)
- `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj` + `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj` (add Microsoft.Extensions.DependencyInjection.Abstractions + Microsoft.Extensions.Http package refs now needed by the moved registration)
- `src/CryptoExchanges.Net.DependencyInjection/CryptoExchanges.Net.DependencyInjection.csproj` (keep refs to exchanges for the aggregator)
- Binance signing types → `internal` (follow-up: harmonize with Bybit; verify InternalsVisibleTo covers DI + tests)
- `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs` (follow-up: back-fill `ThrowIfNullOrWhiteSpace(endpoint)` guard)
- Tests: move/adjust DI resolution tests as needed; keep coverage.

## Pattern Reference
- Current `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs` (the AddBinanceExchange/AddBybitExchange bodies to relocate verbatim, fixing namespaces)
- `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj` InternalsVisibleTo block (DI package must retain access to internal composers/handlers from the exchange assembly)

## Acceptance Criteria
1. `CryptoExchanges.Net.Bybit` exposes `AddBybitExchange`; `CryptoExchanges.Net.Binance` exposes `AddBinanceExchange`; both compile against their own assembly's internals (no behavior change — same keyed singletons, named client, resilience pipeline, secret-gated finalizer, ValidateOnStart).
2. `CryptoExchanges.Net.DependencyInjection.AddCryptoExchanges` still registers all exchanges by delegating to the per-exchange extensions; existing `AddCryptoExchanges` callers need no change. If `[Obsolete]` forwarders are kept for `AddBinanceExchange`/`AddBybitExchange` in the DI namespace, they compile with a warning-as-message (not error).
3. A consumer referencing ONLY `CryptoExchanges.Net.Bybit` can call `AddBybitExchange` WITHOUT pulling in the Binance assembly (verify the dependency graph: Binance is no longer a transitive requirement for a Bybit-only consumer).
4. Binance signing types are `internal` (matching Bybit); `BinanceHttpClient` has the `endpoint` guard. No Binance behavior regression.
5. Full solution builds clean (TreatWarningsAsErrors); all tests pass; no Binance/Bybit functional regression.

## Test Requirements
- DI resolution tests still pass (keyed IExchangeClient per ExchangeId; secretless → PassThrough finalizer; AddCryptoExchanges resolves all). Add a check that the Bybit-only path doesn't require Binance types.

## Notes
- Sequencing: independent of TASK-009 (Core auth) at the file level — may run in parallel, but both are HIGH blast radius; prefer landing TASK-009 first, then this, to keep review diffs focused.
- This is the last good moment to do this cheaply (2 exchanges, not 4) — see ADR-001 rationale.
