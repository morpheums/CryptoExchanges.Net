---
id: TASK-070
status: IMPLEMENTED
depends_on: [TASK-067, TASK-068, TASK-069]
---
# TASK-070: Final verification gate — build 0W/0E, suite green, `dotnet pack` 9-package swap

## Metadata
- **ID**: TASK-070
- **Group**: 6
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-067, TASK-068, TASK-069
- **Delegates to**: none
- **Files modified**: [nazgul/reviews/TASK-070/verification.md]
- **Wave**: 6
- **Traces to**: PRD-FEAT-007 AC-1, AC-3, AC-6, AC-7; TRD-FEAT-007 §"Step 6" / §"Overview" #6; TEST-PLAN-FEAT-007 §"Pack Verification", §"Definition of Done for Tests"; FEAT-007 spec §"Build approach" final step
- **Created at**: 2026-06-21T18:00:00Z
- **Claimed at**:
- **Base SHA**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Whole-solution verification gate confirming the rename is complete and the published-package swap is
correct. This task adds no product code — it runs the gates and records evidence. If any gate fails,
the fix belongs in the owning prior task (065–069), not here; reopen that task rather than patching
around it.

Gates (run from repo root):
1. **Build 0W/0E**: `dotnet build CryptoExchanges.Net.sln -c Release` → 0 warnings, 0 errors
   (`TreatWarningsAsErrors`, `AnalysisLevel=latest-all`).
2. **Non-integration suite green**: `dotnet test CryptoExchanges.Net.sln --filter 'Category!=Integration'`
   → 0 failures, 0 skips on non-integration tests. (Note any known pre-existing parallel-run
   streaming-reconnect harness flake — see TASK-060 note — and confirm it is unrelated to this change.)
3. **No `DependencyInjection` residue**: `grep -rn 'CryptoExchanges.Net.DependencyInjection' .`
   excluding `bin/`, `obj/`, `nazgul/archive/`, `nazgul/tasks/`, `nazgul/docs/`, `nazgul/reviews/`,
   `nazgul/context/`, and the untracked scratch `LoggingTest/` returns ZERO matches in any
   src/test/sample/sln/docs file. (The agent-spec files under `.claude/agents/generated/` are
   non-shipping tooling; note but do not block on them unless they name the package as a consumer
   instruction.)
4. **Pack swap (9 packages)**: `dotnet pack CryptoExchanges.Net.sln -c Release -o ./artifacts` →
   exactly 9 `.nupkg`:
   - present: `CryptoExchanges.Net.0.5.0-preview.1.nupkg`, plus `CryptoExchanges.Net.Core.*`,
     `.Http.*`, `.Binance.*`, `.Bybit.*`, `.Okx.*`, `.Bitget.*`, `.Kucoin.*`, `.Mcp.*` — all at
     `0.5.0-preview.1`.
   - absent: no file matching `*DependencyInjection*.nupkg`.
5. **No `…DependencyInjection` project in the solution**: `dotnet sln list` shows no project
   named/pathed `…DependencyInjection`; the renamed `CryptoExchanges.Net` and
   `CryptoExchanges.Net.Tests.Unit` are present.
6. **Coverage exactly once**: confirm `AddCryptoExchanges_ResolvesAllFiveExchanges` exists only in
   `tests/CryptoExchanges.Net.Tests.Unit/AddCryptoExchangesTests.cs` (grep count == 1 across the repo).

Record all command outputs (build summary, test totals, pack file list, grep results) in
`nazgul/reviews/TASK-070/verification.md` as the evidence artifact. Clean up `./artifacts` after
recording, or add it to `.gitignore` if not already ignored (do not commit `.nupkg` binaries).

## Acceptance Criteria
- [ ] `dotnet build CryptoExchanges.Net.sln -c Release` → 0W/0E and `dotnet test --filter 'Category!=Integration'` → 0 failures/0 skips (any flake explicitly attributed to the pre-existing harness race, not this change).
- [ ] `dotnet pack -c Release` → exactly 9 `.nupkg` including `CryptoExchanges.Net.0.5.0-preview.1.nupkg`, none matching `*DependencyInjection*`; `dotnet sln list` shows no `…DependencyInjection` project.
- [ ] Repo-wide grep finds zero `CryptoExchanges.Net.DependencyInjection` in src/test/sample/sln/docs; `AddCryptoExchanges_ResolvesAllFiveExchanges` exists exactly once; evidence recorded in `nazgul/reviews/TASK-070/verification.md`.

## Pattern Reference
- Build/test commands: `nazgul/config.json` (`project.build_command`, `project.test_command`).
- Pack expectations + the 9-package list: `nazgul/docs/TEST-PLAN-FEAT-007.md` §"Pack Verification".
- Version asserted: `Directory.Build.props:20` (set by TASK-069 to `0.5.0-preview.1`).

## File Scope

**Creates**:
- nazgul/reviews/TASK-070/verification.md

**Modifies**:
- (none — verification only; `.gitignore` only if `./artifacts`/`.nupkg` is not already ignored)

## Traceability
- **PRD Acceptance Criteria**: AC-1 (9-package pack incl. `CryptoExchanges.Net.0.5.0-preview.1`), AC-3 (no `…DependencyInjection` project), AC-6 (0W/0E), AC-7 (suite green; coverage once)
- **TRD Component**: §"Overview" step 6; §"Build Requirements"
- **ADR Reference**: ADR-003 (published set stays 9; clean swap)

## Commits

## Implementation Log

## Review Results
