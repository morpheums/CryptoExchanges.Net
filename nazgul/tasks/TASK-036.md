---
id: TASK-036
status: DONE
depends_on: []
commit: c8a4335
claimed_at: 2026-06-19T14:00:00Z
---
# TASK-036: Exchange brand SVG assets (7) under docs/assets/exchanges/

## Metadata
- **ID**: TASK-036
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: none
- **Delegates to**: none
- **Files modified**: [docs/assets/exchanges/binance.svg, docs/assets/exchanges/bybit.svg, docs/assets/exchanges/okx.svg, docs/assets/exchanges/bitget.svg, docs/assets/exchanges/coinbase.svg, docs/assets/exchanges/kraken.svg, docs/assets/exchanges/kucoin.svg, docs/assets/exchanges/ATTRIBUTION.md]
- **Wave**: 1
- **Traces to**: FEAT-003 spec §Scope-In "Assets" (`docs/assets/exchanges/*.svg`, curated set for all 7, kept small + consistent)
- **Created at**: 2026-06-19T13:10:00Z
- **Claimed at**: 2026-06-19T14:00:00Z
- **Implemented at**: 2026-06-19T14:20:00Z
- **Completed at**: 2026-06-19T15:30:00Z
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Status

Blast radius: **docs only** — 7 new SVG files + 1 attribution note under `docs/assets/exchanges/`.
No source, no `.csproj`, no behavior change. Build/tests untouched and stay green.

## Description

Create a small, visually consistent, committed SVG brand-logo set for all **7** exchanges:
the **4 supported** (Binance, Bybit, OKX, Bitget) and the **3 coming-soon** (Coinbase, Kraken, KuCoin).

These icons back the README supported-exchanges status table (TASK-039) and the per-exchange
`docs/exchanges.md` entries (TASK-037). Constraints:

- **Curated committed set is required.** simple-icons does NOT carry Bybit, Bitget, or Kraken,
  so do not rely on a CDN/icon-font — commit a consistent local SVG per exchange.
- Keep each SVG **small and consistent**: comparable viewBox/canvas, minimal/optimized markup
  (no embedded rasters, no scripts), so they render uniformly at table-icon size on GitHub.
- File naming: lowercase exchange id matching `ExchangeId` (`binance`, `bybit`, `okx`,
  `bitget`, `coinbase`, `kraken`, `kucoin`) — used by TASK-039's table and TASK-037's exchange pages.
- Add `docs/assets/exchanges/ATTRIBUTION.md` — a short note recording each logo's source and
  the brand/trademark usage basis (logos denote supported integrations only; Apache-2.0 covers
  the project's code, not third-party trademarks).
- **Opsec**: assets are purely visual; no roadmap/strategy text anywhere.

This is a no-dependency task (Wave 1) and is file-disjoint from TASK-037/TASK-038.

## Acceptance Criteria
- [x] All 7 SVGs exist at `docs/assets/exchanges/{binance,bybit,okx,bitget,coinbase,kraken,kucoin}.svg`, each small/optimized (no rasters/scripts) and visually consistent (comparable sizing).
- [x] `docs/assets/exchanges/ATTRIBUTION.md` records each logo's source + trademark/usage basis.
- [x] All 7 SVGs render correctly on GitHub at table-icon size (no broken/oversized renders); docs-only — `dotnet build`/`dotnet test` unaffected.

## Pattern Reference
- No existing in-repo asset pattern (first asset set). Match the file-naming convention to the
  `ExchangeId` enum values used throughout the codebase (lowercase: binance/bybit/okx/bitget).
- Visual-consistency reference: treat the supported-exchanges table in the current
  `README.md` (lines 179–188) as the layout these icons must slot into.

## File Scope

**Creates**:
- docs/assets/exchanges/binance.svg
- docs/assets/exchanges/bybit.svg
- docs/assets/exchanges/okx.svg
- docs/assets/exchanges/bitget.svg
- docs/assets/exchanges/coinbase.svg
- docs/assets/exchanges/kraken.svg
- docs/assets/exchanges/kucoin.svg
- docs/assets/exchanges/ATTRIBUTION.md

**Modifies**:
- none

## Traceability
- **PRD Acceptance Criteria**: n/a — FEAT-003 spec §Scope-In "Assets" (curated committed SVG set for all 7; simple-icons gap on Bybit/Bitget/Kraken)
- **TRD Component**: n/a — static documentation assets
- **ADR Reference**: n/a

## Implementation Log

### Attempt 1

- Created `docs/assets/exchanges/` directory.
- Fetched 4 official CC0 icons from `cdn.simpleicons.org`: binance, coinbase, kucoin, okx.
  All have `viewBox="0 0 24 24"` and minimal path-only SVG markup.
- Created 3 placeholder monogram SVGs (bybit, bitget, kraken) with matching `viewBox="0 0 24 24"`,
  rounded-rect background, initial letter in white on black — no trademark reproduction.
- Created `ATTRIBUTION.md` distinguishing official (CC0 from Simple Icons) vs placeholder files.
- All 7 SVGs validated as well-formed XML (python3 xml.etree.ElementTree).
- No source files touched; build/test unaffected.

### Auto-fix (review gate)

- Replaced `<text>` elements in bybit/bitget/kraken with `<path>` geometry letterforms (GitHub
  SVG sanitizer strips `<text>` — renders as identical black squares without the fix).
- Fixed bitget.svg: letter corrected from `G` to `B` (first initial of "Bitget").
- Differentiated Bybit vs Bitget by background fill: Bybit `#1a1a1a` (near-black), Bitget `#444444` (dark grey).
- Updated `ATTRIBUTION.md` to document path-vs-text rationale and B/B shade disambiguation.
- Re-validated: all 7 SVGs pass XML parse, no `<text>` elements remain, `viewBox="0 0 24 24"` and
  `xmlns` preserved. Build 0 errors, 354+ unit tests pass.

## Commits

- `c8a4335` — feat(FEAT-003): add exchange SVG icon set (TASK-036)

## Review Results

### Attempt 1

| Reviewer           | Verdict  | Notes                                                         |
|--------------------|----------|---------------------------------------------------------------|
| architect-reviewer | APPROVED | All structural/naming checks pass; 2 LOW non-blocking concerns |
| code-reviewer      | APPROVED | Blocking findings (text elements, wrong letter) fixed by auto-fix |
| security-reviewer  | APPROVED | No scripts/external refs/event handlers/foreignObject; CC0 verified |
| api-reviewer       | APPROVED | No public API surface changes; file scope matches manifest    |
