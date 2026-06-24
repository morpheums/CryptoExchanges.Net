---
status: PLANNED
---
# TASK-085: Bitget streaming wire DTOs + BitgetStreamOptions

## Metadata
- **ID**: TASK-085
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-084
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Bitget/Streaming/BitgetStreamOptions.cs, src/CryptoExchanges.Net.Bitget/Dtos/Streaming/StreamTickerDto.cs, src/CryptoExchanges.Net.Bitget/Dtos/Streaming/StreamTradeDto.cs, src/CryptoExchanges.Net.Bitget/Dtos/Streaming/StreamDepthDto.cs, src/CryptoExchanges.Net.Bitget/Dtos/Streaming/StreamKlineDto.cs]
- **Wave**: 11
- **Traces to**: PRD AC#2; TRD §"Per-Exchange Variation Points" §1+§4 + §"Bitget Public WebSocket"; ADR-009-001
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

GATED ON OKX MERGE: Bitget work begins only after the OKX PR (TASK-084) merges to `main` per
ADR-009-005. The Bitget branch is cut from updated `main`.

Create the Bitget streaming option class and the four internal wire DTOs the Bitget decoders
(TASK-087) deserialize Bitget v2 public-WS data frames into. Foundation task for the Bitget group.

`BitgetStreamOptions` — public `sealed class`, single `StreamBaseUrl` property defaulting to
`wss://ws.bitget.com/v2/ws/public` (TRD §"Bitget Public WebSocket"). No credentials. Full XML docs.

Four `internal` `{Concept}Dto` records, one per file, in `Dtos/Streaming/`, canonical names, Bitget
field names ONLY in `[JsonPropertyName]`. Bitget data frames: `{"action":"snapshot"/"update","arg":{"instType","channel","instId"},"data":[...]}` (`data` is an ARRAY):
- `StreamTickerDto` → `Ticker` (`ticker` channel: `instId`, `lastPr`, `high24h`, `low24h`, `baseVolume`, `bidPr`, `askPr`, `ts`)
- `StreamTradeDto` → `Trade` (`trade` channel: `ts`, `price`, `size`, `side`, `tradeId`)
- `StreamDepthDto` → `OrderBook` (`books5`/`books15` channel: `bids` `[[px,sz],...]`, `asks`, `ts`, `seq`; symbol from `arg.instId`)
- `StreamKlineDto` → `Candlestick` (`candle1m` etc.: row is a positional ARRAY `[ts,o,h,l,c,baseVol,...]`; symbol from `arg.instId`)

Symbol wire format is `BTCUSDT` (no separator; reuse `BitgetSymbolFormat` downstream). Follow the
Binance/KuCoin DTO record style, nullability, and string-decimal conventions.

### Steps
1. Confirm OKX PR merged + branch from updated `main`.
2. Read `src/CryptoExchanges.Net.Binance/Dtos/Streaming/` + `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/` for record style.
3. Create `BitgetStreamOptions.cs` + the four DTO files; Bitget field names only in `[JsonPropertyName]`; note positional-array kline row + `data` array + `action` envelope.
4. `dotnet build CryptoExchanges.Net.sln` → 0W/0E.

## Acceptance Criteria
- [ ] `BitgetStreamOptions` public sealed with `StreamBaseUrl` defaulting to `wss://ws.bitget.com/v2/ws/public`, full XML docs.
- [ ] Four internal `{Concept}Dto` records under `Dtos/Streaming/` (Ticker/Trade/Depth/Kline), one type per file, Bitget field names only in `[JsonPropertyName]`, matching the Binance DTO style; kline modeled for Bitget's positional-array row.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0 Warning / 0 Error.

## Pattern Reference
- DTO style: `src/CryptoExchanges.Net.Binance/Dtos/Streaming/` (all files), `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/`
- Options class: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamOptions.cs`
- OKX DTOs as immediate intra-objective reference (also positional-array kline + data-array): `src/CryptoExchanges.Net.Okx/Dtos/Streaming/`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Bitget/Streaming/BitgetStreamOptions.cs
- src/CryptoExchanges.Net.Bitget/Dtos/Streaming/StreamTickerDto.cs
- src/CryptoExchanges.Net.Bitget/Dtos/Streaming/StreamTradeDto.cs
- src/CryptoExchanges.Net.Bitget/Dtos/Streaming/StreamDepthDto.cs
- src/CryptoExchanges.Net.Bitget/Dtos/Streaming/StreamKlineDto.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #2 (Bitget all four stream kinds)
- **TRD Component**: Bitget variation §1 (BitgetStreamOptions) + §4 (Dtos/Streaming/)
- **ADR Reference**: ADR-009-001, ADR-009-002, ADR-009-005 (gated on OKX merge)

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
