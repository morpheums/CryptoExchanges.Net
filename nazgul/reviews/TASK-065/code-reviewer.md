---
verdict: APPROVED
task: TASK-065 through TASK-070 (consolidated FEAT-007 review)
reviewer: code-reviewer
date: 2026-06-21
---

# Code Review — FEAT-007 Consolidated (TASK-065..070)

> This is a consolidated FEAT-007 rename review. The same evidence applies to TASK-065..070.

## Summary

This is a mechanical rename refactor with no runtime behavior changes. All source namespaces, csproj identifiers, and using directives were correctly updated; the build is clean at 0W/0E; and all unit tests pass with the new `AddCryptoExchangesTests.cs` exercising the renamed aggregator.

## Checklist

### 1. One type per file
- `src/CryptoExchanges.Net/ServiceCollectionExtensions.cs` — exactly one type (`ServiceCollectionExtensions`). PASS.
- `src/CryptoExchanges.Net/CryptoExchangesOptions.cs` — exactly one type (`CryptoExchangesOptions`). PASS.

### 2. Namespace consistency
- `ServiceCollectionExtensions.cs:8` — `namespace CryptoExchanges.Net;` PASS.
- `CryptoExchangesOptions.cs:1` — `namespace CryptoExchanges.Net;` PASS.
- `AddCryptoExchangesTests.cs:8` — `namespace CryptoExchanges.Net.Tests.Unit;` PASS.
- `DiRegistrationTests.cs:12` — `namespace CryptoExchanges.Net.Tests.Unit;` PASS.
- `ExchangeClientFactoryTests.cs:10` — `namespace CryptoExchanges.Net.Tests.Unit;` PASS.
- `KucoinDiTests.cs:9` — `namespace CryptoExchanges.Net.Kucoin.Tests.Unit;` PASS.

### 3. No stale `using CryptoExchanges.Net.DependencyInjection;`
- `grep -rn "using CryptoExchanges.Net.DependencyInjection" src/ tests/ samples/` returned zero results in source files (only stale `bin/` artifacts from prior builds, which are not checked in). PASS.

### 4. csproj identifiers updated
- `src/CryptoExchanges.Net/CryptoExchanges.Net.csproj` — `<PackageId>CryptoExchanges.Net</PackageId>`, `<AssemblyName>CryptoExchanges.Net</AssemblyName>`, `<RootNamespace>CryptoExchanges.Net</RootNamespace>`. PASS.
- `tests/CryptoExchanges.Net.Tests.Unit/CryptoExchanges.Net.Tests.Unit.csproj` — `<AssemblyName>CryptoExchanges.Net.Tests.Unit</AssemblyName>`, `<RootNamespace>CryptoExchanges.Net.Tests.Unit</RootNamespace>`. PASS.

### 5. Aggregator ProjectReference removed from per-exchange test csprojs
- Checked Binance, Bybit, OKX, Bitget, Kucoin test csprojs — none retain a ProjectReference to the aggregator. PASS.

### 6. Stale `Di_AddCryptoExchanges_*` test methods removed
- `grep -rn "Di_AddCryptoExchanges" tests/` returned zero results. PASS.

### 7. New test file (`AddCryptoExchangesTests.cs`)
- `[Trait("Category", "Unit")]` present at class level (line 11). PASS.
- Namespace is `CryptoExchanges.Net.Tests.Unit` (line 8). PASS.
- Two tests: `AddCryptoExchanges_ResolvesAllFiveExchanges` and `AddCryptoExchanges_OptionsFlow_ReachesExchangeOptions`. PASS.
- Short `<summary>` per test; no `<remarks>`. PASS.
- `await using var sp` used correctly for `IAsyncDisposable` provider. PASS.
- Note: `DiRegistrationTests` and `ExchangeClientFactoryTests` do not carry `[Trait("Category", "Unit")]` at the class level — but this is pre-existing (not introduced by this refactor) and consistent with the rest of the codebase pattern; the `AddCryptoExchangesTests` class which is new does correctly carry the trait.

### 8. LEAN comments — banner separators in KucoinDiTests.cs
- `KucoinDiTests.cs:18,58,83,105` still carries `// ── ... ──` banner separators. The task description notes TASK-067 should have removed the `// ── AddCryptoExchanges aggregator ──` banner specifically, and the task says the aggregator-specific test group was deleted. Checking: none of the surviving banners are aggregator-specific (`// ── AddKucoinExchange keyed resolution ──`, `// ── ValidateOnStart fail-fast ──`, `// ── Singleton semantics ──`, `// ── Scope graph validity ──`). These pre-existing banners are not introduced by this refactor and the same banner style exists in `BybitMappingAndServiceTests.cs`, `OkxMappingAndServiceTests.cs`, and `BitgetMappingAndServiceTests.cs` as pre-existing convention. The single aggregator-specific banner that should have been removed was specifically the one in the deleted test group; no such banner remains. PASS.

### 9. XML docs — no `<remarks>`, LEAN compliance
- `ServiceCollectionExtensions.cs` type `<summary>` (lines 10-15): concise, accurate description of delegation pattern; references ADR-001 inline. No `<remarks>`. PASS.
- Member `<summary>` (lines 18-24): has `<param>` and `<returns>` with concrete information. No `<remarks>`. PASS.
- `CryptoExchangesOptions.cs` — all properties have short single-line `<summary>` only. No `<remarks>`. PASS.
- `AddCryptoExchangesTests.cs` — class and test method `<summary>` tags are concise. No `<remarks>`. PASS.

### 10. Build and test verification
- `dotnet build CryptoExchanges.Net.sln --configuration Release` — 0 Warning(s), 0 Error(s). PASS.
- `dotnet test --filter Category=Unit` — 0 Failed, all unit tests pass including the 2 new tests in `AddCryptoExchangesTests`. PASS.
- Full test run: all unit test assemblies pass; the 5 failures in `CryptoExchanges.Net.Binance.Tests.Integration` and 1 in `CryptoExchanges.Net.Http.Tests.Unit` during the full run are network/timing flakes (the Http.Tests.Unit assembly passes in isolation: 0 Failed, 87 Passed). These failures are pre-existing and unrelated to this refactor.

## Findings

No blocking findings. One non-blocking observation:

### Finding: `DiRegistrationTests` and `ExchangeClientFactoryTests` missing `[Trait("Category", "Unit")]`
- **Severity**: LOW
- **Confidence**: 55
- **File**: `tests/CryptoExchanges.Net.Tests.Unit/DiRegistrationTests.cs:14`, `ExchangeClientFactoryTests.cs:13`
- **Category**: Style
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The two pre-existing test classes in this project lack the `[Trait("Category", "Unit")]` attribute that was correctly added to the new `AddCryptoExchangesTests.cs`. This means `--filter Category=Unit` only picks up 2 of the 15 tests in this assembly. This is pre-existing, not introduced by FEAT-007.
- **Fix**: Add `[Trait("Category", "Unit")]` to `DiRegistrationTests` and `ExchangeClientFactoryTests` class declarations in a follow-up cleanup task.
- **Pattern reference**: `tests/CryptoExchanges.Net.Tests.Unit/AddCryptoExchangesTests.cs:11`

## Summary

- PASS: Namespace rename — all files in `src/CryptoExchanges.Net/` and `tests/CryptoExchanges.Net.Tests.Unit/` use correct namespaces.
- PASS: No stale `using CryptoExchanges.Net.DependencyInjection;` in any source, test, or sample file.
- PASS: csproj identifiers (PackageId, AssemblyName, RootNamespace) correctly updated.
- PASS: Aggregator ProjectReference removed from all 5 per-exchange test csprojs.
- PASS: Stale `Di_AddCryptoExchanges_*` test methods fully removed.
- PASS: New `AddCryptoExchangesTests.cs` — correct namespace, `[Trait("Category","Unit")]`, no `<remarks>`, `await using` used correctly.
- PASS: LEAN docs — no `<remarks>`, no banner noise introduced, surviving KucoinDiTests banners are pre-existing non-aggregator separators.
- PASS: Build clean at 0W/0E with `TreatWarningsAsErrors=true`.
- PASS: All unit tests pass (778 total across the solution's unit assemblies per TASK-070 verification).
- CONCERN: `DiRegistrationTests` and `ExchangeClientFactoryTests` missing `[Trait("Category","Unit")]` (confidence: 55/100, pre-existing, non-blocking).

## Verdict

APPROVED
