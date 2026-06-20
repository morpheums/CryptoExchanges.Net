---
id: TASK-064
status: PLANNED
depends_on: [TASK-060, TASK-062]
---
# TASK-064: Docs — README KuCoin row → supported + MCP/exchanges reference

## Metadata
- **ID**: TASK-064
- **Group**: 7
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-060, TASK-062
- **Delegates to**: none
- **Files modified**: [README.md, docs/exchanges.md, docs/mcp-server.md, docs/streaming.md]
- **Wave**: 7
- **Traces to**: PRD-FEAT-006 AC-9; TRD-FEAT-006 §"MCP Wiring"; FEAT-006 spec §"DI + MCP", §"Build approach" step 9, §"Success criteria"
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**:
- **Base SHA**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
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

No source/test changes. Verify the README badge URL matches the actual package id.

## Acceptance Criteria
- [ ] README KuCoin row shows ✅ Supported + the `CryptoExchanges.Net.Kucoin` NuGet version badge (mirroring the Bitget row); the supported-exchange count/prose updated to include KuCoin.
- [ ] `docs/exchanges.md` has a KuCoin section (credentials `KUCOIN_API_KEY`/`KUCOIN_SECRET_KEY`/`KUCOIN_PASSPHRASE`, operations, `BTC-USDT` symbol format, streaming note); `docs/mcp-server.md` lists `kucoin` as a valid exchange key (no tool-schema change); `docs/streaming.md` lists KuCoin under public streaming.
- [ ] All edits are strictly technical (no roadmap/gateway/competitive/monetization leakage); no source or test files changed; solution still builds 0W/0E.

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

## Traceability
- **PRD Acceptance Criteria**: AC-9 (README KuCoin supported badge + MCP reference updated)
- **TRD Component**: §"MCP Wiring" (no tool-schema change; kucoin is a valid exchange key)
- **ADR Reference**: FEAT-006 spec §"Success criteria" (README row supported, MCP docs reflect KuCoin); MEMORY: public-artifacts-no-strategy (no opsec leakage)

## Commits

<!-- implementer fills SHAs -->

## Implementation Log

## Review Results
