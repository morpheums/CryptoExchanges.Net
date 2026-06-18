---
id: TASK-009B
status: IMPLEMENTED
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

## Implementation Notes

### What moved where
- `AddBinanceExchange` (+ private `ApplyEnvDefaults(BinanceOptions)` + `ClientName` const) → new `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs`, namespace **`CryptoExchanges.Net.Binance`** (assembly root namespace — same as `BinanceOptions`/`BinanceExchangeClient`, so a single `using CryptoExchanges.Net.Binance;` covers both the options type and the registration method). Moved verbatim; now references Binance internals (composer, signing, HTTP wrapper) directly.
- `AddBybitExchange` (+ private `ApplyEnvDefaults(BybitOptions)` + `BybitClientName` const) → new `src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs`, namespace **`CryptoExchanges.Net.Bybit`**. Same approach.
- DI package `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs` reduced to ONLY `AddCryptoExchanges` + `CryptoExchangesOptions`; it now `using`s `CryptoExchanges.Net.Binance` / `.Bybit` and delegates to their public extensions. No `[Obsolete]` forwarders (would be a build ERROR under TreatWarningsAsErrors) — clean move, acceptable breaking change pre-v1.0 per ADR-001.

### ExchangeClientFactory relocation (necessary, not in original step list)
- `ExchangeClientFactory` was `internal` in the DI assembly but is referenced by BOTH `AddXxxExchange` methods (`TryAddSingleton<IExchangeClientFactory, ExchangeClientFactory>`). For a Bybit-only consumer (no DI-package reference) to register it, the concrete type had to move to a shared lower layer.
- Moved it to **`src/CryptoExchanges.Net.Http/ExchangeClientFactory.cs`**, namespace `CryptoExchanges.Net.Http`, kept `internal`. Http is referenced by every exchange assembly and already pulls DI abstractions transitively (via `Microsoft.Extensions.Http.Resilience`); the factory only needs Abstractions (`GetKeyedService(s)`, `KeyedService.AnyKey`). The `IExchangeClientFactory` interface stays public in Core (unchanged). Deleted the old DI-package copy.

### csproj wiring
- Binance + Bybit csprojs: added `Microsoft.Extensions.DependencyInjection.Abstractions` (Version `10.0.*`). They already had `Microsoft.Extensions.Http` + `Microsoft.Extensions.Options` (used by the moved code).
- DI csproj: dropped `Microsoft.Extensions.Http` + `Microsoft.Extensions.Options` (no longer used directly), added `Microsoft.Extensions.DependencyInjection.Abstractions`. Kept ProjectReferences to Binance + Bybit (aggregator calls their public extensions). Kept ProjectReferences to Core + Http.
- Http csproj: added `InternalsVisibleTo` for `CryptoExchanges.Net.Binance` + `CryptoExchanges.Net.Bybit` (so each `AddXxxExchange` can register the internal `ExchangeClientFactory`).

### InternalsVisibleTo cleanup
- Removed `InternalsVisibleTo("CryptoExchanges.Net.DependencyInjection")` from BOTH Binance and Bybit csprojs — the DI package no longer touches exchange internals (only calls public `AddXxxExchange`). Verified by clean build. Retained: Binance test-integration IVT; Bybit unit + integration test IVTs + `DynamicProxyGenAssembly2`.

### Folded follow-ups (ADR-001)
- **Binance signing types → internal**: `BinanceSignatureService` (Auth), `BinanceSigningHandler` + `BinanceSigningRequest` (Resilience) changed `public` → `internal`, matching Bybit's posture. References are covered: the moved `AddBinanceExchange` is now in-assembly; `CryptoExchanges.Net.Binance.Tests.Integration` already has IVT (it constructs these types directly).
- **BinanceHttpClient endpoint guard**: added `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` as the first statement of `GetAsync<T>`, `GetStringAsync`, `PostAsync<T>`, `DeleteAsync<T>` (mirrors the Bybit fix from PR #11).

### Test changes
- `DiRegistrationTests.cs` + `ExchangeClientFactoryTests.cs`: added `using CryptoExchanges.Net.Binance;` (and `.Bybit;` in DiRegistrationTests) for the relocated extensions. All existing coverage retained.
- Added `DiRegistrationTests.BybitOnly_Registration_ResolvesBybitClient` — registers via `AddBybitExchange` alone (no aggregator) and asserts a keyed Bybit `IExchangeClient` resolves + no unkeyed registration. Uses `await using` (resolved client is `IAsyncDisposable`-only).
- No change needed to `BybitMappingAndServiceTests.cs` (already `using CryptoExchanges.Net.Bybit;` for `AddBybitExchange`; still uses `AddCryptoExchanges` so its DI `using` stays) or README (`AddCryptoExchanges` sample unchanged).

### Public-API / namespace change (for OKX PR changelog)
- **BREAKING (pre-v1.0):** `AddBinanceExchange` moved from namespace `CryptoExchanges.Net.DependencyInjection` → `CryptoExchanges.Net.Binance`; `AddBybitExchange` → `CryptoExchanges.Net.Bybit`. Callers add `using CryptoExchanges.Net.Binance;` / `.Bybit;`. `AddCryptoExchanges` is unchanged (still in `CryptoExchanges.Net.DependencyInjection`). Binance signing types (`BinanceSignatureService`, `BinanceSigningHandler`, `BinanceSigningRequest`) are no longer public.

### Verification
- `dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s)** (TreatWarningsAsErrors).
- `dotnet test --filter 'Category!=Integration'` → all pass: Core 93, Http 12, DI **11** (was 10, +1 new), Bybit.Unit 80, Binance.Integration 45 (no Integration trait, runs here) = 241, 0 failures.
- `dotnet test --filter 'Category=Integration'` → Bybit integration **5/5** pass.
- Dependency direction verified: `dotnet list src/CryptoExchanges.Net.Bybit reference` → only Core + Http (no Binance). Bybit-only consumer never pulls in Binance.

## Commits
- **Commit**: 1a56835 refactor(M3): TASK-009B per-exchange DI re-homing (ADR-001)
