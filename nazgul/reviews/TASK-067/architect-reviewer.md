---
verdict: APPROVED
task: TASK-065 through TASK-070 (consolidated FEAT-007 review)
reviewer: architect-reviewer
date: 2026-06-21
---

# Architect Review — FEAT-007 Consolidated (TASK-065..070)

> This is a consolidated FEAT-007 rename review. The same evidence applies to TASK-065, TASK-066,
> TASK-067, TASK-068, TASK-069, and TASK-070. This file is written once and copied to all six task
> review directories.

## Summary

FEAT-007 is a clean, complete mechanical rename: `CryptoExchanges.Net.DependencyInjection` →
`CryptoExchanges.Net`. The 4-layer dependency chain is preserved, ADR-001 and ADR-003 are honored,
all per-exchange test coupling is removed, every legitimate consumer is repointed, and the build
evidence confirms 0 warnings, 0 errors, 778 tests passing. No architectural invariants are violated.

---

## Checklist

### 1. 4-layer chain preserved

CHECKED — PASS.

`src/CryptoExchanges.Net/CryptoExchanges.Net.csproj` carries exactly 7 ProjectReferences: Core,
Http, Binance, Bybit, Okx, Bitget, Kucoin. No new transitive dependency was added. The Http
reference is present (meta-package depends on Http as before) and all five exchange references are
present. No project in a lower layer gained an upward reference. Layer graph: Core → Http →
Exchange(s) → CryptoExchanges.Net (meta) is intact.

### 2. ADR-003 honored — bare root id as meta-bundle

CHECKED — PASS.

`PackageId`, `AssemblyName`, and `RootNamespace` in `CryptoExchanges.Net.csproj` are all set to
`CryptoExchanges.Net`. Description reads "All-exchanges meta-package for CryptoExchanges.Net.
Registers all available exchange clients in one call via AddCryptoExchanges()." This is the exact
intent of ADR-003.

### 3. ADR-001 preserved — per-exchange AddXxxExchange stays in exchange assemblies

CHECKED — PASS.

`ServiceCollectionExtensions.AddCryptoExchanges()` only delegates to `AddBinanceExchange`,
`AddBybitExchange`, `AddOkxExchange`, `AddBitgetExchange`, `AddKucoinExchange`. No per-exchange
registration logic was moved into the meta-package. Method body is identical to the prior
DependencyInjection version.

### 4. Test projects properly decoupled

CHECKED — PASS.

All 5 per-exchange `.Tests.Unit` csprojs (Binance, Bybit, Okx, Bitget, Kucoin) reference only Core,
Http, and their own exchange assembly — no reference to the meta-package or any sibling exchange.
Confirmed by exhaustive grep across all test csproj `ProjectReference` nodes: zero hits for
`CryptoExchanges.Net.DependencyInjection` or `CryptoExchanges.Net` (meta) in any per-exchange test
project. The consolidated resolution test `AddCryptoExchanges_ResolvesAllFiveExchanges` exists
exactly once in `tests/CryptoExchanges.Net.Tests.Unit/AddCryptoExchangesTests.cs`.

### 5. git-mv history preserved — solution GUIDs

CHECKED — PASS.

`CryptoExchanges.Net.sln` entry for the meta-package src project retains GUID
`{C3D4E5F6-A7B8-9012-CDEF-123456789012}` with the new path
`src\CryptoExchanges.Net\CryptoExchanges.Net.csproj`. The test project entry retains GUID
`{A552E3A1-3728-4BA3-8DB8-8EC84EF8288E}` with the new path
`tests\CryptoExchanges.Net.Tests.Unit\CryptoExchanges.Net.Tests.Unit.csproj`. No GUID churn.

### 6. No shim / type-forwarder — `src/` is clean

CHECKED — PASS.

`ls src/` shows no `CryptoExchanges.Net.DependencyInjection` folder. Recursive grep across
`src/`, `tests/`, and `samples/` for the string `CryptoExchanges.Net.DependencyInjection` returned
zero hits in tracked source files. The only hits were in `obj/Release/` build artifact directories
(old cached nuspecs from prior versions) which are not tracked by git and do not affect the build.

### 7. One type per file

CHECKED — PASS.

`ServiceCollectionExtensions.cs` contains exactly one top-level type (`public static class
ServiceCollectionExtensions`). `CryptoExchangesOptions.cs` contains exactly one top-level type
(`public sealed class CryptoExchangesOptions`). Both files remain unchanged in structure from the
prior DependencyInjection assembly; only the `namespace` declaration changed.

### 8. MCP and samples repointed

CHECKED — PASS.

- `src/CryptoExchanges.Net.Mcp/Program.cs`: `using CryptoExchanges.Net;` (line 1).
- `src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs`: `using CryptoExchanges.Net;` (line 1).
- `src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj`: ProjectReference points to
  `../CryptoExchanges.Net/CryptoExchanges.Net.csproj`.
- `tests/CryptoExchanges.Net.Mcp.Tests.Unit/CryptoExchanges.Net.Mcp.Tests.Unit.csproj`:
  ProjectReference points to `../../src/CryptoExchanges.Net/CryptoExchanges.Net.csproj`.
- `samples/BasicUsage/BasicUsage.csproj`: ProjectReference points to
  `..\..\src\CryptoExchanges.Net\CryptoExchanges.Net.csproj`.

### 9. Consumer-facing docs updated

CHECKED — PASS.

`README.md` and `NUGET_README.md` contain no reference to `CryptoExchanges.Net.DependencyInjection`
as a package name. `CHANGELOG.md` has the `[0.5.0-preview.1]` section at the top with the BREAKING
note, migration steps, and internal decoupling note, matching TRD-FEAT-007 verbatim.

### 10. Version bumped

CHECKED — PASS.

`Directory.Build.props` `<Version>` is `0.5.0-preview.1`.

### 11. No new public API added to existing interfaces

CHECKED — PASS (not applicable). This refactor touches no Core interfaces (`IMarketDataService`,
`ITradingService`, `IAccountService`, `IExchangeClient`). No interface was modified.

### 12. No DependencyInjection project in solution

CHECKED — PASS.

`dotnet sln list` evidence (TASK-070) and direct grep of `.sln` file both confirm no project named
or ID'd `CryptoExchanges.Net.DependencyInjection` remains.

---

## Findings

No blocking findings. One non-blocking observation below.

### Finding: MCP csproj carries explicit per-exchange ProjectReferences alongside meta-package reference

- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj` lines 14-19
- **Category**: Architecture (redundant references, non-blocking)
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The MCP csproj references both the meta-package (`CryptoExchanges.Net.csproj`) and all
  five per-exchange csprojs directly. The meta-package already transitively brings in all five
  exchanges, so the five individual references are redundant. They cause no correctness problem
  (MSBuild deduplicates) but add noise to the dependency graph and could mislead a future developer
  into thinking the MCP project has a direct dependency contract with each exchange beyond what the
  meta-package provides.
- **Fix**: Remove the five individual per-exchange ProjectReferences from MCP csproj, keeping only
  the `CryptoExchanges.Net` reference. If MCP needs exchange-specific internal types (which it
  should not — it uses only public interfaces), document that explicitly.
- **Pattern reference**: `samples/BasicUsage/BasicUsage.csproj` — correctly references only the
  meta-package and nothing else.

---

## Verdict

APPROVED

All 12 checklist items pass. The 4-layer chain is intact, ADR-001 and ADR-003 are honored, test
isolation is restored, every consumer is correctly repointed, and no source file retains a reference
to the old `CryptoExchanges.Net.DependencyInjection` identity. The one CONCERN (redundant MCP
per-exchange references) is pre-existing, low-severity, and non-blocking for this rename refactor.
