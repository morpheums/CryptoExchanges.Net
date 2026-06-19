---
id: TASK-032
status: DONE
depends_on: [TASK-030, TASK-031]
commit: 3432f08
claimed_at: 2026-06-19T06:00:00Z
---
# TASK-032: Tool-roster guard test + README/packaging

## Metadata
- **ID**: TASK-032
- **Group**: 4
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-030, TASK-031
- **Delegates to**: none
- **Files modified**: [tests/CryptoExchanges.Net.Mcp.Tests.Unit/ToolRosterTests.cs, src/CryptoExchanges.Net.Mcp/README.md, README.md]
- **Wave**: 4
- **Traces to**: Approved design §Non-Goals (read-only structural) + §Packaging/adoption; Approved plan **Task 5**
- **Created at**: 2026-06-19T04:00:00Z
- **Claimed at**: 2026-06-19T06:00:00Z
- **Base SHA**: fade890
- **Implemented at**: 2026-06-19T06:30:00Z
- **Completed at**: 2026-06-19T07:30:00Z
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Status

Blast radius: 1 new test file + 1 new package README + 1 edit to repo-root README. Depends on
both tool sets being present (asserts the full 12-tool surface). No source-logic changes.

## Description

Add the read-only structural guard test and the adoption docs. Implement faithfully per
**plan Task 5** (it has the exact `ToolRosterTests.cs` + README guidance). Follow the TDD order.

Reflection guard test `ToolRosterTests` over `MarketDataTools` + `AccountTools`:
- `Exposes_AllTwelve_ReadOnlyTools` — exactly **12** `[McpServerTool]` methods.
- `EveryTool_HasNonEmptyDescription`.
- `NoTool_NameImpliesAWriteOperation` — none contains Place/Cancel/Create/Submit/Delete.

NOTE (from plan): if the count differs, do NOT add/remove tools to satisfy it — 12 is the
spec'd surface (6 + 6). If the SDK's attribute type names differ in the resolved version, fix the
`using`/type references in the test to the SDK's actual attribute type (verify via build).

Docs: `src/CryptoExchanges.Net.Mcp/README.md` — what it is, `dotnet tool install -g`, the env
vars (BINANCE_API_KEY … BITGET_PASSPHRASE; market-data needs none), the MCP-client config JSON
block, the 12 tools grouped market-data/account, and an explicit **read-only — no order
placement** statement. Add a ~15-line "MCP server" section to the repo-root `README.md`
pointing at the package. Do NOT touch license metadata (AGPL relicense is a separate PR).
Verify packaging with `dotnet pack` (produces a `.nupkg`, 0W/0E).

## Acceptance Criteria
- [x] `dotnet build CryptoExchanges.Net.sln -c Release` is 0W/0E and full-solution `dotnet test` passes — existing 455 tests **plus** all new MCP tests green.
- [x] `ToolRosterTests` pass: exactly **12** tools, every tool has a non-empty description, **zero** write-named tools.
- [x] `src/CryptoExchanges.Net.Mcp/README.md` (install + env vars + MCP config block + 12-tool list + "read-only" statement) and a repo-root README "MCP server" section exist; `dotnet pack` produces a tool `.nupkg`.

## Pattern Reference
- Tool types under test: `src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs` (TASK-030),
  `src/CryptoExchanges.Net.Mcp/Tools/AccountTools.cs` (TASK-031) — the `[McpServerTool]` /
  `[Description]` attributes are read via reflection.
- Reflection-over-attributes + FluentAssertions style: `nazgul/context/test-strategy.md`.
- Existing repo-root `README.md` for the section's tone/structure.
- Exact create code + README content: **plan Task 5, Steps 1–8** — implement as written.

## File Scope

**Creates**:
- tests/CryptoExchanges.Net.Mcp.Tests.Unit/ToolRosterTests.cs
- src/CryptoExchanges.Net.Mcp/README.md

**Modifies**:
- README.md (repo root — add an "MCP server" section)

## Traceability
- **PRD Acceptance Criteria**: n/a — Approved design §Non-Goals (read-only is structural), §Packaging/adoption (dotnet global tool + README config)
- **TRD Component**: n/a — `ToolRosterTests` (structural guard) + package/repo README
- **ADR Reference**: ADR-001 (read-only structural enforcement; no write surface)

## Commits

- `3432f08` — feat(FEAT-002): tool-roster guard test + README/packaging (TASK-032)

## Implementation Log

### Attempt 1

- Wrote `ToolRosterTests.cs` per plan Task 5, Step 1 — 3 tests: count=12, all have [Description], no write verbs.
- Confirmed all 3 roster tests pass immediately (tools already correctly implemented by TASK-030/031).
- `McpServerToolAttribute` type name confirmed correct for ModelContextProtocol 1.4.0.
- Created `src/CryptoExchanges.Net.Mcp/README.md` with install, env vars, MCP client config block, 12-tool reference table, read-only statement.
- Added ~15-line "MCP server" section to repo-root `README.md`.
- Fixed `dotnet pack` NU5039 error: Directory.Build.props sets `<PackageReadmeFile>README.md</PackageReadmeFile>` globally; added `<None Include="README.md" Pack="true" PackagePath="\">` to csproj.
- All acceptance criteria met: build 0W/0E, 44 MCP tests pass (incl. 3 roster), pack produces `CryptoExchanges.Net.Mcp.0.1.0-preview.1.nupkg`.
- Roster test actual tool count: **12** (6 MarketDataTools + 6 AccountTools).

## Review Results

### Attempt 1

- **architect-reviewer**: APPROVED (97) — Roster guard structurally correct; csproj pack fix minimal and idiomatic; README accurately represents architecture. Non-blocking CONCERN: stale roadmap checkboxes in root README (pre-existing, confidence 70).
- **code-reviewer**: APPROVED (97) — BindingFlags and attribute type correct; count guard not gameable; tool names match source exactly; pack syntax verified. LR-001/002/003 N/A.
- **security-reviewer**: APPROVED (95) — No hardcoded secrets; placeholders used in config JSON; OKX_PASSPHRASE/BITGET_PASSPHRASE present; read-only claim verified against source. Non-blocking CONCERN: secret key rows missing "(read permission)" annotation (confidence 55).
- **api-reviewer**: APPROVED (93) — PackAsTool+ToolCommandName correct; README pack fix satisfies NU5039; MCP config JSON valid; 12-tool count verified against source. Non-blocking CONCERN: GenerateDocumentationFile=false without inline comment (confidence 65).
- **Overall**: ALL APPROVED — TASK-032 DONE.
