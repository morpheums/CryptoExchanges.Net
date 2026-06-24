---
status: IN_PROGRESS
---
# TASK-076: BybitStreamProtocol (IStreamProtocol) + protocol unit tests

## Metadata
- **ID**: TASK-076
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-075
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamProtocol.cs, tests/CryptoExchanges.Net.Bybit.Tests.Unit/Streaming/BybitStreamProtocolTests.cs]
- **Wave**: 2
- **Traces to**: PRD AC#1/#2/#3; TRD §"Per-Exchange Variation Points" §2 + §"Bybit v5 Public Spot"; ADR-009-003 (heartbeat), ADR-009-004 (pacing), ADR-009-006 (routing key)
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**: 2026-06-24T08:00:00Z
- **Base SHA**: 7746044a5f51406874fc3b6195b3df497351bd6f
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Implement `BybitStreamProtocol : IStreamProtocol` as an `internal sealed class` in
`src/CryptoExchanges.Net.Bybit/Streaming/`, cloning the Binance protocol structure. Bybit v5
public spot uses a STATIC URL (no token negotiation, ADR-009-002), so
`ResolveConnectionAsync` builds `StreamConnectionInfo` once in the constructor and returns it
via `ValueTask.FromResult`.

Wire formats (TRD §"Bybit v5 Public Spot"):
- Subscribe: `{"req_id":"<uuid>","op":"subscribe","args":["publicTrade.BTCUSDT"]}`
- Unsubscribe: `{"req_id":"<uuid>","op":"unsubscribe","args":[...]}`
- Batch: multiple args in one frame (implement `BuildSubscribeBatch`/`BuildUnsubscribeBatch`; engine pre-chunks to `MaxBatchSize=100`).
- Topic → kind: `tickers.<SYM>`=Ticker, `publicTrade.<SYM>`=Trade, `orderbook.<DEPTH>.<SYM>`=OrderBook, `kline.<INTERVAL>.<SYM>`=Kline.
- Routing key = the `topic` field verbatim (ADR-009-006), e.g. `tickers.BTCUSDT`. Single-source it in one private `BuildTopic(request)` helper used by BOTH `RoutingKeyFor` and `Classify`, exactly as Binance's `BuildStreamToken` is used by both.
- `Classify`: data frame `{"topic":...,"type":"snapshot"/"delta","data":{...}}` → `FrameKind.Data` + routing key from `topic`; ack `{"success":true,"op":"subscribe",...}` → `FrameKind.Ack` (null key); `{"success":false,...}` or `ret_msg` error → `FrameKind.Error`; unrecognised/empty → `FrameKind.Error`. No I/O, minimal allocation (mirror Binance `Classify`).
- Symbol wire format: reuse the existing `BybitSymbolFormat` for canonical→wire (`BTCUSDT`, no separator).
- Kline interval token: map `KlineInterval` → Bybit codes `1 3 5 15 30 60 120 240 360 720 D W M`.

HeartbeatPolicy + MinOutboundInterval set inside `ResolveConnectionAsync`/`StreamConnectionInfo`:
- Heartbeat: `HeartbeatDirection.ServerPingClientPong`, `Interval=20s`, `Timeout=60s` (ADR-009-003 TBC).
- `MinOutboundInterval = TimeSpan.FromMilliseconds(100)` (ADR-009-004 TBC).

**Implementor MUST confirm from current Bybit v5 WebSocket docs before finalizing (TRD flag):**
heartbeat direction & ping interval; available order-book depth levels (1/50/200) and the default depth used in `BuildSubscribe`/the order-book topic; that `type:snapshot` vs `delta` does not change `Classify` routing (both classify as `Data` on the same `topic` key). Record the confirmed values in the protocol class summary and in the test assertions.

Tests (`BybitStreamProtocolTests.cs` under `tests/CryptoExchanges.Net.Bybit.Tests.Unit/Streaming/`, mirroring `BinanceStreamProtocolTests.cs`) must cover the full matrix from TEST-PLAN §File 1: Classify data/ack/error/unrecognised/empty; BuildSubscribe ticker/trade/orderbook/kline produce the expected topic (RoutingKeyFor output appears in the frame); BuildUnsubscribe; BuildSubscribeBatch two-requests→one frame with both topics; `RoutingKeyFor_MatchesClassify_DataFrame`; `ResolveConnectionAsync` endpoint = configured base URL; heartbeat direction matches ADR-009-003; MinOutboundInterval = 100 ms. Inline UTF-8 byte literals only (no external JSON).

### Steps
1. Read `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs` fully + the shared `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs`, `StreamConnectionInfo.cs`, `StreamFrame`/`FrameKind`, `HeartbeatPolicy`. Do NOT modify any Http file.
2. Confirm the Bybit v5 WS specifics flagged above from current docs; record values.
3. Implement `BybitStreamProtocol` with the single `BuildTopic` helper feeding `RoutingKeyFor` + `Classify`; implement all six members + batch builders.
4. Write `BybitStreamProtocolTests.cs` mirroring the Binance test matrix.
5. `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Acceptance Criteria
- [ ] `BybitStreamProtocol : IStreamProtocol` (internal sealed) implements all members + batch builders; routing key single-sourced in one helper shared by `RoutingKeyFor` and `Classify`; static endpoint via cached `StreamConnectionInfo`; heartbeat + `MinOutboundInterval=100ms` set per ADR-009-003/004 (confirmed values recorded in the class summary).
- [ ] `BybitStreamProtocolTests.cs` covers the full TEST-PLAN §File-1 matrix (classify data/ack/error/unrecognised/empty; subscribe/unsubscribe/batch topic correctness; `RoutingKeyFor_MatchesClassify_DataFrame`; endpoint + heartbeat + pacing assertions), mirroring `BinanceStreamProtocolTests.cs`, with inline UTF-8 frame literals.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- Protocol: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs` (static URL, server-ping, batched subscribe, `BuildStreamToken` shared by RoutingKeyFor+Classify)
- KuCoin variant for batch-by-join + client-ping contrast: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs`
- Shared seam (read-only): `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs`, `StreamConnectionInfo.cs`
- Protocol tests: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamProtocolTests.cs`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamProtocol.cs
- tests/CryptoExchanges.Net.Bybit.Tests.Unit/Streaming/BybitStreamProtocolTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #1, #2, #3 (subscribe/route correctness underpins all stream kinds + multi-symbol)
- **TRD Component**: Bybit variation §2 (BybitStreamProtocol)
- **ADR Reference**: ADR-009-002 (static endpoint), ADR-009-003 (heartbeat), ADR-009-004 (100ms pacing), ADR-009-006 (routing key)

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
