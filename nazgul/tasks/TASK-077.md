---
status: IN_PROGRESS
---
# TASK-077: BybitStreamDecoders (DeltaMapper DTO→Core.Models) + decode unit tests

## Metadata
- **ID**: TASK-077
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-075, TASK-076
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamDecoders.cs, tests/CryptoExchanges.Net.Bybit.Tests.Unit/Streaming/BybitStreamDecodeTests.cs]
- **Wave**: 3
- **Traces to**: PRD AC#1/#2; TRD §"Per-Exchange Variation Points" §3 (XxxStreamDecoders); ADR-009-001; K1
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**: 2026-06-24T08:00:00Z
- **Base SHA**: a61508b2d60b33f561e750162c91237fbef2dcc6
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Implement `BybitStreamDecoders` as an `internal static class` in
`src/CryptoExchanges.Net.Bybit/Streaming/`, mirroring `BinanceStreamDecoders.Build(IMapper, ISymbolMapper)`
exactly. It returns a `StreamDecoderRegistry` of opaque `Func<ReadOnlyMemory<byte>, object>`
closures — one per stream kind — that the engine invokes on each data frame. This keeps K1
intact: the Http engine never sees `Core.Models` or DeltaMapper; the closures live in the Bybit
package and return `object`.

Each closure:
1. Unwraps the Bybit v5 data envelope `{"topic":...,"type":...,"data":{...}|[...]}` — deserialize the `data` element (object for ticker/orderbook/kline; ARRAY for publicTrade — emit per-row or first-row per the existing Binance/KuCoin trade pattern) into the internal `{Concept}Dto` from TASK-075. Use a private `DeserializeData<T>`/`DeserializeDataArray<T>` helper exactly like KuCoin's `DeserializeData<T>` and Binance's TASK-074 unwrap (do NOT deserialize the raw envelope directly — that was the FEAT-008 bug).
2. Maps DTO→Core.Models via DeltaMapper (`mapper.Map<TDto,TModel>`) for Ticker; hand-maps Trade/OrderBook/Kline following the Binance decoder pattern (order-book bid/ask `[price,qty]` string pairs → `PriceLevel`; kline OHLCV string→decimal; symbol resolved via the injected `ISymbolMapper` from the wire symbol in `data.s`/topic).
3. Returns the `Core.Models` object as `object`.

Resolve the canonical symbol from the Bybit wire symbol (`data.s` or the topic's `<SYM>` segment) via the injected `ISymbolMapper` (`FromWire`), matching how Binance resolves it.

Tests (`BybitStreamDecodeTests.cs`, mirroring `BinanceStreamDecodeTests.cs`, TEST-PLAN §File 2): feed a canned FULL push-frame envelope (as it arrives from the socket, including the `topic`/`data` wrapper) through each closure from `BybitStreamDecoders.Build(...)` and assert the Core.Models output — Ticker symbol+lastPrice; Trade price/qty/IsBuyerMaker (`S=="Sell"`); OrderBook bids non-empty + asks non-empty + symbol resolved; Kline OHLCV all non-zero. Use the `FakeSymbolMapper` in `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/` or a minimal inline substitute for Bybit's `BTCUSDT` wire form. Inline UTF-8 literals only.

### Steps
1. Read `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs` (esp. the post-TASK-074 `data`-envelope unwrap) and `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs` (`DeserializeData<T>`/`DeserializeSnapshotData<T>`). Read the shared `StreamDecoderRegistry.cs` (read-only).
2. Implement the four closures with a private unwrap helper; route trade through the array path.
3. Resolve symbol via injected `ISymbolMapper`.
4. Write `BybitStreamDecodeTests.cs` with envelope-level canned frames per TEST-PLAN §File 2.
5. `dotnet build` 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Acceptance Criteria
- [ ] `BybitStreamDecoders.Build(IMapper, ISymbolMapper)` returns a `StreamDecoderRegistry` with Ticker/Trade/OrderBook/Kline closures that unwrap the Bybit `data` envelope before deserializing (no raw-envelope decode), map to Core.Models, and return `object`; symbol resolved via the injected `ISymbolMapper`. No `Core.Models`/DeltaMapper reference leaks into Http (K1 preserved — closures stay in the Bybit package).
- [ ] `BybitStreamDecodeTests.cs` feeds full push-frame envelopes through each closure and asserts populated models (Ticker, Trade incl. IsBuyerMaker, OrderBook bids+asks+symbol, Kline OHLCV), mirroring `BinanceStreamDecodeTests.cs`.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- Decoders + `data`-envelope unwrap + hand-map: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs`
- KuCoin `DeserializeData<T>`/`DeserializeSnapshotData<T>`: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs`
- Shared registry (read-only): `src/CryptoExchanges.Net.Http/Streaming/StreamDecoderRegistry.cs`
- Decode tests + FakeSymbolMapper: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDecodeTests.cs`, `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamDecoders.cs
- tests/CryptoExchanges.Net.Bybit.Tests.Unit/Streaming/BybitStreamDecodeTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #1 (live Ticker), #2 (all four kinds decode)
- **TRD Component**: Bybit variation §3 (BybitStreamDecoders)
- **ADR Reference**: ADR-009-001; binding constraint K1

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
