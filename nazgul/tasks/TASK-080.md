---
status: PLANNED
---
# TASK-080: OKX streaming wire DTOs + OkxStreamOptions

## Metadata
- **ID**: TASK-080
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-079
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Okx/Streaming/OkxStreamOptions.cs, src/CryptoExchanges.Net.Okx/Dtos/Streaming/StreamTickerDto.cs, src/CryptoExchanges.Net.Okx/Dtos/Streaming/StreamTradeDto.cs, src/CryptoExchanges.Net.Okx/Dtos/Streaming/StreamDepthDto.cs, src/CryptoExchanges.Net.Okx/Dtos/Streaming/StreamKlineDto.cs]
- **Wave**: 6
- **Traces to**: PRD AC#2; TRD §"Per-Exchange Variation Points" §1+§4 + §"OKX Public WebSocket"; ADR-009-001
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

GATED ON BYBIT MERGE: OKX work begins only after the Bybit PR (TASK-079) merges to `main` per
ADR-009-005. The OKX branch is cut from updated `main`.

Create the OKX streaming option class and the four internal wire DTOs the OKX decoders
(TASK-082) deserialize OKX public-WS data frames into. Foundation task for the OKX group.

`OkxStreamOptions` — public `sealed class`, single `StreamBaseUrl` property defaulting to
`wss://ws.okx.com:8443/ws/v5/public` (TRD §"OKX Public WebSocket"). No credentials. Full XML docs.

Four `internal` `{Concept}Dto` records, one per file, in `Dtos/Streaming/`, canonical names,
OKX vendor field names ONLY in `[JsonPropertyName]`. OKX data frames carry `data` as an ARRAY
of objects under `{"arg":{"channel","instId"},"data":[{...}]}`:
- `StreamTickerDto` → `Ticker` (`tickers` channel: `instId`, `last`, `high24h`, `low24h`, `vol24h`, `bidPx`, `askPx`, `ts`)
- `StreamTradeDto` → `Trade` (`trades` channel: `instId`, `tradeId`, `px`, `sz`, `side`, `ts`)
- `StreamDepthDto` → `OrderBook` (`books5`/`books` channel: `instId`, `bids` `[[px,sz,...],...]`, `asks`, `ts`, `seqId`)
- `StreamKlineDto` → `Candlestick` (`candle1m` etc.: row is an ARRAY `[ts,o,h,l,c,vol,...]` — model as the existing array-kline pattern; symbol comes from `arg.instId`, not the row)

Symbol wire format is dash-separated `BTC-USDT` (reuse `OkxSymbolFormat` downstream). Follow the
Binance/KuCoin DTO record style, nullability, and string-decimal conventions.

### Steps
1. Confirm Bybit PR merged + branch from updated `main`.
2. Read `src/CryptoExchanges.Net.Binance/Dtos/Streaming/` + `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/` for record style.
3. Create `OkxStreamOptions.cs` + the four DTO files; OKX field names only in `[JsonPropertyName]`; note OKX kline rows are positional arrays.
4. `dotnet build CryptoExchanges.Net.sln` → 0W/0E.

## Acceptance Criteria
- [ ] `OkxStreamOptions` public sealed with `StreamBaseUrl` defaulting to `wss://ws.okx.com:8443/ws/v5/public`, full XML docs.
- [ ] Four internal `{Concept}Dto` records under `Dtos/Streaming/` (Ticker/Trade/Depth/Kline), one type per file, OKX field names only in `[JsonPropertyName]`, matching the Binance DTO style; kline modeled for OKX's positional-array row.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0 Warning / 0 Error.

## Pattern Reference
- DTO style: `src/CryptoExchanges.Net.Binance/Dtos/Streaming/` (all files), `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/`
- Options class: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamOptions.cs`
- Completed Bybit DTOs as the immediate intra-objective reference: `src/CryptoExchanges.Net.Bybit/Dtos/Streaming/`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Okx/Streaming/OkxStreamOptions.cs
- src/CryptoExchanges.Net.Okx/Dtos/Streaming/StreamTickerDto.cs
- src/CryptoExchanges.Net.Okx/Dtos/Streaming/StreamTradeDto.cs
- src/CryptoExchanges.Net.Okx/Dtos/Streaming/StreamDepthDto.cs
- src/CryptoExchanges.Net.Okx/Dtos/Streaming/StreamKlineDto.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #2 (OKX all four stream kinds)
- **TRD Component**: OKX variation §1 (OkxStreamOptions) + §4 (Dtos/Streaming/)
- **ADR Reference**: ADR-009-001, ADR-009-002, ADR-009-005 (gated on Bybit merge)

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
