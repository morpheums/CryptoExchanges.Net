---
id: TASK-069
status: PLANNED
depends_on: [TASK-068]
---
# TASK-069: Docs + CHANGELOG + version bump to `0.5.0-preview.1`

## Metadata
- **ID**: TASK-069
- **Group**: 5
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-068
- **Delegates to**: none
- **Files modified**: [Directory.Build.props, CHANGELOG.md, README.md, NUGET_README.md, docs/architecture.md, docs/getting-started.md, docs/exchanges.md, docs/library-usage.md]
- **Wave**: 5
- **Traces to**: PRD-FEAT-007 AC-1, AC-8; TRD-FEAT-007 §"Step 5 — Docs and version"; FEAT-007 spec §"Scope — In" #5, #6
- **Created at**: 2026-06-21T18:00:00Z
- **Claimed at**:
- **Base SHA**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Update all consumer-facing text to the renamed package, bump the centralized version, and add the
CHANGELOG migration entry. Strictly technical wording — no roadmap/opsec leakage (public repo).

Steps:
1. `Directory.Build.props` (line 20) — `<Version>0.4.0-preview.1</Version>` → `<Version>0.5.0-preview.1</Version>`.
   This is the single centralized lockstep version; all 9 packages inherit it.
2. `CHANGELOG.md` — add a new top section `## [0.5.0-preview.1] — 2026-06-21` with:
   - **Changed**: `[BREAKING — package id / namespace]` Renamed aggregator package
     `CryptoExchanges.Net.DependencyInjection` → `CryptoExchanges.Net`; `AddCryptoExchanges` and
     `CryptoExchangesOptions` moved to namespace `CryptoExchanges.Net`; method name + options shape
     unchanged.
   - **Migration**: remove `dotnet add package CryptoExchanges.Net.DependencyInjection`; add
     `dotnet add package CryptoExchanges.Net`; change
     `using CryptoExchanges.Net.DependencyInjection;` → `using CryptoExchanges.Net;`;
     `services.AddCryptoExchanges(...)` unchanged.
   - **Internal**: decoupled per-exchange `.Tests.Unit` projects from the aggregator; consolidated
     all-exchanges resolution test into `CryptoExchanges.Net.Tests.Unit`.
   Match the existing CHANGELOG heading/section style.
3. Public docs — replace every `CryptoExchanges.Net.DependencyInjection` occurrence (package install
   commands, `using` examples, prose) with `CryptoExchanges.Net`, and present
   `dotnet add package CryptoExchanges.Net` as the all-exchanges entry point. Known occurrences (from
   the repo scan):
   - `README.md` (1 occurrence)
   - `NUGET_README.md` (3)
   - `docs/architecture.md` (2)
   - `docs/getting-started.md` (1)
   - `docs/exchanges.md` (2)
   - `docs/library-usage.md` (1) — present in the repo though not enumerated in the TRD; include it
     so AC-8 ("no `…DependencyInjection` package name in consumer-facing text") holds.
   Re-grep after editing to confirm zero remaining `CryptoExchanges.Net.DependencyInjection` in any
   tracked consumer-facing doc.

Do NOT mention the nuget.org deprecate/unlist step as a build action — it is a documented manual
post-merge step (note it in CHANGELOG Migration prose only if it already fits the existing style;
otherwise omit). No source/test code changes in this task.

## Acceptance Criteria
- [ ] `Directory.Build.props` `<Version>` is `0.5.0-preview.1`; `CHANGELOG.md` has a `## [0.5.0-preview.1]` section with the rename (BREAKING), a two-line migration (package swap + using swap, `AddCryptoExchanges` unchanged), and the test-decoupling internal note.
- [ ] `grep -r 'CryptoExchanges.Net.DependencyInjection' README.md NUGET_README.md docs/` returns zero matches; each shows `dotnet add package CryptoExchanges.Net` / `using CryptoExchanges.Net;` as the all-exchanges entry point.
- [ ] No opsec/roadmap leakage introduced; `dotnet build CryptoExchanges.Net.sln` remains 0W/0E (docs/props-only change does not regress the build).

## Pattern Reference
- Version source of truth: `Directory.Build.props:20`.
- CHANGELOG section style: top entries of `CHANGELOG.md`.
- Doc occurrences to replace: `README.md`, `NUGET_README.md`, `docs/architecture.md`, `docs/getting-started.md`, `docs/exchanges.md`, `docs/library-usage.md` (grep `DependencyInjection`).

## File Scope

**Creates**:
- (none)

**Modifies**:
- Directory.Build.props
- CHANGELOG.md
- README.md
- NUGET_README.md
- docs/architecture.md
- docs/getting-started.md
- docs/exchanges.md
- docs/library-usage.md

## Traceability
- **PRD Acceptance Criteria**: AC-1 (version `0.5.0-preview.1` for the pack), AC-8 (docs + CHANGELOG reference `CryptoExchanges.Net`)
- **TRD Component**: §"Step 5 — Docs and version"
- **ADR Reference**: ADR-003 (CHANGELOG documents the clean-swap migration); MEMORY public-artifacts-no-strategy (no opsec leakage)

## Commits

## Implementation Log

## Review Results
