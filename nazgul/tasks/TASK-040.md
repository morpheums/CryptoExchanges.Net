---
id: TASK-040
status: DONE
commit: d281ea4
claimed_at: 2026-06-19
---

# TASK-040: README icon + tagline polish (FEAT-004)

**Status**: READY

**Blast radius**: NONE (README + docs assets only; no source/behavior change).

## Scope
Replace the weak exchange icons with real colored logos + fix the headline:
- **Coinbase**: blue badge + white "C", fixed all themes.
- **Bitget**: real colored mark (teal badge + black chevron), fixed all themes.
- **OKX**: theme-aware — black (light) / white (dark) via GitHub `<picture>` + prefers-color-scheme.
- **Bybit**: theme-aware wordmark — black (light) / white (dark), orange "I" accent.
- **Binance**: official, brand gold (readable both themes).
- **KuCoin**: official green (simple-icons).
- **Kraken** (coming soon): brand purple, theme-safe.
- Replace the 🔝 "coming soon" marker with a cleaner one (plain text / neutral).
- **Headline**: surface the shipped MCP server in the README tagline (factual — no roadmap leak).

Note: Binance/Coinbase/OKX/KuCoin are in simple-icons (official paths); Bybit/Bitget/Kraken are NOT — hand-authored faithful recreations, flagged in ATTRIBUTION. Implemented directly with a rendered light/dark preview for human confirmation before PR (visual/taste work — full reviewer board not used).

## Acceptance
- README renders cleanly on GitHub light AND dark themes; every icon legible on both; OKX/Bybit switch with theme; Coinbase/Bitget colored; coming-soon marker clean; tagline mentions the MCP server. No source changes; build/tests unaffected.
