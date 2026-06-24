---
status: PLANNED
---
# TASK-087: BitgetStreamDecoders (DeltaMapper DTO→Core.Models) + decode unit tests

## Metadata
- **ID**: TASK-087
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-085, TASK-086
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Bitget/Streaming/BitgetStreamDecoders.cs, tests/CryptoExchanges.Net.Bitget.Tests.Unit/Streaming/BitgetStreamDecodeTests.cs]
- **Wave**: 13
- **Traces to**: PRD AC#2; TRD §"Per-Exchange Variation Points" §3; ADR-009-001; K1
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Implement `BitgetStreamDecoders` (`internal static class`) in `src/CryptoExchanges.Net.Bitget/Streaming/`,
mirroring `BinanceStreamDecoders.Build(IMapper, ISymbolMapper)`. Returns a `StreamDecoderRegistry`
of opaque `Func<ReadOnlyMemory<byte>, object>` closures, preserving K1.

Bitget data envelope is `{"action":"snapshot"/"update","arg":{"instType","channel","instId"},"data":[...]}`
— `data` is an ARRAY. Each closure unwraps `data` via a private `DeserializeDataArray<T>` helper (no raw-
envelope decode — FEAT-008 lesson), and resolves the canonical symbol from `arg.instId` via the
injected `ISymbolMapper` (`FromWire` on `BTCUSDT`). Both `action:snapshot` and `update` decode to a
flat model (no local-book maintenance). Map:
- Ticker via DeltaMapper (`mapper.Map<StreamTickerDto, Ticker>`); symbol from `arg.instId`.
- Trade hand-map (`price`, `size`, `side`, `tradeId`, `ts`; buyer-maker from `side`).
- OrderBook hand-map (`bids`/`asks` `[px,sz]` arrays → `PriceLevel`; symbol from `arg.instId`).
- Kline hand-map: Bitget row is a positional ARRAY `[ts,o,h,l,c,baseVol,...]`; parse by index; symbol from `arg.instId`.

Tests (`BitgetStreamDecodeTests.cs`, mirroring `BinanceStreamDecodeTests.cs`, TEST-PLAN §File 2):
feed canned FULL push frames (with the `action`+`arg`+`data` array envelope) through each closure;
assert Ticker symbol+last; Trade price/size/IsBuyerMaker; OrderBook bids+asks+symbol from `arg.instId`;
Kline OHLCV from the positional row all non-zero. Use `FakeSymbolMapper` or a minimal inline Bitget
`BTCUSDT` substitute. Inline UTF-8 literals.

### Steps
1. Read `BinanceStreamDecoders.cs` (post-TASK-074 unwrap) + the just-built `OkxStreamDecoders.cs` (`data`-array + positional kline + symbol-from-arg). Read `StreamDecoderRegistry.cs` (read-only).
2. Implement four closures with the `data`-array unwrap; resolve symbol from `arg.instId`; handle the positional kline row + `action` envelope.
3. Write `BitgetStreamDecodeTests.cs` with envelope-level canned frames.
4. `dotnet build` 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Acceptance Criteria
- [ ] `BitgetStreamDecoders.Build(IMapper, ISymbolMapper)` returns a registry with Ticker/Trade/OrderBook/Kline closures that unwrap the Bitget `action`+`arg`+`data`-array envelope (no raw-envelope decode), resolve symbol from `arg.instId` via injected `ISymbolMapper`, parse the positional kline row, and return `object`; K1 preserved.
- [ ] `BitgetStreamDecodeTests.cs` feeds full push frames through each closure and asserts populated models (Ticker, Trade incl. IsBuyerMaker, OrderBook bids+asks+symbol, Kline OHLCV), mirroring `BinanceStreamDecodeTests.cs`.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- Decoders + unwrap + hand-map: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs`
- Data-array + positional kline + symbol-from-arg (immediate intra-objective): `src/CryptoExchanges.Net.Okx/Streaming/OkxStreamDecoders.cs`
- Shared registry (read-only): `src/CryptoExchanges.Net.Http/Streaming/StreamDecoderRegistry.cs`
- Decode tests: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDecodeTests.cs`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Bitget/Streaming/BitgetStreamDecoders.cs
- tests/CryptoExchanges.Net.Bitget.Tests.Unit/Streaming/BitgetStreamDecodeTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #2 (all four kinds decode for Bitget)
- **TRD Component**: Bitget variation §3 (BitgetStreamDecoders)
- **ADR Reference**: ADR-009-001; binding constraint K1

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
