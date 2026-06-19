---
id: TASK-039
status: PLANNED
depends_on: [TASK-036, TASK-037, TASK-038]
commit:
claimed_at:
---
# TASK-039: README rewrite (lean) — links into docs/, uses icons, status table

## Metadata
- **ID**: TASK-039
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-036, TASK-037, TASK-038
- **Delegates to**: none
- **Files modified**: [README.md]
- **Wave**: 2
- **Traces to**: FEAT-003 spec §Scope-In "Lean README" (tagline+badges, 60s quick-start, supported-exchanges icon/status table, MCP blurb, links into docs/)
- **Created at**: 2026-06-19T13:10:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Status

Blast radius: **docs only** — rewrites the repo-root `README.md`. No source, no `.csproj`,
no behavior change. Build/tests untouched and stay green.

Depends on TASK-036 (icons), TASK-037 (core docs to link), TASK-038 (MCP docs to link) — it
references the icon files and links into every new `docs/` page, so those must exist first.

## Description

Rewrite the repo-root `README.md` to be **lean and scannable** — the project's first
impression and the MCP onboarding funnel. The current README (250+ lines) is bloated and
partly stale (it lists Coinbase/Bybit/OKX/Kraken as "planned" and carries a leaky Roadmap
section). Trim it; **relocate** accurate prose into the `docs/` pages (TASK-037/038 already
host the detail) rather than deleting it.

New README structure:
- **Tagline + badges** — keep the one-line tagline; badges for NuGet (v0.2.0-preview.1),
  License (Apache-2.0), .NET 10.
- **60-second quick-start** — install one package + one library call + the one-line MCP install
  (`dotnet tool install -g CryptoExchanges.Net.Mcp`). Minimal, copy-pasteable.
- **Supported-exchanges table** — uses the TASK-036 icons (`docs/assets/exchanges/<id>.svg`)
  with status: **✅ supported** Binance/Bybit/OKX/Bitget · **🔝 coming soon** Coinbase/Kraken/KuCoin.
  (Replaces the stale current table at lines 179–188.)
- **MCP one-liner + link** — short blurb (read-only stdio `crypto-mcp`, 12 tools, 4 exchanges),
  link to `docs/mcp-server.md` + `docs/mcp-clients.md`.
- **Links into docs/** — clear links to `docs/getting-started.md`, `docs/library-usage.md`,
  `docs/architecture.md`, `docs/exchanges.md`, `docs/mcp-server.md`, `docs/mcp-clients.md`.

**Remove / relocate**:
- Delete the **Roadmap** section entirely (opsec — WebSockets/MCP-wrapper/rate-limiting/caching/
  "Vigilex DNA" are roadmap/strategy leakage; "coming soon" = exchanges only).
- Trim the long Philosophy / Architecture / Supported-Operations / Project-Structure prose —
  move the keepers into the `docs/` pages.
- Fix stale facts: 4 supported exchanges (not "Binance only + planned"); REST-only; Apache-2.0;
  v0.2.0-preview.1; read-only MCP.

Constraints:
- **Accurate to shipped state**; **no roadmap/strategy leakage**; renders cleanly on GitHub;
  all internal links (docs pages + icon paths) resolve.
- Keep the License + attribution footer.

## Acceptance Criteria
- [ ] README is visibly leaner and scannable: tagline + badges, 60-second quick-start (install + one library call + one-line MCP install), and clear links into all six `docs/` pages — all internal links resolve on GitHub.
- [ ] Supported-exchanges table renders the TASK-036 icons for all 7 with correct status (✅ Binance/Bybit/OKX/Bitget · 🔝 Coinbase/Kraken/KuCoin); MCP blurb links to `docs/mcp-server.md` + `docs/mcp-clients.md`.
- [ ] No roadmap/strategy leakage (Roadmap section removed; no WebSockets/gateway/AI/monetization); facts accurate (4 REST exchanges, Apache-2.0, v0.2.0-preview.1, read-only MCP); docs-only — `dotnet build`/`dotnet test` unaffected.

## Pattern Reference
- File to rewrite: current `README.md` (full file) — keep the tagline (lines 1–9), the accurate
  quick-start (56–107), and the MCP section shape (220–237); drop Roadmap (206–218) and the
  stale exchange table (179–188).
- Icon paths: `docs/assets/exchanges/<id>.svg` from TASK-036.
- Link targets: the six pages from TASK-037 (`getting-started`, `library-usage`, `architecture`,
  `exchanges`) and TASK-038 (`mcp-server`, `mcp-clients`).
- The lean MCP doc voice: `src/CryptoExchanges.Net.Mcp/README.md`.

## File Scope

**Creates**:
- none

**Modifies**:
- README.md (repo root — full lean rewrite)

## Traceability
- **PRD Acceptance Criteria**: n/a — FEAT-003 spec §Scope-In "Lean README" + §Success criteria (leaner/scannable, accurate, no leakage, icon status table)
- **TRD Component**: n/a — links to TASK-037/TASK-038 docs; uses TASK-036 assets
- **ADR Reference**: n/a

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
