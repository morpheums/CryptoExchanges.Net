---
id: TASK-033
status: DONE
commit: 9630611
claimed_at: 2026-06-19
---

# TASK-033: Fold-ins for PR #19 — AGPL relicense + limit lower-bound guard

**Status**: IMPLEMENTED — ships with PR #19 (6b0cd43 license-split, 9630611 limit-guard). Decision changed from solution-wide AGPL to SPLIT: libraries Apache-2.0, MCP server AGPL-3.0. Build 0W/0E; 50 MCP tests; pack OK.

**Blast radius**: LOW. (1) License metadata + LICENSE file swap (no code behavior). (2) Small input-validation guards on count parameters across 5 MCP tools + tests. The 499-test suite must stay green; build 0W/0E.

## Scope (two fold-ins onto the open FEAT-002 PR branch)
1. **AGPL relicense** (decided earlier): replace `LICENSE` with the official GNU AGPL-3.0 text; set `<PackageLicenseExpression>AGPL-3.0-or-later</PackageLicenseExpression>` in `Directory.Build.props` (currently `Apache-2.0`); update the README license note. Rationale: open-core wedge — open+auditable library, but a hosted competitor can't SaaS-strip it.
2. **limit/depth lower-bound guard** (review follow-up from TASK-031, api CONCERN): integer count parameters that currently flow unchecked to the exchange API and surface as opaque `ExchangeError` should validate `< 1` → structured `ToolError("BadRequest", ...)`. Apply consistently to: `MarketDataTools.GetOrderBook(depth)`, `GetKlines(limit)`, `GetRecentTrades(limit)`, `AccountTools.GetOrderHistory(limit)`, `GetTradeHistory(limit)`. Add tests asserting the `BadRequest` category for a non-positive value.

## Acceptance
- Build 0W/0E (Release, TreatWarningsAsErrors); all 499 + new guard tests pass.
- `dotnet pack` still succeeds; NuGet license metadata = AGPL-3.0-or-later.
- LICENSE recognized as AGPL-3.0 by GitHub (official text).
