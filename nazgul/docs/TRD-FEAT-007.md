# TRD — FEAT-007: Rename DI Aggregator to Root Meta-Package `CryptoExchanges.Net`

- **Status**: Approved for implementation
- **Date**: 2026-06-21
- **Feature ID**: FEAT-007
- **Type**: Brownfield refactor — package rename + namespace move + test decoupling

## Overview

Six mechanical changes, applied in strict sequence so the build stays green at each step:

1. Rename the aggregator project (folder, csproj, ids, namespaces, source files).
2. Rename and consolidate the aggregator test project; write one all-five resolution test.
3. Decouple the five per-exchange test projects (drop aggregator reference and moved tests).
4. Repoint MCP (src + tests), samples, and the `.sln` file.
5. Update public docs and `CHANGELOG.md`; bump version to `0.5.0-preview.1`.
6. Build 0W/0E; full non-integration suite green; `dotnet pack` verifies 9-package swap.

No shared engine changes. No behavioral changes. This is identity + wiring surgery only.

---

## Current State (Brownfield Baseline)

```
src/CryptoExchanges.Net.DependencyInjection/
  CryptoExchanges.Net.DependencyInjection.csproj   PackageId / AssemblyName / RootNamespace = CryptoExchanges.Net.DependencyInjection
  ServiceCollectionExtensions.cs                    namespace CryptoExchanges.Net.DependencyInjection
  CryptoExchangesOptions.cs                         namespace CryptoExchanges.Net.DependencyInjection

tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/
  DiRegistrationTests.cs          (AddBinanceExchange / AddBybitExchange DI coverage)
  ExchangeClientFactoryTests.cs   (IExchangeClientFactory resolution)
```

Consumers of the aggregator (`using CryptoExchanges.Net.DependencyInjection;`):
- `src/CryptoExchanges.Net.Mcp/Program.cs` + `EnvCredentialBinder.cs` + `.csproj`
- `tests/CryptoExchanges.Net.Mcp.Tests.Unit/EnvCredentialBinderTests.cs` + `.csproj`
- `samples/BasicUsage/` (csproj + usings)
- `tests/CryptoExchanges.Net.{Bybit,Okx,Bitget,Kucoin}.Tests.Unit/` — 5 per-exchange test projects
  each with a ProjectReference to the aggregator and one `AddCryptoExchanges` resolution test:
  - `BybitMappingAndServiceTests.cs` — `Di_AddCryptoExchanges_ResolvesBybitAndBinance`
  - `OkxMappingAndServiceTests.cs` — `Di_AddCryptoExchanges_ResolvesOkxBybitAndBinance`
  - `BitgetMappingAndServiceTests.cs` — `Di_AddCryptoExchanges_ResolvesBitgetOkxBybitAndBinance`
  - `KucoinDiTests.cs` — `AddCryptoExchanges_ResolvesKucoinClient` + `AddCryptoExchanges_ResolvesAllFiveExchanges` + `AddCryptoExchanges_KucoinOptions_AppliesViaAggregator`

Current version in `Directory.Build.props`: `0.4.0-preview.1`. Target: `0.5.0-preview.1`.

---

## Target Architecture

### Step 1 — Rename the aggregator project

**Folder**: `src/CryptoExchanges.Net.DependencyInjection/` → `src/CryptoExchanges.Net/`

**csproj** (`CryptoExchanges.Net.DependencyInjection.csproj` → `CryptoExchanges.Net.csproj`):
```xml
<RootNamespace>CryptoExchanges.Net</RootNamespace>
<AssemblyName>CryptoExchanges.Net</AssemblyName>
<PackageId>CryptoExchanges.Net</PackageId>
<Description>All-exchanges meta-package for CryptoExchanges.Net. Registers all available
exchange clients in one call via AddCryptoExchanges().</Description>
```
All other csproj content (ProjectReferences to Core, Http, Binance, Bybit, Okx, Bitget, Kucoin;
`<PackageReference>` for `Microsoft.Extensions.DependencyInjection.Abstractions`; `NoWarn` entries)
remains unchanged.

**`ServiceCollectionExtensions.cs`**:
- Top-level namespace declaration: `namespace CryptoExchanges.Net;`
- XML doc `<see cref="...">` cross-references updated to point to `CryptoExchanges.Net` types.
- No other changes; the method body is identical.

**`CryptoExchangesOptions.cs`**:
- Top-level namespace declaration: `namespace CryptoExchanges.Net;`
- No other changes.

**`.sln`**: Update project path entry for the renamed project.

### Step 2 — Rename and consolidate the aggregator test project

**Folder**: `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/` → `tests/CryptoExchanges.Net.Tests.Unit/`

**csproj** (`CryptoExchanges.Net.DependencyInjection.Tests.Unit.csproj` → `CryptoExchanges.Net.Tests.Unit.csproj`):
```xml
<RootNamespace>CryptoExchanges.Net.Tests.Unit</RootNamespace>
<AssemblyName>CryptoExchanges.Net.Tests.Unit</AssemblyName>
```
ProjectReference updated to `..\..\src\CryptoExchanges.Net\CryptoExchanges.Net.csproj`.
ProjectReferences to all five exchange packages added (the test project legitimately references
the all-exchanges bundle, which itself references all exchanges — or add them directly for clarity).

**`DiRegistrationTests.cs`**: Namespace → `CryptoExchanges.Net.Tests.Unit`; `using` →
`using CryptoExchanges.Net;`. Existing test methods (`Resolves_KeyedExchangeClient`, etc.)
are preserved unchanged.

**`ExchangeClientFactoryTests.cs`**: Namespace → `CryptoExchanges.Net.Tests.Unit`; `using` →
`using CryptoExchanges.Net;`. All existing tests preserved.

**New file — `AddCryptoExchangesTests.cs`**: Consolidates all-exchanges resolution coverage:
```csharp
namespace CryptoExchanges.Net.Tests.Unit;

[Trait("Category", "Unit")]
public class AddCryptoExchangesTests
{
    /// <summary>Verifies AddCryptoExchanges resolves a keyed IExchangeClient for all five exchanges.</summary>
    [Fact]
    public async Task AddCryptoExchanges_ResolvesAllFiveExchanges()
    {
        var services = new ServiceCollection();
        services.AddCryptoExchanges();
        await using var sp = services.BuildServiceProvider();

        foreach (var id in new[] { ExchangeId.Binance, ExchangeId.Bybit, ExchangeId.Okx,
                                    ExchangeId.Bitget, ExchangeId.Kucoin })
            sp.GetRequiredKeyedService<IExchangeClient>(id).ExchangeId.Should().Be(id);
    }

    /// <summary>Verifies that per-exchange options flow through CryptoExchangesOptions configure delegate.</summary>
    [Fact]
    public async Task AddCryptoExchanges_OptionsFlow_ReachesExchangeOptions()
    {
        var services = new ServiceCollection();
        services.AddCryptoExchanges(o => o.BinanceApiKey = "test-key");
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<BinanceOptions>().ApiKey.Should().Be("test-key");
    }
}
```

**`.sln`**: Replace old DI Tests project entry with new path.

### Step 3 — Decouple the five per-exchange test projects

For each of `Binance`, `Bybit`, `Okx`, `Bitget`, `Kucoin` `.Tests.Unit`:

| File | Change |
|------|--------|
| `*.csproj` | Remove `<ProjectReference … CryptoExchanges.Net.DependencyInjection …>` |
| `*.cs` files | Remove `using CryptoExchanges.Net.DependencyInjection;` |
| Per-exchange mapping/service test files | Delete the `Di_AddCryptoExchanges_*` / `AddCryptoExchanges_*` test methods listed in Current State above |

The `AddXxxExchange` keyed-resolution tests, scope-clean tests, ValidateOnStart tests, and all
mapping/service tests in each file are kept untouched — they use only the exchange's own assembly.

`CryptoExchanges.Net.Binance.Tests.Unit` has no aggregator reference or aggregator-dependent test
in the scanned baseline; no changes needed for Binance.

### Step 4 — Repoint MCP and samples

| File | Change |
|------|--------|
| `src/CryptoExchanges.Net.Mcp/Program.cs` | `using CryptoExchanges.Net.DependencyInjection;` → `using CryptoExchanges.Net;` |
| `src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs` | Same `using` swap |
| `src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj` | ProjectReference path → `..\..\src\CryptoExchanges.Net\CryptoExchanges.Net.csproj` |
| `tests/CryptoExchanges.Net.Mcp.Tests.Unit/EnvCredentialBinderTests.cs` | `using` swap |
| `tests/CryptoExchanges.Net.Mcp.Tests.Unit/*.csproj` | ProjectReference path update |
| `samples/BasicUsage/BasicUsage.csproj` | ProjectReference path update |
| `samples/BasicUsage/Program.cs` (if any using) | `using` swap |
| `CryptoExchanges.Net.sln` | Update all affected project/path entries |

### Step 5 — Docs and version

**`Directory.Build.props`**: `<Version>0.5.0-preview.1</Version>`.

**`CHANGELOG.md`** — new section at top:
```
## [0.5.0-preview.1] — 2026-06-21

### Changed
- **[BREAKING — package id / namespace]** Renamed aggregator package
  `CryptoExchanges.Net.DependencyInjection` → `CryptoExchanges.Net`.
  `AddCryptoExchanges` and `CryptoExchangesOptions` moved to namespace `CryptoExchanges.Net`.
  Method name and options shape are unchanged.

### Migration
- Remove: `dotnet add package CryptoExchanges.Net.DependencyInjection`
- Add:    `dotnet add package CryptoExchanges.Net`
- Change: `using CryptoExchanges.Net.DependencyInjection;` → `using CryptoExchanges.Net;`
- `services.AddCryptoExchanges(...)` — unchanged.

### Internal
- Decoupled per-exchange `.Tests.Unit` projects from the aggregator; consolidated
  all-exchanges resolution test into `CryptoExchanges.Net.Tests.Unit`.
```

**Public docs** (`README.md`, `NUGET_README.md`, `docs/getting-started.md`, `docs/architecture.md`,
`docs/exchanges.md`): replace every occurrence of `CryptoExchanges.Net.DependencyInjection` (in
package install commands, `using` examples, and prose) with `CryptoExchanges.Net`.

---

## Dependency Impact

| Project | Change type |
|---------|-------------|
| `src/CryptoExchanges.Net.DependencyInjection/` | Renamed to `src/CryptoExchanges.Net/`; PackageId/AssemblyName/RootNamespace updated; 2 source files get new `namespace` declarations |
| `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/` | Renamed to `tests/CryptoExchanges.Net.Tests.Unit/`; new `AddCryptoExchangesTests.cs`; existing tests updated for new namespace/using |
| `tests/CryptoExchanges.Net.{Bybit,Okx,Bitget,Kucoin}.Tests.Unit/` | ProjectReference removed; 1–3 test methods removed per project; `using` removed |
| `src/CryptoExchanges.Net.Mcp/` | `using` swap in 2 files; csproj ProjectReference path updated |
| `tests/CryptoExchanges.Net.Mcp.Tests.Unit/` | `using` swap; csproj ProjectReference path updated |
| `samples/BasicUsage/` | csproj + `using` updated |
| `CryptoExchanges.Net.sln` | 3 project path/name entries updated |
| `Directory.Build.props` | `<Version>` bumped to `0.5.0-preview.1` |
| All other projects | No change |

---

## Build Requirements

- `TreatWarningsAsErrors`: true; `AnalysisLevel`: latest-all; `Nullable`: enable (inherited).
- `GenerateDocumentationFile`: true — `ServiceCollectionExtensions.cs` and `CryptoExchangesOptions.cs`
  carry LEAN XML docs: short `<summary>`, one `<param>` per parameter, `<exception>` for any
  explicitly thrown exceptions. No `<inheritdoc/>` needed (no interface implemented; this is the
  concrete static class / concrete sealed class).
- `InternalsVisibleTo` not needed for the aggregator or its test project (no internal types tested).
- `CA1812`, `CA1056`, `CA1031`, `CA2000`, `CA1305`, `CA1032` suppressed in the csproj `<NoWarn>`
  (carry forward from current csproj).
- Pack target: `release.yml` packs via solution glob — no workflow change required.

---

## Post-Merge Manual Step (documented, not automated)

On nuget.org, for each existing `CryptoExchanges.Net.DependencyInjection` version (v0.2–v0.4
preview series): deprecate or unlist with a message pointing to `CryptoExchanges.Net`.
