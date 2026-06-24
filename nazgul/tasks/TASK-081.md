---
status: PLANNED
---
# TASK-081: OkxStreamProtocol (IStreamProtocol, client text-ping + text-pong Classify) + protocol unit tests

## Metadata
- **ID**: TASK-081
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-080
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Okx/Streaming/OkxStreamProtocol.cs, tests/CryptoExchanges.Net.Okx.Tests.Unit/Streaming/OkxStreamProtocolTests.cs]
- **Wave**: 7
- **Traces to**: PRD AC#2/#3; TRD §"Per-Exchange Variation Points" §2 + §"OKX Public WebSocket"; ADR-009-003 (heartbeat), ADR-009-004 (pacing), ADR-009-006 (routing key)
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Implement `OkxStreamProtocol : IStreamProtocol` (`internal sealed`) in
`src/CryptoExchanges.Net.Okx/Streaming/`, cloning the Binance/KuCoin protocol structure. OKX uses
a STATIC URL (ADR-009-002): cache `StreamConnectionInfo` in the constructor, return via
`ValueTask.FromResult`.

Wire formats (TRD §"OKX Public WebSocket"):
- Subscribe: `{"op":"subscribe","args":[{"channel":"tickers","instId":"BTC-USDT"}]}`
- Unsubscribe: `{"op":"unsubscribe","args":[{"channel":"tickers","instId":"BTC-USDT"}]}`
- Batch: multiple arg OBJECTS in one frame (`BuildSubscribeBatch`/`BuildUnsubscribeBatch`; engine pre-chunks to 100).
- Channel → kind: `tickers`=Ticker, `trades`=Trade, `books5`/`books`=OrderBook, `candle1m`…=Kline.
- Routing key (ADR-009-006) = `<channel>:<instId>`, e.g. `tickers:BTC-USDT`. Single-source it in one private `BuildRoutingKey(request)` helper used by BOTH `RoutingKeyFor` and `Classify`. For data frames, reconstruct the key from `arg.channel + ":" + arg.instId`.
- Symbol wire format: dash `BTC-USDT` via existing `OkxSymbolFormat`.
- Kline channel names: `candle1m candle3m candle5m candle15m candle30m candle1H candle2H candle4H candle6H candle12H candle1D candle1W candle1M` mapped from `KlineInterval`.

`Classify` (TRD "Classify note for OKX" — critical):
- Data frame `{"arg":{...},"data":[...]}` → `FrameKind.Data` + composite routing key.
- Ack frame `{"event":"subscribe","arg":{...}}` → `FrameKind.Ack` (null key).
- Error frame `{"event":"error",...}` → `FrameKind.Error`.
- **Bare-text `"pong"` frame** (server reply to the client text-ping) is NOT JSON — `Classify` must check for the literal `pong` bytes BEFORE attempting JSON parse and return `FrameKind.Pong`.
- Unrecognised/empty → `FrameKind.Error`.

Heartbeat (ADR-009-003): `HeartbeatDirection.ClientPing`, `PingFormat.Text`, ping payload `"ping"`,
`Interval=25s`, `Timeout=35s` — the engine sends the text ping; server replies bare-text `"pong"`
(classified as Pong above). `MinOutboundInterval = 100 ms` (ADR-009-004).

**Implementor MUST confirm from current OKX v5 WS docs (TRD flag):** whether `books5` (top-5) or
`books` (full) is the preferred order-book channel + the chosen default used in `BuildSubscribe`;
exact ping/pong text and interval. Record the confirmed values in the class summary + test assertions.

Tests (`OkxStreamProtocolTests.cs`, mirroring `BinanceStreamProtocolTests.cs`, TEST-PLAN §File 1)
cover the full matrix PLUS `Classify_TextPongFrame_ReturnsPong` (bare `"pong"` bytes → `FrameKind.Pong`)
and a composite-routing-key `RoutingKeyFor_MatchesClassify_DataFrame` assertion. Inline UTF-8 literals.

### Steps
1. Read `BinanceStreamProtocol.cs`, `KucoinStreamProtocol.cs` (client-ping reference), and the shared `IStreamProtocol.cs`/`StreamConnectionInfo.cs`/`HeartbeatPolicy` (read-only).
2. Confirm OKX order-book channel + ping/pong specifics; record values.
3. Implement `OkxStreamProtocol` with the `BuildRoutingKey` helper feeding both methods; implement the bare-`pong`-before-JSON branch in `Classify`; set ClientPing/Text heartbeat + 100ms pacing.
4. Write `OkxStreamProtocolTests.cs` incl. the text-pong case.
5. `dotnet build` 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Acceptance Criteria
- [ ] `OkxStreamProtocol : IStreamProtocol` (internal sealed) implements all members + batch builders; composite routing key (`channel:instId`) single-sourced in one helper shared by `RoutingKeyFor` and `Classify`; `Classify` returns `FrameKind.Pong` for the bare-text `"pong"` frame before any JSON parse; heartbeat `ClientPing`/`PingFormat.Text` + `MinOutboundInterval=100ms` (confirmed OKX values recorded in the class summary).
- [ ] `OkxStreamProtocolTests.cs` covers the full TEST-PLAN §File-1 matrix PLUS `Classify_TextPongFrame_ReturnsPong` and composite `RoutingKeyFor_MatchesClassify_DataFrame`, mirroring `BinanceStreamProtocolTests.cs`, inline UTF-8 literals.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- Static-URL protocol + RoutingKey-shared-by-both: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs`
- Client-ping reference: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs`
- Bybit protocol as immediate intra-objective reference: `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamProtocol.cs`
- Shared seam (read-only): `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs`, `StreamConnectionInfo.cs`
- Protocol tests: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamProtocolTests.cs`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Okx/Streaming/OkxStreamProtocol.cs
- tests/CryptoExchanges.Net.Okx.Tests.Unit/Streaming/OkxStreamProtocolTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #2, #3
- **TRD Component**: OKX variation §2 (OkxStreamProtocol) + "Classify note for OKX"
- **ADR Reference**: ADR-009-002, ADR-009-003 (ClientPing/Text), ADR-009-004, ADR-009-006

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
