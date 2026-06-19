# Nazgul Plan — FEAT-003

─── ◈ NAZGUL ▸ PLANNING ────────────────────────────────

## Objective

**Documentation & MCP-onboarding overhaul.** Turn the project's public-facing docs into
a polished, pro-level experience: a lean, scannable README that links out to a new public
`docs/` folder, a supported-exchanges status table with committed brand icons, multi-client
MCP setup guides, and fuller usage examples.

**Objective type**: Documentation / polish — **NO production source or behavior changes.**

Authoritative spec: `nazgul/context/objectives/FEAT-003-spec.md` (read it fully before any task).

## Branch

- **Base**: `main`
- **Feature**: `feat/FEAT-003-docs-overhaul` (to be created)
- Ship as one squash-merge PR to protected `main`.

## Discovery Status

REUSE existing discovery — do NOT re-run.
- Discovery last run: 2026-06-17 (`nazgul/context/`, 83 files scanned).
- Reviewers (4, existing — do NOT regenerate): `architect-reviewer`, `code-reviewer`,
  `security-reviewer`, `api-reviewer`.
- Classification: BROWNFIELD (HIGH confidence). This objective is docs-only on top of the
  shipped Core → Http → Exchange → DI library + the read-only MCP server.

## Shipped State (docs MUST stay accurate to this)

- **4 supported exchanges**: Binance, Bybit, OKX, Bitget (REST-only).
- **3 coming-soon exchanges** (in the `ExchangeId` enum, not yet implemented): Coinbase, Kraken, KuCoin.
- **MCP**: read-only stdio server, tool command `crypto-mcp`, **12 tools** (6 market-data + 6 account).
- **License**: Apache-2.0. **Version**: v0.2.0-preview.1.

## Hard Constraints (recorded for implementer + reviewers)

- **Docs-only.** No `.cs`/`.csproj`/source edits anywhere. `dotnet build` / `dotnet test`
  must remain green and **unchanged** (nothing in `src/` or `tests/` is touched, except the
  existing `src/CryptoExchanges.Net.Mcp/README.md` may be linked/reused but NOT rewritten — reuse, don't duplicate).
- **No feature-roadmap / strategy leakage** (opsec). Do NOT mention WebSockets, a gateway,
  AI/agent positioning, monetization, or competitive analysis in ANY public artifact.
  "Coming soon" applies to **exchanges only** (Coinbase/Kraken/KuCoin).
- **Public `docs/`** is distinct from the gitignored `docs/superpowers/`. New files go directly
  under `docs/` and `docs/assets/exchanges/`.
- **Reuse, don't duplicate** the existing `src/CryptoExchanges.Net.Mcp/README.md` (already documents
  the 12 tools, env vars, error categories) — link to / mirror it, don't fork its content.
- **Verify per-client MCP config formats** against each client's CURRENT docs before publishing
  (formats differ per client and change over time).
- **Curated SVG icon set required**: simple-icons lacks Bybit/Bitget/Kraken, so a committed,
  consistent, small SVG set for all 7 exchanges is mandatory, with a short attribution note.
- **Renders cleanly on GitHub**: all internal links resolve; icons display.

## Status Summary

| Task     | Status   | Wave | Description                                                        |
|----------|----------|------|--------------------------------------------------------------------|
| TASK-036 | ◇ READY  | 1    | Exchange brand SVG assets (7) under `docs/assets/exchanges/`       |
| TASK-037 | ◇ READY  | 1    | Core library docs (getting-started, library-usage, architecture, exchanges) |
| TASK-038 | ◇ READY  | 1    | MCP docs (mcp-server.md + mcp-clients.md, major clients)           |
| TASK-039 | ◇ PLANNED| 2    | README rewrite (lean) — links into docs/, uses icons, status table |

Tasks: 0/4 DONE

## Wave Groups

The loop orchestrator reads this section to determine parallel execution order.

### Wave 1
- **TASK-036**, **TASK-037**, **TASK-038** — all independent (no dependencies) and file-disjoint:
  - TASK-036 → only `docs/assets/exchanges/*.svg` (+ attribution note).
  - TASK-037 → only `docs/getting-started.md`, `docs/library-usage.md`, `docs/architecture.md`, `docs/exchanges.md`.
  - TASK-038 → only `docs/mcp-server.md`, `docs/mcp-clients.md`.
  - No shared files → safe to run in parallel.

### Wave 2
- **TASK-039** — README rewrite. Depends on TASK-036 (icons), TASK-037 (core docs to link),
  and TASK-038 (MCP docs to link). Modifies only repo-root `README.md`.

## Dependency Order

```
TASK-036 ──┐
TASK-037 ──┼──►  TASK-039
TASK-038 ──┘
```

## PRD Traceability

No formal PRD/TRD/ADR document set was generated for FEAT-003. The authoritative acceptance
source is the spec (`nazgul/context/objectives/FEAT-003-spec.md`). Each task's `Traces to`
field points to the specific spec section it fulfills. Coverage check:

- Spec §Scope-In "Assets" / curated SVG set → **TASK-036**.
- Spec §Scope-In "New public docs/ folder" (getting-started, library-usage, architecture, exchanges) → **TASK-037**.
- Spec §Scope-In "mcp-server.md" + "mcp-clients.md" (major clients) → **TASK-038**.
- Spec §Scope-In "Lean README" (tagline+badges, 60s quick-start, exchange status table, MCP blurb, links into docs/) → **TASK-039**.

Objective-level acceptance (verified across the task set):
- Renders cleanly on GitHub; all internal links resolve; exchange icons display for all 7 → TASK-036 (icons) + TASK-039 (table) + TASK-037/038 (link targets).
- README visibly leaner; accurate to shipped state; **no roadmap/strategy leakage** → TASK-039 (+ all tasks bound by the opsec constraint).
- Per-client MCP configs correct and copy-pasteable → TASK-038.
- Docs-only: build/test still green; no source edits → all tasks (Blast radius: docs only).

Every spec scope item maps to at least one task; nothing in Scope-Out (roadmap, source changes,
docs-site generator, WebSockets) is planned.

## Recovery Pointer

- **Current stage**: PLANNING COMPLETE. Ready for implementation (Wave 1).
- **Next action**: Create branch `feat/FEAT-003-docs-overhaul` off `main`, then start Wave 1
  (TASK-036, TASK-037, TASK-038 in parallel — file-disjoint).
- **Active task**: none — all tasks PLANNED/READY.
- **Files are truth**: task manifests in `nazgul/tasks/TASK-036..039.md` carry full state;
  frontmatter `status:` is canonical.

─── ◈ NEXT ─────────────────────────────────────────────
  Wave 1 ready — TASK-036, TASK-037, TASK-038 (parallel, docs-only)
  /nazgul:start to begin implementation
────────────────────────────────────────────────────────
