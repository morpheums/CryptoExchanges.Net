---
id: TASK-037
status: IN_PROGRESS
depends_on: []
commit:
claimed_at: 2026-06-19T14:00:00Z
---
# TASK-037: Core library docs (getting-started, library-usage, architecture, exchanges)

## Metadata
- **ID**: TASK-037
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: none
- **Delegates to**: none
- **Files modified**: [docs/getting-started.md, docs/library-usage.md, docs/architecture.md, docs/exchanges.md]
- **Wave**: 1
- **Traces to**: FEAT-003 spec §Scope-In "New public docs/ folder" (getting-started, library-usage, architecture, exchanges)
- **Created at**: 2026-06-19T13:10:00Z
- **Claimed at**: 2026-06-19T14:00:00Z
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Status

Blast radius: **docs only** — 4 new markdown pages under public `docs/`. No source, no `.csproj`,
no behavior change. Build/tests untouched and stay green.

## Description

Create the public core-library documentation pages (markdown, in-repo; distinct from the
gitignored `docs/superpowers/`). Relocate accurate prose here from the bloated README (don't
delete useful content — move it). All four pages cross-link each other and will be linked from
the README (TASK-039).

Pages to create:

- **`docs/getting-started.md`** — install (`dotnet add package CryptoExchanges.Net.Binance`,
  and per-exchange packages), configuration, and first calls (direct `Create` + ASP.NET Core
  DI via `AddCryptoExchanges` / `IExchangeClientFactory` / keyed services). Mirror the accurate
  examples in the current README (lines 56–107).
- **`docs/library-usage.md`** — fuller examples: market data (price, tickers, order book,
  candles, exchange info), trading (place/cancel/open orders), account (balances, single
  balance, trade history), DI, error handling, and the typed Symbol/Asset model
  (`new Symbol(Asset.Btc, Asset.Usdt)`, `Asset.Of("...")`, `IsSupportedAsync`/`ResolveSymbolAsync`).
  Build on the current README examples (lines 109–177) and expand them.
- **`docs/architecture.md`** — concise: the Core → Http → Exchange → DI layering, the canonical
  `Core.Models`, DeltaMapper (DTO→model), the bespoke `ISymbolMapper`, and signing
  (per-exchange signature service + signing handler). Keep it tight; source it from
  `nazgul/context/architecture-map.md`. **No roadmap/strategy** — describe what ships today only.
- **`docs/exchanges.md`** — per-exchange support detail + status. **Supported (✅)**: Binance,
  Bybit, OKX, Bitget — REST, with each exchange's package id and credential requirements (note
  OKX/Bitget need a passphrase). **Coming soon (🔝)**: Coinbase, Kraken, KuCoin (present in the
  `ExchangeId` enum, not yet implemented). Reference the icons from TASK-036
  (`docs/assets/exchanges/<id>.svg`) by relative path. "Coming soon" = **exchanges only**.

Constraints:
- **Accurate to shipped state**: 4 supported exchanges, REST-only, Apache-2.0, v0.2.0-preview.1.
- **Opsec**: NO WebSockets, gateway, AI/agent positioning, monetization, or competitive content.
- Internal links must resolve and render cleanly on GitHub.

No-dependency task (Wave 1); file-disjoint from TASK-036/TASK-038. (References TASK-036 icons by
path only — does not require them to exist to author the markdown, but link targets should match
TASK-036's filenames.)

## Acceptance Criteria
- [ ] All four pages exist (`docs/getting-started.md`, `docs/library-usage.md`, `docs/architecture.md`, `docs/exchanges.md`), accurate to the shipped state (4 REST exchanges, Apache-2.0, v0.2.0-preview.1), cross-linked, rendering cleanly on GitHub with resolving internal links.
- [ ] `docs/exchanges.md` lists ✅ Binance/Bybit/OKX/Bitget and 🔝 Coinbase/Kraken/KuCoin, references `docs/assets/exchanges/<id>.svg` by relative path, and notes OKX/Bitget passphrase.
- [ ] No roadmap/strategy leakage anywhere (no WebSockets/gateway/AI/monetization); docs-only — `dotnet build`/`dotnet test` unaffected.

## Pattern Reference
- Accurate, reusable prose + code examples: current `README.md` lines 56–177 (relocate/expand these).
- Architecture source of truth: `nazgul/context/architecture-map.md` (Core→Http→Exchange→DI, DeltaMapper, ISymbolMapper, signing).
- Style/tone: `nazgul/context/style-conventions.md`; existing `src/CryptoExchanges.Net.Mcp/README.md` for doc voice.

## File Scope

**Creates**:
- docs/getting-started.md
- docs/library-usage.md
- docs/architecture.md
- docs/exchanges.md

**Modifies**:
- none

## Traceability
- **PRD Acceptance Criteria**: n/a — FEAT-003 spec §Scope-In "New public docs/ folder" (getting-started, library-usage, exchanges, architecture)
- **TRD Component**: n/a — `nazgul/context/architecture-map.md` is the architecture source
- **ADR Reference**: n/a

- **Base SHA**: e17d246

## Implementation Log

### Attempt 1
- Claimed 2026-06-19T14:00:00Z on feat/FEAT-003-docs-overhaul
- Verified all API signatures from source before writing docs
- Creating 4 markdown docs under public docs/

## Review Results

### Attempt 1
