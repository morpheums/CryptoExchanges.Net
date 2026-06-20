# PRD — FEAT-006: KuCoin Exchange Integration

- **Status**: Approved for implementation
- **Date**: 2026-06-20
- **Feature ID**: FEAT-006
- **Type**: Brownfield feature — 5th exchange, clones the verified exchange template

## Problem Statement

CryptoExchanges.Net supports four exchanges (Binance, Bybit, OKX, Bitget). KuCoin is a major global
spot exchange not yet covered. Consumers who target KuCoin currently have no canonical `Core.Models`
path and no streaming capability through this library.

A secondary problem: the shared streaming engine's `IStreamProtocol.Endpoint` is a static `Uri`.
This assumption breaks for KuCoin, whose WebSocket connection requires a server-negotiated token
obtained via a REST call before each connect. The seam must be generalized without regressing Binance.

## Goals

1. KuCoin REST parity with the four existing exchanges: market data, account, trading via canonical
   `Core.Models`.
2. KuCoin KC-API passphrase-v2 signing (base64 HMAC-SHA256; passphrase itself HMAC-signed).
3. Public WebSocket streaming: ticker, trade, order book, kline — with auto-reconnect +
   token re-negotiation + auto-resubscribe.
4. `AddKucoinExchange` DI registration; MCP tool wiring so agents need no changes when switching to it.
5. README supported-exchanges row updated; build stays 0W/0E.

## Out of Scope

- Futures, margin, or any non-spot instrument.
- Authenticated (private) WebSocket streams (account/order updates).
- Local order-book maintenance — raw frames only.
- Any KuCoin feature not covered by the existing `IExchangeClient`/`IMarketDataService`/
  `ITradingService`/`IAccountService` surface.

## User Stories

**As a consumer using `AddKucoinExchange`**, I can resolve `IExchangeClient` for KuCoin and call
`MarketData`, `Account`, and `Trading` methods that return the same `Core.Models` types I use
for every other exchange — with no exchange-specific code in my application.

**As a consumer using `AddKucoinStreams`**, I can subscribe to live ticker/trade/order-book/kline
feeds for any KuCoin spot pair. When the connection drops, the library reconnects, re-negotiates
the bullet-public token, and resubscribes automatically — my callback receives no disconnect events.

**As a consumer of the MCP server**, I can point any MCP-aware agent at KuCoin by setting the
exchange key to `kucoin`; the 12-tool vocabulary works unchanged.

## Acceptance Criteria

| # | Criterion |
|---|-----------|
| AC-1 | `AddKucoinExchange` resolves a working `IExchangeClient`; all REST methods return canonical `Core.Models` against live KuCoin endpoints. |
| AC-2 | KC-API passphrase-v2 signing authenticates private endpoints; per-attempt re-sign; retry is GET-only. |
| AC-3 | Ticker, trade, order-book, and kline public streams deliver `Core.Models` via the streaming client. |
| AC-4 | Forced disconnect triggers reconnect: engine re-negotiates `bullet-public` token, reconnects, resubscribes; consumer callback resumes without intervention. |
| AC-5 | Binance streaming regression-free after the `IStreamProtocol` seam generalization. |
| AC-6 | Build: 0 warnings, 0 errors under `TreatWarningsAsErrors`; full XML docs on all public and `internal` interfaces. |
| AC-7 | Non-integration test suite fully green (`dotnet test --filter 'Category!=Integration'`); new unit tests use fake transport / stub HTTP handlers — no network calls. |
| AC-8 | Live integration smokes (REST + one streaming) self-skip when `KUCOIN_API_KEY` / `KUCOIN_SECRET_KEY` / `KUCOIN_PASSPHRASE` env vars are absent. |
| AC-9 | README KuCoin row shows supported badge; MCP reference updated. |

## Constraints

- 4-layer dependency chain (Core → Http → Exchange → DI) must be preserved.
- No `Core.Models` or DeltaMapper under `src/CryptoExchanges.Net.Http/` (K1).
- Streaming engine constraints C1/K2/K3 from FEAT-005 carry forward unchanged.
- `ExchangeCredentials.Passphrase` is already supported in Core; KuCoin reuses it.
- No opsec leakage: README/commits/PRs/MCP metadata stay strictly technical.
- Spot only; no order-book maintenance.
