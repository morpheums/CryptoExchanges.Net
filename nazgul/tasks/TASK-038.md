---
id: TASK-038
status: DONE
depends_on: []
commit: e588512
claimed_at: 2026-06-19T14:00:00Z
completed_at: 2026-06-19T15:30:00Z
---
# TASK-038: MCP docs (mcp-server.md + mcp-clients.md, major clients)

## Metadata
- **ID**: TASK-038
- **Group**: 1
- **Status**: DONE
- **Depends on**: none
- **Delegates to**: none
- **Files modified**: [docs/mcp-server.md, docs/mcp-clients.md]
- **Wave**: 1
- **Traces to**: FEAT-003 spec §Scope-In "New public docs/ folder" (mcp-server.md, mcp-clients.md — major MCP clients)
- **Created at**: 2026-06-19T13:10:00Z
- **Claimed at**: 2026-06-19T14:00:00Z
- **Implemented at**: 2026-06-19T14:55:00Z
- **Completed at**: 2026-06-19T15:30:00Z
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Status

Blast radius: **docs only** — 2 new markdown pages under public `docs/`. No source, no `.csproj`,
no behavior change. The existing `src/CryptoExchanges.Net.Mcp/README.md` is **reused/linked, not
rewritten**. Build/tests untouched and stay green.

## Description

Create the MCP onboarding documentation — the adoption funnel surface. Two pages:

- **`docs/mcp-server.md`** — what the MCP server is (read-only stdio server, tool command
  `crypto-mcp`), the **12 tools** (6 market-data needing no credentials + 6 account needing
  read-scoped keys), env-var credentials (`BINANCE_API_KEY` … `BITGET_PASSPHRASE`; OKX/Bitget
  need a passphrase), the symbol format (`BASE/QUOTE`), and the structured error categories.
  **Reuse, don't duplicate**: the canonical tool/env/error tables already live in
  `src/CryptoExchanges.Net.Mcp/README.md` — link to it and mirror concisely rather than forking
  it (avoid drift). State explicitly: **read-only — no order placement.**
- **`docs/mcp-clients.md`** — per-client setup for the MAJOR MCP clients, one
  **copy-pasteable config block each**: **Claude Code, Claude Desktop, Cursor, VS Code (Copilot),
  Windsurf, Cline, Codex, Gemini CLI**. README (TASK-039) shows only the Claude Code one-liner
  and links here for the rest (mirrors how popular MCP servers document: cover majors, link out
  for the long tail). Each block installs/points at `crypto-mcp` and shows where the per-exchange
  env-var credentials go.

Constraints:
- **VERIFY each client's CURRENT MCP config format** before publishing — formats differ per
  client and change over time (e.g. `claude mcp add` CLI vs `claude_desktop_config.json`,
  Cursor `.cursor/mcp.json`, VS Code `.vscode/mcp.json` / settings, Windsurf, Cline, Codex
  `~/.codex/config.toml`, Gemini CLI `settings.json`). Confirm the live format for each; do not
  guess. Use Context7 / the clients' current docs to verify.
- Config blocks must be **correct and directly copy-pasteable**, using placeholder credential
  values (never real secrets).
- **Opsec**: technical only — no roadmap/strategy/positioning.
- Cross-link to `docs/mcp-server.md` and resolve cleanly on GitHub.

No-dependency task (Wave 1); file-disjoint from TASK-036/TASK-037.

## Acceptance Criteria
- [x] `docs/mcp-server.md` exists: describes the read-only `crypto-mcp` server, the 12 tools (6 market-data + 6 account), env-var creds (incl. OKX/Bitget passphrase), symbol format, and error categories — reusing/linking `src/CryptoExchanges.Net.Mcp/README.md` (no duplicated/forked tables drifting from source).
- [x] `docs/mcp-clients.md` has a verified, copy-pasteable config block for each of: Claude Code, Claude Desktop, Cursor, VS Code (Copilot), Windsurf, Cline, Codex, Gemini CLI — each correct to that client's current MCP config format, using placeholder creds.
- [x] Both pages render cleanly on GitHub with resolving internal links; read-only is stated; no roadmap/strategy leakage; docs-only — `dotnet build`/`dotnet test` unaffected.

## Pattern Reference
- Canonical MCP content to reuse (tools, env vars, error categories, config JSON): `src/CryptoExchanges.Net.Mcp/README.md` (entire file) — link/mirror, do not fork.
- Tool roster + error categories source of truth: `src/CryptoExchanges.Net.Mcp/Tools/MarketDataTools.cs`, `Tools/AccountTools.cs`, `ToolRunner` (read-only; verify the 12-tool surface matches).
- Doc voice/tone: `src/CryptoExchanges.Net.Mcp/README.md` and `nazgul/context/style-conventions.md`.

## File Scope

**Creates**:
- docs/mcp-server.md
- docs/mcp-clients.md

**Modifies**:
- none

## Traceability
- **PRD Acceptance Criteria**: n/a — FEAT-003 spec §Scope-In "New public docs/ folder" (mcp-server.md + mcp-clients.md for major clients)
- **TRD Component**: n/a — `src/CryptoExchanges.Net.Mcp/README.md` is the reused source of truth
- **ADR Reference**: n/a

## Implementation Log

### Attempt 1

- **Base SHA**: b2322b0f3263c4f1a4aa1a180edf063f3d7bf4ff
- Claimed 2026-06-19T14:00:00Z on branch feat/FEAT-003-docs-overhaul
- Implemented 2026-06-19T14:55:00Z — created docs/mcp-server.md + docs/mcp-clients.md; all checks green

## Commits

- `e588512` — feat(FEAT-003): MCP server + client setup docs (TASK-038)
- `9a2f291` — feat(FEAT-003): simplify TASK-038 docs (prose-only; no technical content changed)
- review-gate auto-fix + DONE commit (this attempt) — see below

## Review Results

### Attempt 1

**Pre-checks**: `dotnet build` PASS (0 warnings/errors), `dotnet test` PASS (no source changed; build/tests green). Smoke: none configured. Docs-only — no src/ or tests/ files touched.

**Simplifier** (Step 0): committed `9a2f291` — 6–7 prose-only fixes (trimmed redundant intros, collapsed symbol-format list, heading capitalization). No technical content (tool names, env vars, error categories, config shapes) altered — verified.

**Reviewers** (all 4 APPROVED):

| Reviewer            | Verdict   | Score | Notes |
|---------------------|-----------|-------|-------|
| architect-reviewer  | APPROVED  | 9/10  | 3 non-blocking CONCERNs (inline tool/error-table drift risk, MCP Inspector duplicated across files); structure, links, opsec, scope all PASS |
| code-reviewer       | APPROVED  | 10/10 | 12 tool names, 9 error categories, 10 env vars, install/tool command, 8 config shapes all verified against source |
| security-reviewer   | APPROVED  | 10/10 | Only placeholder creds, env-var approach sound, read-only accurate, no internal leakage, scope clean |
| api-reviewer        | APPROVED  | 9/10  | 1 PASS-level cosmetic ("24 h"→"24h"), 2 non-blocking CONCERNs (Claude Code `--env` placement, missing SDK prerequisite link) |

**Verdict**: ALL APPROVED. No blocking REJECTs (no finding was confidence ≥80 AND severity HIGH/MEDIUM).

**Auto-fixes applied by review-gate** (trivial doc-correctness fixes per mandate):
1. `docs/mcp-server.md` — `GetTicker` description `24 h` → `24h` (align to canonical README / `MarketDataTools.cs` Description).
2. `docs/mcp-clients.md` — Claude Code credential example: moved `--env` flags **before** the `--` separator (verified against `claude mcp add --help`: flags precede `--`; tokens after `--` are passed to the `crypto-mcp` subprocess). The prior form `claude mcp add crypto -- crypto-mcp --env ...` would have handed `--env` to `crypto-mcp` rather than to `claude mcp add` — a malformed config block.
3. `docs/mcp-clients.md` — added a `.NET 10 SDK + crypto-mcp` prerequisite line near the top cross-linking `mcp-server.md#install` (addresses api-reviewer LOW finding for users arriving directly).

Post-fix `dotnet build` re-run: PASS. All internal links and anchors (`mcp-server.md#install`, `#credentials`, `getting-started.md`, `../src/...README.md`, `../LICENSE`) verified to resolve.

**Deferred (non-blocking, noted for follow-up — not addressed here):** architect's inline-table-drift CONCERN (tool + error-category tables copied inline rather than purely linked) and the MCP Inspector section appearing in both files. These are structural/DRY preferences, not correctness errors, and were intentionally left to avoid over-editing a shipped doc; candidates for a future docs-consistency pass.
