---
id: TASK-054
status: IMPLEMENTED
---

# TASK-054: Drop FluentAssertions (v8+ is paid) → AwesomeAssertions; remove unused Newtonsoft.Json

**Status**: READY

**Blast radius**: TEST-ONLY. No product code or package change.

## Why
- Dependabot PR #10 tried to bump FluentAssertions 6.12.2 → 8.10.0. FA v8+ is commercially licensed
  (v7 last free) and broke the build. Closed #10.
- Migrate all 12 test projects to **AwesomeAssertions** (free MIT fork of FA7, `.Should()` API).
- `Newtonsoft.Json` is an unused stray reference in `Core.Tests.Unit` (no `.cs` uses it; production is
  100% System.Text.Json) — remove it.

## Plan
1. Verify AwesomeAssertions is namespace-compatible (keeps `FluentAssertions` namespace → package-only swap).
2. Swap `FluentAssertions` → `AwesomeAssertions` (9.4.0) in all 12 test `.csproj`; adjust `using` only if needed.
3. Remove the `Newtonsoft.Json` reference from `Core.Tests.Unit`.
4. Build + run full non-integration suite green.

## Acceptance
- 0 FluentAssertions / Newtonsoft references remain; full suite green; 0W/0E.

## Result
- AwesomeAssertions 9.4.0 across all 12 test projects; 40 `using` directives swapped; 2 FA7 method
  renames (`Be{Less,Greater}OrEqualTo` → `Be{Less,Greater}ThanOrEqualTo`); Newtonsoft removed.
- Build 0W/0E; full non-integration suite green (Core 101, Http 83, Okx 93, Bitget 90, Bybit 77,
  Mcp 50, Binance 21, DI 13).

## Commits
- `992c0d0` — migrate FluentAssertions → AwesomeAssertions; drop unused Newtonsoft.Json.
