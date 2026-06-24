---
status: PLANNED
---
# TASK-086: BitgetStreamProtocol (IStreamProtocol) + protocol unit tests

## Metadata
- **ID**: TASK-086
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — that is canonical, read by scripts/lib/structured-state.sh; not duplicated here to avoid drift)
- **Depends on**: TASK-085
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Bitget/Streaming/BitgetStreamProtocol.cs, tests/CryptoExchanges.Net.Bitget.Tests.Unit/Streaming/BitgetStreamProtocolTests.cs]
- **Wave**: 12
- **Traces to**: PRD AC#2/#3; TRD §"Per-Exchange Variation Points" §2 + §"Bitget Public WebSocket"; ADR-009-003, ADR-009-004, ADR-009-006
- **Created at**: 2026-06-24T07:40:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Description

Implement `BitgetStreamProtocol : IStreamProtocol` (`internal sealed`) in
`src/CryptoExchanges.Net.Bitget/Streaming/`, cloning the Binance/Bybit/OKX protocol structure.
Bitget v2 public uses a STATIC URL (ADR-009-002): cache `StreamConnectionInfo` in the constructor,
return via `ValueTask.FromResult`.

Wire formats (TRD §"Bitget Public WebSocket"):
- Subscribe: `{"op":"subscribe","args":[{"instType":"SPOT","channel":"ticker","instId":"BTCUSDT"}]}`
- Unsubscribe: `{"op":"unsubscribe","args":[{"instType":"SPOT","channel":"ticker","instId":"BTCUSDT"}]}`
- `instType` is ALWAYS `"SPOT"` for this objective.
- Batch: multiple arg OBJECTS in one frame (`BuildSubscribeBatch`/`BuildUnsubscribeBatch`; engine pre-chunks to 100).
- Channel → kind: `ticker`=Ticker, `trade`=Trade, `books5`/`books15`=OrderBook, `candle1m`…=Kline.
- Routing key (ADR-009-006) = `<channel>:<instId>`, e.g. `ticker:BTCUSDT`. Single-source it in one private `BuildRoutingKey(request)` helper used by BOTH `RoutingKeyFor` and `Classify`. Data-frame key reconstructed from `arg.channel + ":" + arg.instId`.
- Symbol wire format: `BTCUSDT` (no separator) via existing `BitgetSymbolFormat`.
- Kline channel names: implementor confirms Bitget v2 naming (`candle1m` family) from docs.

`Classify`:
- Data frame `{"action":"snapshot"/"update","arg":{...},"data":[...]}` → `FrameKind.Data` + composite routing key (both `snapshot` and `update` classify as Data on the same key — no local-book maintenance).
- Ack frame `{"event":"subscribe","arg":{...},"code":"0"}` → `FrameKind.Ack` (null key).
- Error frame `{"event":"error",...}` or non-zero `code` → `FrameKind.Error`.
- Heartbeat reply: if Bitget uses a text `"pong"` reply, handle the bare-text `pong` branch like OKX (check literal bytes before JSON parse → `FrameKind.Pong`). Confirm direction below.
- Unrecognised/empty → `FrameKind.Error`.

Heartbeat (ADR-009-003 — TBC): Bitget sends server `"ping"` ~30 s; client replies `"pong"`. Choose
`ServerPingClientPong` (let .NET auto-pong handle control-frame pings) OR `ClientPing`/`PingFormat.Text`
with a `"pong"`/`"ping"` text reply per what the v2 docs actually require. `MinOutboundInterval = 100 ms`
(ADR-009-004).

**Implementor MUST confirm from current Bitget v2 WS docs (TRD flag):** exact heartbeat direction
(control-frame Ping vs text `"ping"`/`"pong"`); available order-book channel names + depth levels and
the chosen default in `BuildSubscribe`; kline channel naming. Record confirmed values in the class
summary + test assertions; adjust the `Classify` pong branch + `HeartbeatPolicy` accordingly.

Tests (`BitgetStreamProtocolTests.cs`, mirroring `BinanceStreamProtocolTests.cs`, TEST-PLAN §File 1)
cover the full matrix; add the heartbeat/pong-frame test matching the confirmed behaviour; assert
composite `RoutingKeyFor_MatchesClassify_DataFrame`. Inline UTF-8 literals.

### Steps
1. Read `BinanceStreamProtocol.cs`, the just-built `OkxStreamProtocol.cs` (composite key + text-pong reference), and the shared `IStreamProtocol.cs`/`StreamConnectionInfo.cs`/`HeartbeatPolicy` (read-only).
2. Confirm Bitget heartbeat direction, order-book channel/depth, kline naming; record values.
3. Implement `BitgetStreamProtocol` with the `BuildRoutingKey` helper feeding both methods + `instType:SPOT` args; set the confirmed heartbeat + 100ms pacing; handle the pong branch per confirmed direction.
4. Write `BitgetStreamProtocolTests.cs` incl. the confirmed heartbeat/pong case.
5. `dotnet build` 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Acceptance Criteria
- [ ] `BitgetStreamProtocol : IStreamProtocol` (internal sealed) implements all members + batch builders with `instType:SPOT` args; composite routing key (`channel:instId`) single-sourced in one helper shared by `RoutingKeyFor` and `Classify` (snapshot+update both → Data); heartbeat + pong handling match the confirmed Bitget v2 direction; `MinOutboundInterval=100ms` (confirmed values recorded in the class summary).
- [ ] `BitgetStreamProtocolTests.cs` covers the full TEST-PLAN §File-1 matrix PLUS the confirmed heartbeat/pong case and composite `RoutingKeyFor_MatchesClassify_DataFrame`, mirroring `BinanceStreamProtocolTests.cs`, inline UTF-8 literals.
- [ ] `dotnet build CryptoExchanges.Net.sln` → 0W/0E; `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- Static-URL protocol + RoutingKey-shared-by-both: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs`
- Composite-key + text-pong reference (immediate intra-objective): `src/CryptoExchanges.Net.Okx/Streaming/OkxStreamProtocol.cs`
- Shared seam (read-only): `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs`, `StreamConnectionInfo.cs`
- Protocol tests: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamProtocolTests.cs`

## File Scope
**Creates**:
- src/CryptoExchanges.Net.Bitget/Streaming/BitgetStreamProtocol.cs
- tests/CryptoExchanges.Net.Bitget.Tests.Unit/Streaming/BitgetStreamProtocolTests.cs

**Modifies**: (none)

## Traceability
- **PRD Acceptance Criteria**: #2, #3
- **TRD Component**: Bitget variation §2 (BitgetStreamProtocol)
- **ADR Reference**: ADR-009-002, ADR-009-003 (TBC heartbeat), ADR-009-004, ADR-009-006

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
