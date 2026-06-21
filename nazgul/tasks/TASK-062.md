---
id: TASK-062
status: IN_REVIEW
depends_on: [TASK-058, TASK-060, TASK-061]
---
# TASK-062: `KucoinStreamProtocol` + bullet-public negotiation + 4 decoders + `AddKucoinStreams`

## Metadata
- **ID**: TASK-062
- **Group**: 5
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-058, TASK-060, TASK-061
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs, src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs, src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamOptions.cs, src/CryptoExchanges.Net.Kucoin/Streaming/KucoinBulletPublicClient.cs, src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/BulletPublicDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTickerDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTradeDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamDepthDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamKlineDto.cs, src/CryptoExchanges.Net.Kucoin/StreamServiceCollectionExtensions.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamProtocolTests.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamDecodeTests.cs]
- **Wave**: 6
- **Traces to**: PRD-FEAT-006 AC-3, AC-4; TRD-FEAT-006 §"WebSocket Streaming — KuCoin Protocol", §"Data Flow (Streaming)"; FEAT-006 spec §"WebSocket streaming (public)", §"Build approach" step 7; TEST-PLAN-FEAT-006 §6, §7
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**: 2026-06-21T00:00:00Z
- **Base SHA**: fb8a8855b3f2901ffe7c07614c11273787203280
- **Implemented at**: 2026-06-21T00:30:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Implement KuCoin's public WebSocket protocol on the generalized seam, cloning the Binance streaming
package and adding KuCoin's token-negotiated connection. `Core.Models` + DeltaMapper legitimately live
here (K1 is Http-only). One type per file. Depends on the seam from TASK-061, the symbol mapper/profiles
from TASK-058, and the keyed DI registration from TASK-060 (for `AddKucoinStreams` to mirror).

Create:
- **`Dtos/Streaming/BulletPublicDto.cs`** — the `POST /api/v1/bullet-public` response shape: `token` +
  `instanceServers[{endpoint, pingInterval, pingTimeout}]` (under the `ResponseDto`/`data` envelope).
- **`Dtos/Streaming/StreamTickerDto.cs` / `StreamTradeDto.cs` / `StreamDepthDto.cs` /
  `StreamKlineDto.cs`** — the four KuCoin push frame shapes (the `{"type":"message","topic":...,"data":{...}}`
  inner `data` payloads; distinct from REST DTOs).
- **`Streaming/KucoinBulletPublicClient.cs`** — `NegotiateAsync(ct)` POSTs `/api/v1/bullet-public`
  (unauthenticated) via the resilient HTTP client → `BulletPublicDto`. Injectable interface so tests
  fake it (no network).
- **`Streaming/KucoinStreamProtocol.cs`** (`internal sealed : IStreamProtocol`):
  `ResolveConnectionAsync(ct)` → calls `KucoinBulletPublicClient.NegotiateAsync`, picks the first
  instance server, appends `?token={token}&connectId={Guid:N}`, returns `StreamConnectionInfo(uri,
  new HeartbeatPolicy(ClientPing, pingInterval, pingTimeout, jsonPingPayload, PingFormat.Json))` with the
  KuCoin ping payload `{"id":"<ts>","type":"ping"}`. `BuildSubscribe`/`BuildUnsubscribe` produce the
  KuCoin JSON (`type:subscribe|unsubscribe`, `topic`, `privateChannel:false`, `response:true`). Topic
  map: Ticker `/market/ticker:{WIRE}`, Trade `/market/match:{WIRE}`, OrderBook `/market/level2:{WIRE}`,
  Kline `/market/candles:{WIRE}_{INTERVAL_WIRE}`. `RoutingKeyFor` agrees with `Classify`. `Classify`
  reads `"type"`: `message`→Data (routing key = `topic`), `ack`→Ack, `pong`→Pong, `error`/unknown→Error.
  Pure data + classification — NO timers/threads (C1).
- **`Streaming/KucoinStreamDecoders.cs`** — 4 decode closures (`Func<ReadOnlyMemory<byte>, object>`):
  Ticker via DeltaMapper (`StreamTickerDto`→`Ticker`), Trade/OrderBook/Kline hand-mapped (matching the
  Binance streaming convention), reusing the keyed `ISymbolMapper` for wire→domain symbol. K1: DeltaMapper
  used HERE, in the Kucoin package.
- **`Streaming/KucoinStreamOptions.cs`** — `RestBaseUrl` (bullet-public host) + optional stream-ping
  override; validatable via `ValidateOnStart`.
- **`StreamServiceCollectionExtensions.cs`** — `AddKucoinStreams()` (~5–10 lines) delegating to
  `StreamServiceRegistration.AddStreams<KucoinStreamOptions>` supplying the protocol + decoder-registry
  factories, registering the keyed `IStreamClient` for `ExchangeId.Kucoin`. Mirror
  `BinanceStreamServiceCollectionExtensions`/`AddBinanceStreams`.

Tests (`Streaming/` in the Kucoin unit project), no network:
- Protocol: subscribe topic for each StreamKind (Ticker/Trade/OrderBook/Kline incl. `BTC-USDT_1min`);
  unsubscribe type; `RoutingKeyFor`==`Classify` round-trip; `Classify` for message/ack/pong/error;
  `ResolveConnectionAsync` with a FAKE `KucoinBulletPublicClient` returning a fixture `BulletPublicDto`
  → `StreamConnectionInfo` has the token query param + heartbeat from pingInterval ms.
- Decoders: each of the 4 with a JSON bytes fixture → correct `Core.Models` (ticker price/volume, trade
  price/qty/side/ts, orderbook bids/asks, candlestick OHLCV + interval).

## Acceptance Criteria
- [x] `KucoinStreamProtocol : IStreamProtocol` (`internal sealed`, `ResolveConnectionAsync` via fake-able `KucoinBulletPublicClient` → token+connectId URL + server-dictated `HeartbeatPolicy(ClientPing, JSON ping)`, KuCoin subscribe/unsubscribe wire, 4-topic map, `Classify` by `type`, `RoutingKeyFor`==`Classify`, no timers — C1) + 4 streaming DTOs + `BulletPublicDto` + `KucoinStreamOptions` exist, one type per file, full XML docs.
- [x] `KucoinStreamDecoders` registers 4 decode closures (Ticker via DeltaMapper; Trade/OrderBook/Kline hand-mapped) reusing the keyed `ISymbolMapper` → boxed `Core.Models`; `AddKucoinStreams()` registers the keyed `IStreamClient` for `ExchangeId.Kucoin`, mirroring `AddBinanceStreams`.
- [x] `Streaming/` unit tests assert subscribe/unsubscribe wire + 4-topic map + `Classify` (message/ack/pong/error) + `RoutingKeyFor` round-trip + `ResolveConnectionAsync` (fake bullet-public → token URL + heartbeat) + 4 decoder `Core.Models` outputs — ALL NO network; solution builds 0W/0E; existing non-integration suite stays green.

## Pattern Reference
- Streaming package to clone: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs`, `BinanceStreamDecoders.cs`, `BinanceStreamOptions.cs` + `Dtos/Streaming/Stream*Dto.cs`.
- Registration delegator: `src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs` (`AddBinanceStreams` → `AddStreams<TOptions>`).
- Seam (post-TASK-061): `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs` (`ResolveConnectionAsync`) + `StreamConnectionInfo.cs` + `HeartbeatPolicy.cs` + `PingFormat.cs` + `StreamKind.cs`.
- DeltaMapper profile + `ISymbolMapper` reuse: `src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs` (TASK-058). Bullet-public response shape: TRD-FEAT-006 §"Bullet-public negotiation". Decoder/protocol tests: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/`.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs
- src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs
- src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamOptions.cs
- src/CryptoExchanges.Net.Kucoin/Streaming/KucoinBulletPublicClient.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/BulletPublicDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTickerDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTradeDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamDepthDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamKlineDto.cs
- src/CryptoExchanges.Net.Kucoin/StreamServiceCollectionExtensions.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamProtocolTests.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamDecodeTests.cs

**Modifies**:
- (none — additive; `KucoinMappingProfiles` extended only if a WS DTO needs a new CreateMap, but prefer reuse)

## Traceability
- **PRD Acceptance Criteria**: AC-3 (4 public streams → Core.Models), AC-4 (reconnect re-negotiates token — `ResolveConnectionAsync` per connect), AC-7 (no-network tests)
- **TRD Component**: §"WebSocket Streaming — KuCoin Protocol", §"Data Flow (Streaming)"
- **ADR Reference**: ADR-002 (KuCoin implements `ResolveConnectionAsync`); FEAT-006 spec §"WebSocket streaming"; K1 (DeltaMapper in Kucoin pkg, not Http); C1 (protocol describes heartbeat)

## Commits

- `af4d08a` — feat(FEAT-006): TASK-062 — KucoinStreamProtocol + bullet-public negotiation + 4 decoders + AddKucoinStreams

## Implementation Log

## Review Results
