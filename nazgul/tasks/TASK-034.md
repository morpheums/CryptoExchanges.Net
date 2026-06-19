---
id: TASK-034
status: IN_PROGRESS
commit:
claimed_at: 2026-06-19
---

# TASK-034: Revert split licensing — all packages Apache-2.0 for now

**Status**: READY

**Blast radius**: NONE (license metadata/docs only; no code/behavior).

## Scope
Decision revised: AGPL on the local-stdio MCP server is dormant (no network service), so defer any
AGPL/commercial/BSL until a real hosted product exists. Make ALL packages Apache-2.0:
- Remove `src/CryptoExchanges.Net.Mcp/LICENSE` (AGPL) — done.
- Remove the `PackageLicenseExpression` AGPL override from the MCP csproj (inherit Apache-2.0).
- Revert README license section + Directory.Build.props comment.
- Remove the CHANGELOG "split licensing" entry (keep the limit-guard entry).
Forward-revert on PR #19 (split commit already pushed; squash-merge collapses it).

## Acceptance
- All packages report Apache-2.0 (MCP nuspec = Apache-2.0); build 0W/0E; tests green; pack OK.
