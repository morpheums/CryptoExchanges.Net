# FEAT-003 — Documentation & MCP-onboarding Overhaul

## Objective
Turn the project's public-facing docs into a polished, pro-level experience: a lean, scannable README that links out to a new public `docs/` folder, a supported-exchanges status table with committed brand icons, multi-client MCP setup guides, and more usage examples.

## Objective type
Documentation / polish (no production code or behavior changes).

## Purpose
The current README is bloated, out of date, and visually weak. As the project's first impression (it's a public repo and the MCP tool's onboarding surface), it should communicate value in seconds and route detail to dedicated docs. Good onboarding for the MCP server across the major AI clients directly drives adoption — the funnel for everything else.

## Scope — In
- **Lean README**: tagline + badges; a 60-second quick-start (install + one library call + the one-line MCP install); a **supported-exchanges table** with committed SVG brand icons and status (✅ supported: Binance/Bybit/OKX/Bitget · 🔝 coming soon: Coinbase/Kraken/KuCoin); a short MCP blurb; clear links into `docs/`. Trim wordiness — most prose moves to `docs/`.
- **New public `docs/` folder** (markdown; distinct from the gitignored `docs/superpowers/`):
  - `getting-started.md` — install, configuration, first calls.
  - `library-usage.md` — fuller examples (market data, trading, account, DI, error handling, symbol model).
  - `mcp-server.md` — what the MCP server is, the 12 tools, env-var credentials, error categories.
  - `mcp-clients.md` — per-client setup for the **major** MCP clients: Claude Code, Claude Desktop, Cursor, VS Code (Copilot), Windsurf, Cline, Codex, Gemini CLI. README shows only the Claude Code one-liner and links here (mirrors how popular MCP servers do it: cover majors, link out for the long tail).
  - `exchanges.md` — per-exchange support detail + status.
  - `architecture.md` — the Core→Http→Exchange→DI layering, canonical model, DeltaMapper, signing (concise).
- **Assets**: `docs/assets/exchanges/*.svg` — small, curated brand logos for all 7 exchanges (simple-icons lacks Bybit/Bitget/Kraken, so a committed curated set is used for visual consistency). Logos denote supported integrations; kept small and linked to each exchange.
- Keep README content accurate to the current shipped state (4 exchanges, REST, read-only MCP, Apache-2.0, v0.2.0-preview.1).

## Scope — Out
- **No public feature roadmap** — do NOT mention WebSockets, a gateway, AI/agent positioning, monetization, or competitive analysis in any public artifact (opsec). "Coming soon" applies to **exchanges only**.
- No production source/behavior changes; no new library features.
- No docs-site generator/hosting (GitHub Pages/Docusaurus) — in-repo markdown only for now.
- WebSockets remains parked for a later objective.

## Success criteria
- README is visibly leaner and scannable; renders cleanly on GitHub with the exchange-icon status table; no stale/incorrect info; no roadmap/strategy leakage.
- `docs/` exists with the listed pages, each accurate and cross-linked from the README.
- MCP setup is documented for the major clients with a correct, copy-pasteable config per client.
- Exchange icons render for all 7 (4 supported + 3 coming-soon), visually consistent.
- Build/tests unaffected (docs-only); the existing suite stays green.

## Notes / constraints
- Public repo → every artifact strictly technical (no strategy).
- Verify each client's MCP config format against current docs before publishing (formats differ per client and change).
- Trim, don't delete useful accurate content — relocate it into `docs/`.
