---
id: TASK-065
status: IN_PROGRESS
depends_on: []
---
# TASK-065: Rename aggregator project → `CryptoExchanges.Net` (folder, csproj, ids, namespace, sln)

## Metadata
- **ID**: TASK-065
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: none
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net/CryptoExchanges.Net.csproj, src/CryptoExchanges.Net/ServiceCollectionExtensions.cs, src/CryptoExchanges.Net/CryptoExchangesOptions.cs, CryptoExchanges.Net.sln]
- **Wave**: 1
- **Traces to**: PRD-FEAT-007 AC-3, AC-6; TRD-FEAT-007 §"Step 1 — Rename the aggregator project"; ADR-003 (Decision); FEAT-007 spec §"Scope — In" #1; design §"Scope — In" #1
- **Created at**: 2026-06-21T18:00:00Z
- **Claimed at**: 2026-06-21T18:10:00Z
- **Base SHA**: 60d00a7
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Rename the all-exchanges DI aggregator project in place to the bare root id `CryptoExchanges.Net`.
This is identity surgery only — no behavior change. The two source files move to the
`CryptoExchanges.Net` namespace; their bodies are otherwise untouched.

Steps:
1. Rename the folder `src/CryptoExchanges.Net.DependencyInjection/` → `src/CryptoExchanges.Net/`
   (use `git mv` so history follows).
2. Rename the csproj file `CryptoExchanges.Net.DependencyInjection.csproj` →
   `CryptoExchanges.Net.csproj` (`git mv`).
3. In the csproj, set `<RootNamespace>`, `<AssemblyName>`, `<PackageId>` all to `CryptoExchanges.Net`;
   update `<Description>` to the all-exchanges meta-package wording from the TRD
   ("All-exchanges meta-package for CryptoExchanges.Net. Registers all available exchange clients in
   one call via AddCryptoExchanges()."). Keep every `<ProjectReference>` (Core, Http, Binance, Bybit,
   Okx, Bitget, Kucoin), the `Microsoft.Extensions.DependencyInjection.Abstractions` PackageReference,
   and the `<NoWarn>` list (`CA1812;CA1056;CA1031;CA2000;CA1305;CA1032`) unchanged.
4. `ServiceCollectionExtensions.cs`: change the top-level namespace declaration from
   `namespace CryptoExchanges.Net.DependencyInjection;` to `namespace CryptoExchanges.Net;`. The
   `<see cref="..."/>` cross-references (`CryptoExchangesOptions`, `IServiceCollection`) resolve
   unchanged. No other edits — the method body is identical.
5. `CryptoExchangesOptions.cs`: change the namespace declaration to `namespace CryptoExchanges.Net;`.
   No other edits.
6. `CryptoExchanges.Net.sln`: update the renamed project's `Project(...)` entry (line 10) — name
   `CryptoExchanges.Net.DependencyInjection` → `CryptoExchanges.Net` and path
   `src\CryptoExchanges.Net.DependencyInjection\CryptoExchanges.Net.DependencyInjection.csproj` →
   `src\CryptoExchanges.Net\CryptoExchanges.Net.csproj`. Keep the existing project GUID
   (`{C3D4E5F6-A7B8-9012-CDEF-123456789012}`) and its build-config block unchanged.

This task intentionally does NOT touch consumers (their references will break the build until later
tasks repoint them); the per-task build verification is scoped to the renamed project compiling on
its own (`dotnet build src/CryptoExchanges.Net/CryptoExchanges.Net.csproj`). The full-solution 0W/0E
gate is TASK-070.

No XML-doc changes beyond the namespace move (LEAN docs already present: short `<summary>`,
`<param>` per param, `<returns>`). No `<inheritdoc/>` — this is a concrete static class + concrete
sealed options class implementing no interface (per TRD §Build Requirements).

## Acceptance Criteria
- [ ] Folder is `src/CryptoExchanges.Net/` with `CryptoExchanges.Net.csproj` whose `PackageId`/`AssemblyName`/`RootNamespace` are all `CryptoExchanges.Net`; all 7 ProjectReferences + the DI.Abstractions PackageReference + the `<NoWarn>` list are preserved; no folder/file named `*DependencyInjection*` remains under `src/`.
- [ ] `ServiceCollectionExtensions.cs` and `CryptoExchangesOptions.cs` declare `namespace CryptoExchanges.Net;`; method name `AddCryptoExchanges` and the `CryptoExchangesOptions` property shape are byte-for-byte unchanged otherwise; `dotnet build src/CryptoExchanges.Net/CryptoExchanges.Net.csproj` succeeds 0W/0E.
- [ ] `CryptoExchanges.Net.sln` references the renamed project at `src\CryptoExchanges.Net\CryptoExchanges.Net.csproj` (same GUID); no `…DependencyInjection` project path remains for the src project entry.

## Pattern Reference
- Packable lib csproj shape (PackageId/AssemblyName/RootNamespace/NoWarn pattern): `src/CryptoExchanges.Net.Kucoin/CryptoExchanges.Net.Kucoin.csproj`, `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj`.
- Current aggregator csproj being renamed: `src/CryptoExchanges.Net.DependencyInjection/CryptoExchanges.Net.DependencyInjection.csproj`.
- Source files being moved: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:8` (namespace line), `src/CryptoExchanges.Net.DependencyInjection/CryptoExchangesOptions.cs:1` (namespace line).
- sln project-entry format: `CryptoExchanges.Net.sln:10` (the entry to edit).

## File Scope

**Creates** (via `git mv` — renamed paths):
- src/CryptoExchanges.Net/CryptoExchanges.Net.csproj
- src/CryptoExchanges.Net/ServiceCollectionExtensions.cs
- src/CryptoExchanges.Net/CryptoExchangesOptions.cs

**Modifies**:
- src/CryptoExchanges.Net/CryptoExchanges.Net.csproj (ids + description)
- src/CryptoExchanges.Net/ServiceCollectionExtensions.cs (namespace line)
- src/CryptoExchanges.Net/CryptoExchangesOptions.cs (namespace line)
- CryptoExchanges.Net.sln (renamed project entry, line 10)

**Deletes** (via `git mv`):
- src/CryptoExchanges.Net.DependencyInjection/ (entire folder)

## Traceability
- **PRD Acceptance Criteria**: AC-3 (no `…DependencyInjection` project remains), AC-6 (0W/0E)
- **TRD Component**: §"Step 1 — Rename the aggregator project"
- **ADR Reference**: ADR-003 (claim bare root id `CryptoExchanges.Net`); CLAUDE.md one-type-per-file + LEAN XML docs

## Commits

## Implementation Log

## Review Results
