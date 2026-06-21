---
id: TASK-056
status: DONE
depends_on: []
---
# TASK-056: Scaffold `CryptoExchanges.Net.Kucoin` package + Unit/Integration test projects (clone OKX)

## Metadata
- **ID**: TASK-056
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: none
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Kucoin/CryptoExchanges.Net.Kucoin.csproj, src/CryptoExchanges.Net.Kucoin/GlobalUsings.cs, src/CryptoExchanges.Net.Kucoin/KucoinOptions.cs, src/CryptoExchanges.Net.Kucoin/KucoinSymbolFormat.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/CryptoExchanges.Net.Kucoin.Tests.Unit.csproj, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/ScaffoldSmokeTests.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Integration/CryptoExchanges.Net.Kucoin.Tests.Integration.csproj, CryptoExchanges.Net.sln]
- **Wave**: 1
- **Traces to**: PRD-FEAT-006 AC-1, AC-6; TRD-FEAT-006 §"Project Layout (mirrors OKX/Bitget)", §"Build Requirements"; FEAT-006 spec §"Build approach" step 1
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**: 2026-06-20T19:30:00Z
- **Base SHA**: ad0385cbbb38e6af28a296c5de301be40dd8f9e2
- **Implemented at**: 2026-06-21T00:00:00Z
- **Completed at**: 2026-06-21T01:30:00Z
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Create the empty `CryptoExchanges.Net.Kucoin` package and its two test projects by cloning the OKX
project shape exactly. This is the structural foundation; no signing/DTO/service logic lands here —
only the project files, the options record, the symbol-format descriptor, global usings, and solution
wiring. The 4-layer chain (Core → Http → Exchange → DI) must be preserved: this project references
`Core` and `Http` only (mirror OKX's `.csproj` references).

Create:
- **`CryptoExchanges.Net.Kucoin.csproj`** — clone `CryptoExchanges.Net.Okx.csproj`: `net10.0`,
  `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-all`,
  `GenerateDocumentationFile=true`, NuGet package metadata (id `CryptoExchanges.Net.Kucoin`,
  description, icon/README per the OKX package), `InternalsVisibleTo` for the Unit + Integration test
  projects + `DynamicProxyGenAssembly2`, same `ProjectReference`s OKX uses (Core, Http), DeltaMapper +
  resilience PackageReferences matching OKX. CA1031 suppressed per-project as OKX does.
- **`GlobalUsings.cs`** — clone OKX's global usings.
- **`KucoinOptions.cs`** — clone `OkxOptions.cs`: `BaseUrl` (default `https://api.kucoin.com`),
  `ApiKey`, `SecretKey`, `Passphrase`, `TimeoutSeconds`, plus the same validation attributes/shape OKX
  uses. Full XML docs.
- **`KucoinSymbolFormat.cs`** — clone `OkxSymbolFormat.cs` but configure delimiter `"-"` + upper casing
  for `BTC-USDT` (vs OKX's own format). Full XML docs.
- **`tests/CryptoExchanges.Net.Kucoin.Tests.Unit/`** — new xUnit.v3 + AwesomeAssertions + NSubstitute
  project cloning `CryptoExchanges.Net.Okx.Tests.Unit`'s csproj; one trivial `ScaffoldSmokeTests.cs`
  proving `new KucoinSymbolFormat()` / `new KucoinOptions()` compile and behave (placeholder until
  later tasks add real coverage).
- **`tests/CryptoExchanges.Net.Kucoin.Tests.Integration/`** — new project cloning
  `CryptoExchanges.Net.Okx.Tests.Integration`'s csproj (Category=Integration default trait). No tests
  yet (added in TASK-063); empty placeholder file is acceptable if the project must hold one type.

Add all three new projects to `CryptoExchanges.Net.sln`. Do NOT reference any other exchange project.

## Acceptance Criteria
- [x] `CryptoExchanges.Net.Kucoin.csproj` + Unit + Integration test csprojs exist (cloned from OKX shape: net10.0, Nullable, TreatWarningsAsErrors, AnalysisLevel=latest-all, GenerateDocumentationFile, InternalsVisibleTo, Core+Http refs only), all added to `CryptoExchanges.Net.sln`; solution builds 0W/0E.
- [x] `KucoinOptions` (BaseUrl default `https://api.kucoin.com`, ApiKey/SecretKey/Passphrase/TimeoutSeconds) and `KucoinSymbolFormat` (delimiter `-`, upper casing → `BTC-USDT`) exist with full XML docs, one type per file.
- [x] `ScaffoldSmokeTests` passes; existing non-integration suite stays 100% green (`dotnet test --filter 'Category!=Integration'`).

## Pattern Reference
- Project/csproj shape to clone: `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj` (full file — TFM, analyzers, package metadata, InternalsVisibleTo, project refs).
- Options record: `src/CryptoExchanges.Net.Okx/OkxOptions.cs`. Symbol format: `src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs`.
- Global usings: `src/CryptoExchanges.Net.Okx/GlobalUsings.cs`.
- Unit test project: `tests/CryptoExchanges.Net.Okx.Tests.Unit/CryptoExchanges.Net.Okx.Tests.Unit.csproj`. Integration test project: `tests/CryptoExchanges.Net.Okx.Tests.Integration/CryptoExchanges.Net.Okx.Tests.Integration.csproj`.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Kucoin/CryptoExchanges.Net.Kucoin.csproj
- src/CryptoExchanges.Net.Kucoin/GlobalUsings.cs
- src/CryptoExchanges.Net.Kucoin/KucoinOptions.cs
- src/CryptoExchanges.Net.Kucoin/KucoinSymbolFormat.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/CryptoExchanges.Net.Kucoin.Tests.Unit.csproj
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/ScaffoldSmokeTests.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Integration/CryptoExchanges.Net.Kucoin.Tests.Integration.csproj

**Modifies**:
- CryptoExchanges.Net.sln (add the 3 new projects)

## Traceability
- **PRD Acceptance Criteria**: AC-1 (working package foundation), AC-6 (0W/0E + XML docs + analyzers)
- **TRD Component**: §"Project Layout (mirrors OKX/Bitget)", §"Build Requirements"
- **ADR Reference**: ADR-001 (per-exchange package owns its own assembly + DI); FEAT-006 spec §"Binding constraints" (4-layer chain)

## Commits

- `2b9c308` — feat(FEAT-006): scaffold CryptoExchanges.Net.Kucoin + Unit/Integration test projects (TASK-056)
- `8263fe1` — feat(FEAT-006): TASK-056 state transition (IMPLEMENTED)

## Implementation Log

- Created `src/CryptoExchanges.Net.Kucoin/` with 4 files: csproj, GlobalUsings.cs, KucoinOptions.cs, KucoinSymbolFormat.cs
- Created `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/` with csproj + ScaffoldSmokeTests.cs (7 tests)
- Created `tests/CryptoExchanges.Net.Kucoin.Tests.Integration/` with csproj + KucoinIntegrationPlaceholder.cs
- Wired all 3 projects into CryptoExchanges.Net.sln with new GUIDs + configuration entries + NestedProjects
- Build result: 0W/0E
- Test result: 7 new KuCoin unit tests pass; full non-integration suite (563 tests) green

## Review Results

- **architect-reviewer**: APPROVE (confidence: 98) — 4-layer chain verified; InternalsVisibleTo correct; one type per file; XML docs complete; non-blocking concern on test project DI reference (pre-existing pattern, not introduced here)
- **code-reviewer**: APPROVE (confidence: 98) — OKX clone faithful; XML docs complete; LR-005 N/A (no service methods); ToCredentials() documented correctly
- **security-reviewer**: APPROVE (confidence: 97) — no credential serialization attributes; no hardcoded secrets; ToCredentials() sound; non-blocking concern: ToString() redaction hardening recommended for TASK-059+
- **api-reviewer**: APPROVE (confidence: 97) — public surface matches OkxOptions pattern; NuGet metadata correct; scoping correct; LR-004 N/A

**Gate**: ALL APPROVED — 4/4 ✦
