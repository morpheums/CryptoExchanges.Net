---
id: TASK-064
status: IN_PROGRESS
depends_on: [TASK-060, TASK-062]
---
# TASK-064: Docs — README KuCoin row → supported + MCP/exchanges reference

## Metadata
- **ID**: TASK-064
- **Group**: 7
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-060, TASK-062
- **Delegates to**: none
- **Files modified**: [README.md, docs/exchanges.md, docs/mcp-server.md, docs/streaming.md, src/CryptoExchanges.Net.Mcp/ToolInputs.cs, src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs]
- **Wave**: 7
- **Traces to**: PRD-FEAT-006 AC-9; TRD-FEAT-006 §"MCP Wiring"; FEAT-006 spec §"DI + MCP", §"Build approach" step 9, §"Success criteria"
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**: 2026-06-21T00:00:00Z
- **Base SHA**: b6a59569331a0d09eb409ef4daa8c15fd1c2bd27
- **Implemented at**: 2026-06-21T00:15:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 1/3
- **Test failures**: 0

## Description

Flip the KuCoin row from "Coming soon" to supported and reflect KuCoin in the MCP/exchanges/streaming
docs. Strictly technical content — no roadmap, gateway, competitive, or monetization language (public
repo; opsec). The `docs/assets/exchanges/kucoin.svg` icon already exists.

Modify:
- **`README.md`** — line ~25 Supported Exchanges table: change the KuCoin row from
  `🕓 Coming soon | —` to `✅ Supported` + the NuGet version badge (mirror the Bitget row exactly,
  swapping the package id to `CryptoExchanges.Net.Kucoin`). Update the "All four supported exchanges…"
  prose (line ~67) to "five" (or reword to a count-agnostic phrasing). Add KuCoin to any inline
  supported-exchange list.
- **`docs/exchanges.md`** — add a KuCoin section mirroring the OKX/Bitget entries: credentials
  (`KUCOIN_API_KEY` / `KUCOIN_SECRET_KEY` / `KUCOIN_PASSPHRASE`), supported operations (market data,
  account, trading), symbol format (`BTC-USDT`), and a note that public streaming is supported.
- **`docs/mcp-server.md`** — update the supported-exchanges list / count so agents know `kucoin` is a
  valid exchange key for the existing 12-tool vocabulary (no tool-schema change).
- **`docs/streaming.md`** — add KuCoin to the list of exchanges with public streaming (ticker / trade /
  order book / kline) via `AddKucoinStreams`.
- **`src/CryptoExchanges.Net.Mcp/ToolInputs.cs`** — add `["kucoin"] = ExchangeId.Kucoin` to Exchanges dict (Fix-First: MCP gap from TASK-060).
- **`src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs`** — add `KUCOIN_*` env var bindings (Fix-First: MCP gap from TASK-060).

No additional source/test changes beyond the MCP wiring fix.

## Acceptance Criteria
- [x] README KuCoin row shows ✅ Supported + the `CryptoExchanges.Net.Kucoin` NuGet version badge (mirroring the Bitget row); the supported-exchange count/prose updated to include KuCoin.
- [x] `docs/exchanges.md` has a KuCoin section (credentials `KUCOIN_API_KEY`/`KUCOIN_SECRET_KEY`/`KUCOIN_PASSPHRASE`, operations, `BTC-USDT` symbol format, streaming note); `docs/mcp-server.md` lists `kucoin` as a valid exchange key (no tool-schema change); `docs/streaming.md` lists KuCoin under public streaming.
- [x] All edits are strictly technical (no roadmap/gateway/competitive/monetization leakage); solution still builds 0W/0E.
- [ ] `ToolInputs.cs` maps `kucoin` → `ExchangeId.Kucoin`; `EnvCredentialBinder.cs` reads `KUCOIN_*` env vars.

## Pattern Reference
- README Supported Exchanges table + badges: `README.md` lines 15–25 (Bitget row at line 22 is the exact template; KuCoin "Coming soon" row at line 25).
- Per-exchange docs entry: `docs/exchanges.md` (OKX/Bitget sections).
- MCP exchange-key list: `docs/mcp-server.md`. Streaming exchange list: `docs/streaming.md`.
- Existing KuCoin icon: `docs/assets/exchanges/kucoin.svg`.

## File Scope

**Creates**:
- (none)

**Modifies**:
- README.md
- docs/exchanges.md
- docs/mcp-server.md
- docs/streaming.md
- src/CryptoExchanges.Net.Mcp/ToolInputs.cs
- src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs

## Traceability
- **PRD Acceptance Criteria**: AC-9 (README KuCoin supported badge + MCP reference updated)
- **TRD Component**: §"MCP Wiring" (no tool-schema change; kucoin is a valid exchange key)
- **ADR Reference**: FEAT-006 spec §"Success criteria" (README row supported, MCP docs reflect KuCoin); MEMORY: public-artifacts-no-strategy (no opsec leakage)

## Commits

- `425b66b` — feat(FEAT-006): TASK-064 — docs: KuCoin row → supported + MCP/exchanges/streaming refs

## Implementation Log

- Claimed 2026-06-21; base SHA b6a59569331a0d09eb409ef4daa8c15fd1c2bd27.
- README.md: KuCoin row changed from "🕓 Coming soon | —" to "✅ Supported | CryptoExchanges.Net.Kucoin NuGet badge" (mirror Bitget row exactly). Updated two count-fixed "four exchanges" prose fragments to count-agnostic wording.
- docs/exchanges.md: Added full KuCoin section (credentials, KC-API passphrase-v2 signing, BTC-USDT symbol format, streaming note, DI + AddKucoinStreams snippets). Removed KuCoin from "Coming soon" table. Updated opening prose (four → five) and AddCryptoExchanges/GetClient blocks to include KuCoin credentials/resolver.
- docs/mcp-server.md: Added `kucoin` to introductory exchange list, supported-exchanges line, and credentials table (KUCOIN_API_KEY/KUCOIN_SECRET_KEY/KUCOIN_PASSPHRASE). Updated passphrase note to include KuCoin.
- docs/streaming.md: Updated scope note to name Binance and KuCoin. Added KuCoin DI streaming section (AddKucoinStreams, bullet-public note, full subscribe snippet). Updated without-DI section to mention KuCoin.
- Fix-First Cycle 1 (2026-06-21): code-reviewer REJECT@95% — mcp-server.md claimed kucoin as valid exchange key but ToolInputs.cs + EnvCredentialBinder.cs missing KuCoin wiring (gap from TASK-060). Applied mechanical fix: added ["kucoin"] = ExchangeId.Kucoin to ToolInputs.Exchanges dict; added KUCOIN_API_KEY/SECRET_KEY/PASSPHRASE bindings to EnvCredentialBinder.Apply.

## Review Results

### Cycle 1 (CHANGES_REQUESTED)
- architect-reviewer: APPROVE
- code-reviewer: CHANGES_REQUESTED (REJECT@95% — ToolInputs.cs + EnvCredentialBinder.cs missing kucoin wiring)
- security-reviewer: APPROVE
- api-reviewer: APPROVE
- Fix-First: Applied mechanical MCP source fix; proceeding to Cycle 2.
