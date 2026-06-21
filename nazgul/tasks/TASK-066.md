---
id: TASK-066
status: DONE
depends_on: [TASK-065]
---
# TASK-066: Rename + consolidate aggregator test project → `CryptoExchanges.Net.Tests.Unit`

## Metadata
- **ID**: TASK-066
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-065
- **Delegates to**: none
- **Files modified**: [tests/CryptoExchanges.Net.Tests.Unit/CryptoExchanges.Net.Tests.Unit.csproj, tests/CryptoExchanges.Net.Tests.Unit/DiRegistrationTests.cs, tests/CryptoExchanges.Net.Tests.Unit/ExchangeClientFactoryTests.cs, tests/CryptoExchanges.Net.Tests.Unit/AddCryptoExchangesTests.cs, CryptoExchanges.Net.sln]
- **Wave**: 2
- **Traces to**: PRD-FEAT-007 AC-2, AC-3, AC-6, AC-7; TRD-FEAT-007 §"Step 2 — Rename and consolidate the aggregator test project"; TEST-PLAN-FEAT-007 §"New and Modified Tests"; ADR-003 (test isolation); FEAT-007 spec §"Scope — In" #2
- **Created at**: 2026-06-21T18:00:00Z
- **Claimed at**: 2026-06-21T20:00:00Z
- **Base SHA**: 6658ca32d147f97f7f658585bd251461c9198b0c
- **Implemented at**: 2026-06-21T20:05:00Z
- **Completed at**: 2026-06-21T21:00:00Z
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Rename the aggregator's own test project to track the renamed package, and consolidate all
all-exchanges aggregator-resolution coverage into a single file here. This is the canonical home for
that coverage after the per-exchange projects are decoupled in TASK-067.

Steps:
1. `git mv` the folder `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/` →
   `tests/CryptoExchanges.Net.Tests.Unit/` and the csproj
   `CryptoExchanges.Net.DependencyInjection.Tests.Unit.csproj` →
   `CryptoExchanges.Net.Tests.Unit.csproj`.
2. csproj: add explicit `<RootNamespace>CryptoExchanges.Net.Tests.Unit</RootNamespace>` and
   `<AssemblyName>CryptoExchanges.Net.Tests.Unit</AssemblyName>`. Repoint the aggregator
   ProjectReference from `..\..\src\CryptoExchanges.Net.DependencyInjection\CryptoExchanges.Net.DependencyInjection.csproj`
   → `..\..\src\CryptoExchanges.Net\CryptoExchanges.Net.csproj`. Keep the Core ProjectReference and
   the test PackageReferences (Microsoft.NET.Test.Sdk, xunit.v3, xunit.runner.visualstudio,
   AwesomeAssertions) and `<NoWarn>` unchanged. The aggregator already transitively references all
   five exchanges, so no extra exchange ProjectReferences are required for the consolidated test to
   compile (the `ExchangeId` enum + `IExchangeClient` come via Core).
3. `DiRegistrationTests.cs`: change `namespace CryptoExchanges.Net.DependencyInjection.Tests.Unit;`
   → `namespace CryptoExchanges.Net.Tests.Unit;` and `using CryptoExchanges.Net.DependencyInjection;`
   → `using CryptoExchanges.Net;`. Keep all existing tests unchanged
   (`Resolves_KeyedExchangeClient`, `Resolves_Mapper_AsSingleton`,
   `NoUnkeyed_ExchangeClient_Registered`, `InvalidOptions_FailFast_OnValidateOnStart`,
   `BybitOnly_Registration_ResolvesBybitClient`, `Registers_ExchangeTimeSync_AsDefault`,
   `Consumer_Can_Override_ExchangeTimeSync`, `Registration_IsScopeClean`).
4. `ExchangeClientFactoryTests.cs`: same namespace + using swap; keep all existing tests
   (`Get_ReturnsRegisteredClient`, `Get_Unregistered_Throws`, `TryGet_Registered_ReturnsTrue`,
   `TryGet_Unregistered_ReturnsFalse`, `Available_ListsRegisteredExchanges`).
5. New file `AddCryptoExchangesTests.cs` (`namespace CryptoExchanges.Net.Tests.Unit;`,
   `[Trait("Category", "Unit")]`) with exactly two facts — the single authoritative aggregator
   coverage replacing the per-exchange copies removed in TASK-067:
   - `AddCryptoExchanges_ResolvesAllFiveExchanges` — `services.AddCryptoExchanges()`; build provider;
     for each of `ExchangeId.Binance, Bybit, Okx, Bitget, Kucoin` assert
     `sp.GetRequiredKeyedService<IExchangeClient>(id).ExchangeId.Should().Be(id)`.
   - `AddCryptoExchanges_OptionsFlow_ReachesExchangeOptions` — `AddCryptoExchanges(o => o.BinanceApiKey = "test-key")`;
     assert the resolved Binance options surface `ApiKey == "test-key"` (mirror however the existing
     `DiRegistrationTests` reach per-exchange options — match that exact resolution pattern; use the
     real per-exchange options type the Binance package exposes).
   LEAN XML doc: one short `<summary>` per test (no `<remarks>` — the LEAN mandate that bit FEAT-006).
6. `CryptoExchanges.Net.sln`: update the test project entry (line 28) — name
   `CryptoExchanges.Net.DependencyInjection.Tests.Unit` → `CryptoExchanges.Net.Tests.Unit` and path
   to `tests\CryptoExchanges.Net.Tests.Unit\CryptoExchanges.Net.Tests.Unit.csproj`; keep the GUID
   (`{A552E3A1-3728-4BA3-8DB8-8EC84EF8288E}`) and its build-config block.

Run `dotnet test tests/CryptoExchanges.Net.Tests.Unit/ --filter 'Category!=Integration'` — all green,
including the two new facts.

## Acceptance Criteria
- [x] Test project is `tests/CryptoExchanges.Net.Tests.Unit/` with `AssemblyName`/`RootNamespace` = `CryptoExchanges.Net.Tests.Unit`, referencing `..\..\src\CryptoExchanges.Net\CryptoExchanges.Net.csproj`; `DiRegistrationTests`/`ExchangeClientFactoryTests` keep every existing test, now under `namespace CryptoExchanges.Net.Tests.Unit;` with `using CryptoExchanges.Net;`.
- [x] `AddCryptoExchangesTests.cs` contains `AddCryptoExchanges_ResolvesAllFiveExchanges` (asserts a working keyed `IExchangeClient` for Binance/Bybit/Okx/Bitget/Kucoin) and `AddCryptoExchanges_OptionsFlow_ReachesExchangeOptions`; both pass with no network.
- [x] `CryptoExchanges.Net.sln` references `tests\CryptoExchanges.Net.Tests.Unit\…` (same GUID); `dotnet test tests/CryptoExchanges.Net.Tests.Unit/ --filter 'Category!=Integration'` is green and `dotnet build` of this project is 0W/0E.

## Pattern Reference
- Existing aggregator tests to preserve + adapt: `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/DiRegistrationTests.cs` (namespace line :12, using :9), `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/ExchangeClientFactoryTests.cs` (namespace :10, using :8).
- Keyed `AddCryptoExchanges` resolution test shape to consolidate: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs:133` (`AddCryptoExchanges_ResolvesAllFiveExchanges`) and `:152` (`AddCryptoExchanges_KucoinOptions_AppliesViaAggregator` — the options-flow pattern).
- Test csproj shape: `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/CryptoExchanges.Net.DependencyInjection.Tests.Unit.csproj`.
- sln test-entry format: `CryptoExchanges.Net.sln:28`.

## File Scope

**Creates**:
- tests/CryptoExchanges.Net.Tests.Unit/AddCryptoExchangesTests.cs
- tests/CryptoExchanges.Net.Tests.Unit/CryptoExchanges.Net.Tests.Unit.csproj (via git mv)
- tests/CryptoExchanges.Net.Tests.Unit/DiRegistrationTests.cs (via git mv)
- tests/CryptoExchanges.Net.Tests.Unit/ExchangeClientFactoryTests.cs (via git mv)

**Modifies**:
- tests/CryptoExchanges.Net.Tests.Unit/CryptoExchanges.Net.Tests.Unit.csproj (ids + ProjectReference)
- tests/CryptoExchanges.Net.Tests.Unit/DiRegistrationTests.cs (namespace + using)
- tests/CryptoExchanges.Net.Tests.Unit/ExchangeClientFactoryTests.cs (namespace + using)
- CryptoExchanges.Net.sln (test project entry, line 28)

**Deletes** (via git mv):
- tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/ (entire folder)

## Traceability
- **PRD Acceptance Criteria**: AC-2 (single consolidated resolution test), AC-3 (no `…DependencyInjection` project), AC-6 (0W/0E), AC-7 (coverage exists exactly once)
- **TRD Component**: §"Step 2 — Rename and consolidate the aggregator test project"
- **ADR Reference**: ADR-003 (test isolation restored); CLAUDE.md LEAN XML docs (no `<remarks>`)

## Commits

- `998525f` feat(FEAT-007): TASK-066 — rename + consolidate aggregator test project → CryptoExchanges.Net.Tests.Unit

## Implementation Log

- 2026-06-21T20:05:00Z: Claimed task (base SHA `6658ca3`). Read manifest, existing test files, KuCoin pattern references.
- `git mv` folder to `tests/CryptoExchanges.Net.Tests.Unit/` and renamed csproj to `CryptoExchanges.Net.Tests.Unit.csproj`.
- Updated csproj: added `AssemblyName`/`RootNamespace`, repointed `ProjectReference` from `DependencyInjection` → `CryptoExchanges.Net`.
- Updated `DiRegistrationTests.cs`: swapped `using CryptoExchanges.Net.DependencyInjection;` → `using CryptoExchanges.Net;`, namespace → `CryptoExchanges.Net.Tests.Unit`.
- Updated `ExchangeClientFactoryTests.cs`: same using/namespace swap.
- Created `AddCryptoExchangesTests.cs` with `AddCryptoExchanges_ResolvesAllFiveExchanges` and `AddCryptoExchanges_OptionsFlow_ReachesExchangeOptions` (Binance options-flow pattern, mirrors KuCoin test).
- Updated `CryptoExchanges.Net.sln` line 28: name + path to `CryptoExchanges.Net.Tests.Unit`; GUID `{A552E3A1-3728-4BA3-8DB8-8EC84EF8288E}` preserved.
- Build: 0W/0E. Tests: 15/15 passed (13 existing + 2 new).

## Review Results

- **Consolidated FEAT-007 review** — 2026-06-21: architect-reviewer APPROVED, code-reviewer APPROVED, security-reviewer APPROVED, api-reviewer APPROVED. Gate: ✦ APPROVED (all 4/4). Task → DONE.
