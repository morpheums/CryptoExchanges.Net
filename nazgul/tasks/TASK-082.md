---
status: PLANNED
---
# TASK-082: OkxStreamDecoders (DeltaMapper DTO→Core.Models) + decode unit tests

## Metadata
- **ID**: TASK-082
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-080, TASK-081
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Okx/Streaming/OkxStreamDecoders.cs, tests/CryptoExchanges.Net.Okx.Tests.Unit/Streaming/OkxStreamDecodeTests.cs]
- **Wave**: 8
- **Traces to**: PRD AC#2; TRD §"Per-Exchange Variation Points" §3; ADR-009-001; K1
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Implement `OkxStreamDecoders` (`internal static class`) in `src/CryptoExchanges.Net.Okx/Streaming/`,
mirroring `BinanceStreamDecoders.Build(IMapper, ISymbolMapper)`. Returns a `StreamDecoderRegistry`
of opaque `Func<ReadOnlyMemory<byte>, object>` closures, preserving K1 (Http never sees Core.Models
/DeltaMapper).

OKX data envelope is `{"arg":{"channel","instId"},"data":[{...}]}` — `data` is an ARRAY. Each
closure unwraps `data` (take the array element(s)) via a private `DeserializeDataArray<T>` helper
(do NOT decode the raw envelope directly — FEAT-008 lesson), and resolves the canonical symbol from
`arg.instId` (OKX data rows do not always repeat the symbol) via the injected `ISymbolMapper`
(`FromWire` on `BTC-USDT`). Map:
- Ticker via DeltaMapper (`mapper.Map<StreamTickerDto, Ticker>`); set symbol from `arg.instId`.
- Trade hand-map (`side`, `px`, `sz`, `tradeId`, `ts`; buyer-maker from `side`).
- OrderBook hand-map (`bids`/`asks` are `[px, sz, ...]` arrays → `PriceLevel`).
- Kline hand-map: OKX row is a positional ARRAY `[ts,o,h,l,c,vol,...]`; parse by index; symbol from `arg.instId`.

Tests (`OkxStreamDecodeTests.cs`, mirroring `BinanceStreamDecodeTests.cs`, TEST-PLAN §File 2): feed
canned FULL push frames (with the `arg`+`data` array envelope) through each closure; assert Ticker
symbol+last; Trade px/sz/IsBuyerMaker; OrderBook bids+asks+symbol resolved from `arg.instId`; Kline
OHLCV from the positional row all non-zero. Use `FakeSymbolMapper` or a minimal inline OKX
`BTC-USDT` substitute. Inline UTF-8 literals.

### Steps
1. Read `BinanceStreamDecoders.cs` (post-TASK-074 unwrap) + `KucoinStreamDecoders.cs` (`DeserializeData<T>`). Read `StreamDecoderRegistry.cs` (read-only).
2. Implement four closures with the `data`-array unwrap; resolve symbol from `arg.instId`; handle the positional kline row.
3. Write `OkxStreamDecodeTests.cs` with envelope-level canned frames.
4. `dotnet build` 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Acceptance Criteria
- [ ] `OkxStreamDecoders.Build(IMapper, ISymbolMapper)` returns a registry with Ticker/Trade/OrderBook/Kline closures that unwrap the OKX `arg`+`data`-array envelope (no raw-envelope decode), resolve symbol from `arg.instId` via injected `ISymbolMapper`, parse the positional kline row, and return `object`; K1 preserved.
- [ ] `OkxStreamDecodeTests.cs` feeds full push frames through each closure and asserts populated models (Ticker, Trade incl. IsBuyerMaker, OrderBook bids+asks+symbol, Kline OHLCV), mirroring `BinanceStreamDecodeTests.cs`.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- Decoders + unwrap + hand-map: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs`
- KuCoin `DeserializeData<T>`: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs`
- Bybit decoders as immediate intra-objective reference: `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamDecoders.cs`
- Shared registry (read-only): `src/CryptoExchanges.Net.Http/Streaming/StreamDecoderRegistry.cs`
- Decode tests: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDecodeTests.cs`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Okx/Streaming/OkxStreamDecoders.cs
- tests/CryptoExchanges.Net.Okx.Tests.Unit/Streaming/OkxStreamDecodeTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #2 (all four kinds decode for OKX)
- **TRD Component**: OKX variation §3 (OkxStreamDecoders)
- **ADR Reference**: ADR-009-001; binding constraint K1

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
