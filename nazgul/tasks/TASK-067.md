---
id: TASK-067
status: IN_PROGRESS
depends_on: [TASK-066]
---
# TASK-067: Decouple the 5 per-exchange `.Tests.Unit` projects from the aggregator

## Metadata
- **ID**: TASK-067
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-066
- **Delegates to**: none
- **Files modified**: [tests/CryptoExchanges.Net.Binance.Tests.Unit/CryptoExchanges.Net.Binance.Tests.Unit.csproj, tests/CryptoExchanges.Net.Bybit.Tests.Unit/CryptoExchanges.Net.Bybit.Tests.Unit.csproj, tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitMappingAndServiceTests.cs, tests/CryptoExchanges.Net.Okx.Tests.Unit/CryptoExchanges.Net.Okx.Tests.Unit.csproj, tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxMappingAndServiceTests.cs, tests/CryptoExchanges.Net.Bitget.Tests.Unit/CryptoExchanges.Net.Bitget.Tests.Unit.csproj, tests/CryptoExchanges.Net.Bitget.Tests.Unit/BitgetMappingAndServiceTests.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/CryptoExchanges.Net.Kucoin.Tests.Unit.csproj, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs]
- **Wave**: 3
- **Traces to**: PRD-FEAT-007 AC-4, AC-6, AC-7; TRD-FEAT-007 §"Step 3 — Decouple the five per-exchange test projects"; TEST-PLAN-FEAT-007 §"Regression Coverage"; ADR-003 (test isolation); FEAT-007 spec §"Scope — In" #3
- **Created at**: 2026-06-21T18:00:00Z
- **Claimed at**: 2026-06-21T22:00:00Z
- **Base SHA**: d447033a93962404ef834e780da093abee014460
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Remove every per-exchange test project's dependency on the all-exchanges aggregator. After
TASK-066, the consolidated `AddCryptoExchanges_ResolvesAllFiveExchanges` test lives once in
`CryptoExchanges.Net.Tests.Unit`, so the scattered per-exchange copies are deleted and the aggregator
ProjectReference + `using` removed. Each per-exchange unit-test project must then compile and pass
referencing only its own exchange package + Core/Http/test libs.

NOTE — real-code correction to the TRD baseline: **all five** per-exchange test csprojs (Binance
included) currently carry a `<ProjectReference … CryptoExchanges.Net.DependencyInjection …>` at
line 15. Binance's `.cs` files have NO aggregator `using` and NO `AddCryptoExchanges` test, so
Binance needs the csproj reference removed only — no `.cs` edits.

Per-project changes:

**Binance** (`tests/CryptoExchanges.Net.Binance.Tests.Unit/`):
- csproj line 15 — remove the `CryptoExchanges.Net.DependencyInjection` ProjectReference. No `.cs`
  changes (only `Microsoft.Extensions.DependencyInjection` usings are present, which stay).

**Bybit** (`tests/CryptoExchanges.Net.Bybit.Tests.Unit/`):
- csproj line 15 — remove the aggregator ProjectReference.
- `BybitMappingAndServiceTests.cs` — remove `using CryptoExchanges.Net.DependencyInjection;` (line 14)
  and delete the test method `Di_AddCryptoExchanges_ResolvesBybitAndBinance` (around line 339). Keep
  every `Di_AddBybitExchange_*`, mapping, and service test untouched.

**OKX** (`tests/CryptoExchanges.Net.Okx.Tests.Unit/`):
- csproj line 15 — remove the aggregator ProjectReference.
- `OkxMappingAndServiceTests.cs` — remove `using CryptoExchanges.Net.DependencyInjection;` (line 13)
  and delete `Di_AddCryptoExchanges_ResolvesOkxBybitAndBinance` (around line 521). Keep all
  `Di_AddOkxExchange_*`, mapping, and service tests.

**Bitget** (`tests/CryptoExchanges.Net.Bitget.Tests.Unit/`):
- csproj line 15 — remove the aggregator ProjectReference.
- `BitgetMappingAndServiceTests.cs` — remove `using CryptoExchanges.Net.DependencyInjection;`
  (line 13) and delete `Di_AddCryptoExchanges_ResolvesBitgetOkxBybitAndBinance` (around line 528).
  Keep all `Di_AddBitgetExchange_*`, mapping, and service tests.

**KuCoin** (`tests/CryptoExchanges.Net.Kucoin.Tests.Unit/`):
- csproj line 15 — remove the aggregator ProjectReference.
- `KucoinDiTests.cs` — remove `using CryptoExchanges.Net.DependencyInjection;` (line 7) and delete the
  three aggregator tests under the `// ── AddCryptoExchanges aggregator ──` banner:
  `AddCryptoExchanges_ResolvesKucoinClient` (line 122), `AddCryptoExchanges_ResolvesAllFiveExchanges`
  (line 133), `AddCryptoExchanges_KucoinOptions_AppliesViaAggregator` (line 152). Remove that banner
  comment too (now dead). Update the class-level XML `<summary>` (line 14) that says "and
  AddCryptoExchanges aggregator coverage" to drop the aggregator clause. Keep all `AddKucoinExchange*`
  tests.

Do NOT touch the `.sln` (no project added/removed — only references inside csprojs change). Do NOT
add new ProjectReferences. After edits, `dotnet build CryptoExchanges.Net.sln` must be 0W/0E and
`dotnet test --filter 'Category!=Integration'` green; verify no per-exchange test project resolves
any path containing `DependencyInjection`.

## Acceptance Criteria
- [ ] No per-exchange `.Tests.Unit` csproj (Binance, Bybit, Okx, Bitget, Kucoin) contains a ProjectReference whose path contains `DependencyInjection`; no per-exchange test `.cs` file contains `using CryptoExchanges.Net.DependencyInjection;` or an `AddCryptoExchanges`/`Di_AddCryptoExchanges` test method.
- [ ] All retained per-exchange tests (`AddXxxExchange`/`Di_AddXxxExchange_*`, mapping, service, streaming) still pass; the aggregator-resolution coverage now exists exactly once (in `CryptoExchanges.Net.Tests.Unit`).
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E and `dotnet test --filter 'Category!=Integration'` → green.

## Pattern Reference
- ProjectReference to remove (identical line in all five): `tests/CryptoExchanges.Net.Bybit.Tests.Unit/CryptoExchanges.Net.Bybit.Tests.Unit.csproj:15`.
- Aggregator test methods to delete: `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitMappingAndServiceTests.cs:339`, `tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxMappingAndServiceTests.cs:521`, `tests/CryptoExchanges.Net.Bitget.Tests.Unit/BitgetMappingAndServiceTests.cs:528`, `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs:122,133,152`.
- Retained-test reference (untouched `AddXxxExchange` style): `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs:22` (`AddKucoinExchange_ResolvesKeyedClient`).

## File Scope

**Creates**:
- (none)

**Modifies**:
- tests/CryptoExchanges.Net.Binance.Tests.Unit/CryptoExchanges.Net.Binance.Tests.Unit.csproj
- tests/CryptoExchanges.Net.Bybit.Tests.Unit/CryptoExchanges.Net.Bybit.Tests.Unit.csproj
- tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitMappingAndServiceTests.cs
- tests/CryptoExchanges.Net.Okx.Tests.Unit/CryptoExchanges.Net.Okx.Tests.Unit.csproj
- tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxMappingAndServiceTests.cs
- tests/CryptoExchanges.Net.Bitget.Tests.Unit/CryptoExchanges.Net.Bitget.Tests.Unit.csproj
- tests/CryptoExchanges.Net.Bitget.Tests.Unit/BitgetMappingAndServiceTests.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/CryptoExchanges.Net.Kucoin.Tests.Unit.csproj
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs

## Traceability
- **PRD Acceptance Criteria**: AC-4 (no aggregator reference/using; `AddXxxExchange` tests pass), AC-6 (0W/0E), AC-7 (coverage exactly once)
- **TRD Component**: §"Step 3 — Decouple the five per-exchange test projects"
- **ADR Reference**: ADR-003 (test isolation restored); ADR-001 (per-exchange independence)

## Commits

## Implementation Log

## Review Results
