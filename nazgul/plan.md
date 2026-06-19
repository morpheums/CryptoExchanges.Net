# Nazgul Plan — FEAT-002

─── ◈ NAZGUL ▸ PLANNING ────────────────────────────────

## Objective

Ship **`CryptoExchanges.Net.Mcp`** — a local (stdio) MCP server that exposes the
existing REST library to LLM agents as **read-only** tools (6 market-data + 6
read-scoped account) across Binance / Bybit / OKX / Bitget, using the canonical
`Core.Models` so one agent vocabulary works identically across all venues.

This is sub-project A of the AI-agent-native roadmap. It is a faithful execution of
the APPROVED design + plan — no re-design:
- Approved design: `docs/superpowers/specs/2026-06-19-mcp-server-readonly-v1-design.md`
- Approved plan (5 tasks, exact code + TDD steps): `docs/superpowers/plans/2026-06-19-mcp-server-readonly-v1.md`

## Branch

- **Base**: `main`
- **Feature**: `feat/FEAT-002-mcp-server-readonly` (already created)
- Ship as one squash-merge PR to protected `main`.

## Discovery Status

REUSE existing discovery — do NOT re-run.
- Discovery last run: 2026-06-17 (`nazgul/context/`, 83 files scanned).
- Reviewers (4, existing — do NOT regenerate): `architect-reviewer`, `code-reviewer`,
  `security-reviewer`, `api-reviewer`.
- Classification: BROWNFIELD (HIGH confidence). This adds a new console-host project
  on top of the verified Core → Http → Exchange → DI architecture; no Core changes.

## Hard Constraints (recorded for implementer + reviewers)

- **Target**: `net10.0` single-target (inherited from `Directory.Build.props`).
- **Build hygiene (inherited)**: `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-all`,
  `Nullable=enable`, `ImplicitUsings=enable`. Build MUST stay **0 warnings / 0 errors**.
- **Doc-gen**: the MCP project sets `<GenerateDocumentationFile>false</GenerateDocumentationFile>`
  (it is an app/tool, not a documented library API — avoids CS1591 on public tool types
  under warnings-as-errors). Tool/param docs are carried by `[Description]` attributes.
- **READ-ONLY is structural**: only market-data + read-scoped account tools exist. **No**
  order-placement/cancel tool is written anywhere. TASK-032 adds a reflection guard test
  asserting **exactly 12 tools** and **zero write-named tools**.
- **Reuse existing libraries** via `IExchangeClientFactory` + `AddCryptoExchanges`. **No new
  exchange logic, no Core changes** — the MCP layer is a thin facade over existing
  `IExchangeClient` reads.
- **Tools are static methods** taking `IExchangeClientFactory` as a **method parameter**
  (SDK service injection) and returning `Task<ToolResult<T>>`.
- **Nothing throws across the MCP boundary**: every tool returns `ToolResult<T>`; failures
  become structured `ToolError` (categories: AuthRequired, RateLimited, ExchangeUnavailable,
  Connectivity, SymbolNotSupported, ExchangeError, BadInterval, Unknown).
- **Keys are local only**: read from env vars in `Program.cs`; never logged. Logging goes to
  **stderr** (stdout is the MCP channel).
- **Existing suite stays green**: the current **455 tests** must still pass; new tests are
  added in `tests/CryptoExchanges.Net.Mcp.Tests.Unit`.
- **SDK version verification (TASK-028 FIRST step)**: confirm the latest `ModelContextProtocol`
  package version with `dotnet add ... package ModelContextProtocol --prerelease` and pin
  whatever it resolves to before writing tool code (the plan's `0.4.0-preview.1` is a
  placeholder to reconcile). Same for `Microsoft.Extensions.Hosting` (latest stable resolved).
- **Out of scope**: writes, WebSockets, hosted/HTTP transport, multi-tenant key custody,
  commercial gateway, and the **AGPL relicense** (that is a SEPARATE PR — do not change
  license metadata here).
- **Build / test commands** (repo root):
  - `dotnet build CryptoExchanges.Net.sln -c Release`
  - `dotnet test tests/CryptoExchanges.Net.Mcp.Tests.Unit/CryptoExchanges.Net.Mcp.Tests.Unit.csproj -c Release`

## Status Summary

| Task     | Status  | Wave | Description                                          |
|----------|---------|------|------------------------------------------------------|
| TASK-028 | ✦ DONE | 1    | Project scaffold + host wiring + env→options binder  |
| TASK-029 | ✦ DONE | 2    | Tool primitives (ToolResult, ToolInputs, ToolRunner) |
| TASK-030 | ✦ DONE | 3  | Market-data tools (6, no keys)                    |
| TASK-031 | ✦ DONE | 3  | Account tools (6, read-scoped keys)                  |
| TASK-032 | ✦ DONE | 4  | Tool-roster guard test + README/packaging            |

Tasks: 5/5 DONE

## Wave Groups

The loop orchestrator reads this section to determine parallel execution order.

### Wave 1
- TASK-028 (no dependencies — creates the MCP + test projects and host wiring)

### Wave 2
- TASK-029 (depends on TASK-028; adds tool primitives to `src/CryptoExchanges.Net.Mcp/`)

### Wave 3
- TASK-030, TASK-031 (both depend on TASK-029; independent of each other — disjoint files:
  TASK-030 touches `Tools/MarketDataTools.cs` + its test, TASK-031 touches
  `Tools/AccountTools.cs` + its test; no overlap, so they run in parallel)

### Wave 4
- TASK-032 (depends on TASK-030 AND TASK-031 — the roster guard test asserts the 12-tool
  surface produced by both, plus README/packaging)

## Dependency Order

```
TASK-028  ──►  TASK-029  ──►  TASK-030  ──┐
                          └─►  TASK-031  ──┴──►  TASK-032
```

## PRD Traceability

No formal PRD/TRD/ADR document set was generated for FEAT-002. The authoritative
acceptance source is the APPROVED design + plan. Each task's `Traces to` field points to
the specific section of those documents it fulfills. Coverage check (from the plan author's
self-review, re-verified): host/scaffold → TASK-028; tool primitives + error mapping →
TASK-029; 6 market-data tools → TASK-030; 6 account tools + AuthRequired → TASK-031;
read-only-structural guard + packaging/README → TASK-032. No design requirement is left
unmapped; writes/WebSockets/hosted-transport are correctly absent.

## Completed

- TASK-028 (Wave 1) — scaffold + host wiring. DONE.
- TASK-029 (Wave 2) — tool primitives (ToolResult/ToolInputs/ToolRunner). DONE. Completion SHA: 66039b1.
- TASK-030 (Wave 3) — 6 read-only market-data tools. DONE (review board PASS; fix-first added 8h/3d intervals). Completion SHA: 290869b.
- TASK-031 (Wave 3) — 6 read-only account tools. DONE (review board PASS, cycle 2 — all 4 reviewers APPROVED). Completion SHA: 2de728c.
- TASK-032 (Wave 4) — tool-roster guard test + README/packaging. DONE (review board PASS, cycle 1 — all 4 reviewers APPROVED). Implementation SHA: 3432f08.

## Recovery Pointer

- **Current stage**: ALL 5 TASKS DONE. Post-loop phase next (post-loop simplify → PR).
- **Next action**: Post-loop simplify pass + create PR for feat/FEAT-002-mcp-server-readonly → main.
- **Active task**: none — all tasks DONE.
- **Files are truth**: task manifests in `nazgul/tasks/TASK-028..032.md` carry full state;
  frontmatter `status:` is canonical.

─── ◈ NEXT ─────────────────────────────────────────────
  All 5 tasks DONE — post-loop phase (simplify + PR)
  /nazgul:start to trigger post-loop
────────────────────────────────────────────────────────
