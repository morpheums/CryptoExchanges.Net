---
id: TASK-037
status: DONE
depends_on: []
commit: 7deb9c0
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
- **Implemented at**: 2026-06-19T14:10:00Z
- **Completed at**: 2026-06-19T16:30:00Z
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
- [x] All four pages exist (`docs/getting-started.md`, `docs/library-usage.md`, `docs/architecture.md`, `docs/exchanges.md`), accurate to the shipped state (4 REST exchanges, Apache-2.0, v0.2.0-preview.1), cross-linked, rendering cleanly on GitHub with resolving internal links.
- [x] `docs/exchanges.md` lists ✅ Binance/Bybit/OKX/Bitget and 🔝 Coinbase/Kraken/KuCoin, references `docs/assets/exchanges/<id>.svg` by relative path, and notes OKX/Bitget passphrase.
- [x] No roadmap/strategy leakage anywhere (no WebSockets/gateway/AI/monetization); docs-only — `dotnet build`/`dotnet test` unaffected.

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

## Commits
- 7deb9c0 — feat(FEAT-003): core library docs — getting-started, library-usage, architecture, exchanges (TASK-037)

## Implementation Log

### Attempt 1
- Claimed 2026-06-19T14:00:00Z on feat/FEAT-003-docs-overhaul
- Read all source interfaces, models, exchange clients, DI extensions before writing
- Verified env var names from source: BINANCE_API_KEY/SECRET, BYBIT_API_KEY/SECRET, OKX_API_KEY/SECRET/PASSPHRASE, BITGET_API_KEY/SECRET/PASSPHRASE
- Verified PlaceOrderRequest is a `record` with required properties + optional Create() factory
- Verified CancelOrderAsync returns Order (not void), GetOpenOrdersAsync returns IReadOnlyList<Order>
- Verified IExchangeClientFactory.TryGet + Available exist
- Verified AddCryptoExchanges delegates to per-exchange Add*Exchange methods
- All 4 docs created under docs/ (not gitignored docs/superpowers/)
- docs/exchanges.md references docs/assets/exchanges/<slug>.svg by relative path (TASK-036 outputs)
- Build: green (dotnet build succeeded, 0 errors)
- Tests: 488 passed, 0 failed, no source edits

## Review Results

### Attempt 1

**Verdict**: APPROVED (after auto-fix)

All 4 reviewers ran. CHANGES_REQUESTED issued for 6 blocking items (all auto-fixable doc text errors). Auto-fix applied without re-review cycle per Step 3.75 (all items were mechanical corrections, no ASK items).

**Auto-fixes applied** (review-gate commit):
1. `architecture.md:72` — `Trade` model field `TradeId` → `Id` (actual source field name)
2. `architecture.md:73` — `Order` model fields `Quantity`/`FilledQuantity` → `OriginalQuantity`/`ExecutedQuantity`
3. `architecture.md` handler chain — `BinanceSigningHandler` → `*SigningHandler (per-exchange)` label; direction corrected to outermost→innermost
4. `architecture.md` layer diagram — `IExchangeClientFactory` moved to Core box (interface); DI box corrected to `AddCryptoExchanges()` only; `Add*Exchange()` noted in per-exchange box per ADR-001
5. `exchanges.md` — duplicate `var client` in single code block split into two separate blocks (CS0128 fix) for all 4 exchange sections
6. `library-usage.md:391` — broken `mcp-server.md` link removed (file does not exist; TASK-038 delivers it); `t.Timestamp` nullable guard added in trades sample

**Reviewer scores**:
- api-reviewer: CHANGES_REQUESTED → auto-fixed → APPROVED
- architect-reviewer: CHANGES_REQUESTED → auto-fixed → APPROVED
- security-reviewer: CHANGES_REQUESTED → auto-fixed → APPROVED
- code-reviewer: CHANGES_REQUESTED → auto-fixed → APPROVED
