---
id: TASK-041
status: DONE
commit: bd15a22
claimed_at: 2026-06-19
---

# TASK-041: Fix Bybit icon + cache-bust README icon URLs (FEAT-004 follow-up)

**Status**: READY

**Blast radius**: NONE (docs/assets only).

## Scope
Follow-up to PR #22 after on-GitHub review:
1. **Bybit icon rendered as "B|U"** — the recreated mark drew a stray "U" glyph. Redesign `bybit-light.svg`/`bybit-dark.svg` to a clean theme-aware "B" + orange accent (no "U"). Still an original stand-in for the official wordmark.
2. **GitHub image cache served stale icons** — coinbase/bitget/kraken were correct on main but GitHub's camo cache served the old same-named SVGs. Append a `?v=2` cache-bust query to all exchange icon URLs in the README (`<img src>` + `<picture><source srcset>`) so GitHub re-fetches.

## Acceptance
- README icon URLs carry `?v=2`; Bybit no longer renders "B|U"; all SVGs valid; build 0W/0E; docs-only.
