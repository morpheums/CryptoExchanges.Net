# Test Plan — FEAT-006: KuCoin Exchange Integration

- **Status**: Approved for implementation
- **Date**: 2026-06-20
- **Feature ID**: FEAT-006
- **Test command (non-integration)**: `dotnet test --filter 'Category!=Integration'`

## Strategy

TDD within each build slice. Unit tests use injected fakes and stub HTTP handlers — no network.
Integration tests require `KUCOIN_API_KEY`, `KUCOIN_SECRET_KEY`, `KUCOIN_PASSPHRASE` env vars
and self-skip when absent (following `BinanceExchangeClient.CreateFromEnvironment` pattern).
All tests live in separate `tests/` projects; no test code co-located with source.

---

## Test Projects

| Project | Type | Purpose |
|---------|------|---------|
| `tests/CryptoExchanges.Net.Kucoin.Tests.Unit` | Unit | Signing, symbol mapping, DTO parsing, decoder closures, DI wiring, stub-HTTP service methods |
| `tests/CryptoExchanges.Net.Kucoin.Tests.Integration` | Integration | Live REST + one streaming smoke (self-skip without env vars) |
| `tests/CryptoExchanges.Net.Http.Tests.Unit` | Unit (existing, extended) | `IStreamProtocol` seam migration, engine reconnect behavior with `ResolveConnectionAsync` |
| `tests/CryptoExchanges.Net.Binance.Tests.Integration` | Integration (existing, regression) | Binance streaming regression after seam change |

---

## Unit Test Areas

### 1. Signing — `KucoinSignatureService`

| Test | Verification |
|------|-------------|
| `Sign_ComputesBase64HmacSha256` | Known prehash + secret → expected base64 signature (golden-value test). |
| `SignPassphrase_ComputesBase64HmacSha256` | `SignPassphrase(passphrase)` returns base64 HMAC-SHA256 of the passphrase with the secret key. |
| `BuildPrehash_ConcatenatesCorrectly` | `timestamp + METHOD + requestPath + body` assembled in exact order. |
| `FormatTimestamp_ReturnsUnixEpochMs` | `DateTimeOffset` → Unix ms string (not ISO-8601 like OKX). |

### 2. Signing Handler — `KucoinSigningHandler`

| Test | Verification |
|------|-------------|
| `UnsignedRequest_PassesThrough` | Request without `KucoinSigningRequest` marker: no KC-API-* headers added. |
| `SignedRequest_AllFiveHeadersPresent` | Marked request: `KC-API-KEY`, `KC-API-SIGN`, `KC-API-TIMESTAMP`, `KC-API-PASSPHRASE`, `KC-API-KEY-VERSION: 2`. |
| `RetryResigns_WithFreshTimestamp` | Handler called twice on same request (simulate retry): timestamps differ; no duplicate headers. |
| `MissingApiKey_Throws` | `InvalidOperationException` when `apiKey` is empty. |
| `MissingPassphrase_Throws` | `InvalidOperationException` when passphrase is null/empty. |

### 3. Symbol Mapper — `KucoinSymbolFormat` / `ISymbolMapper`

| Test | Verification |
|------|-------------|
| `ToWire_FormatsWithDash` | `Symbol(BTC, USDT)` → `"BTC-USDT"`. |
| `FromWire_ParsesDash` | `"BTC-USDT"` → `Symbol(BTC, USDT)`. |
| `IsSupported_ReturnsTrueForRegistered` | Registered pair → `true`. |
| `ToWire_ThrowsForUnsupported` | Unregistered symbol → `ExchangeException`. |

### 4. DTO Deserialization / Value Parsers

| Test | Verification |
|------|-------------|
| `ParseDecimal_StringNumeric` | `"123.45"` → `123.45m`. |
| `ParseDecimal_EmptyString` | `""` → `0m` or `null` per convention. |
| `TickerDto_Roundtrip` | JSON fixture → `TickerDto` fields populated. |
| `OrderBookDto_Roundtrip` | JSON fixture → bids/asks arrays. |
| `BulletPublicDto_Roundtrip` | Negotiation JSON fixture → `token`, `instanceServers[0].endpoint/pingInterval/pingTimeout`. |

### 5. DeltaMapper Profiles — `KucoinMappingProfiles`

| Test | Verification |
|------|-------------|
| `TickerDto_MapsToTicker` | All canonical `Ticker` fields populated; `Symbol` resolved via `ISymbolMapper.FromWire`. |
| `OrderDto_MapsToOrder` | `OrderStatus`, `OrderSide`, `OrderType` enums mapped correctly. |
| `BalanceDto_MapsToAssetBalance` | Asset + free/locked decimals. |
| `FillDto_MapsToTrade` | Fill fields → `Trade` model. |

### 6. Stream Decoders — `KucoinStreamDecoders`

Each decoder tested with a JSON bytes fixture (no network):

| Test | Verification |
|------|-------------|
| `TickerDecoder_ReturnsTicker` | Bytes of a ticker message frame → `Ticker` with correct price/volume. |
| `TradeDecoder_ReturnsTrade` | Bytes of a trade frame → `Trade` with price/qty/side/timestamp. |
| `OrderBookDecoder_ReturnsOrderBook` | Depth frame → `OrderBook` with bids/asks. |
| `KlineDecoder_ReturnsCandlestick` | Candle frame → `Candlestick` with OHLCV + interval. |

### 7. `KucoinStreamProtocol`

| Test | Verification |
|------|-------------|
| `BuildSubscribe_TickerTopic` | `StreamKind.Ticker` + `BTC-USDT` → `{"type":"subscribe","topic":"/market/ticker:BTC-USDT",...}`. |
| `BuildSubscribe_TradeTopic` | `StreamKind.Trade` → `/market/match:BTC-USDT`. |
| `BuildSubscribe_OrderBookTopic` | `StreamKind.OrderBook` → `/market/level2:BTC-USDT`. |
| `BuildSubscribe_KlineTopic` | `StreamKind.Kline` + `OneMinute` → `/market/candles:BTC-USDT_1min`. |
| `BuildUnsubscribe_TypeIsUnsubscribe` | `"type":"unsubscribe"` in wire JSON. |
| `RoutingKeyFor_MatchesClassifyKey` | `RoutingKeyFor` and `Classify` produce the same key for a round-trip. |
| `Classify_DataFrame_ReturnsData` | `{"type":"message","topic":"/market/ticker:BTC-USDT",...}` → `FrameKind.Data`, routing key `/market/ticker:BTC-USDT`. |
| `Classify_AckFrame_ReturnsAck` | `{"type":"ack","id":"..."}` → `FrameKind.Ack`. |
| `Classify_PongFrame_ReturnsPong` | `{"type":"pong"}` → `FrameKind.Pong`. |
| `Classify_ErrorFrame_ReturnsError` | `{"type":"error","code":"..."}` → `FrameKind.Error`. |
| `ResolveConnectionAsync_UsesBulletPublicResult` | Fake `KucoinBulletPublicClient` returning a fixture `BulletPublicDto` → `StreamConnectionInfo` has correct `Uri` (with token query param) and `HeartbeatPolicy` (pingInterval ms). |

### 8. `IStreamProtocol` Seam Migration — Existing Engine Tests (extended)

Tests live in `CryptoExchanges.Net.Http.Tests.Unit`, using a fake `IStreamProtocol` implementation:

| Test | Verification |
|------|-------------|
| `Engine_CallsResolveConnectionAsync_OnFirstConnect` | `ResolveConnectionAsync` called once per `OpenSocketAsync`. |
| `Engine_CallsResolveConnectionAsync_OnEachReconnect` | `ResolveConnectionAsync` called on every reconnect attempt (not cached from first connect). |
| `BinanceProtocol_ResolveConnectionAsync_ReturnsCachedInfo` | `BinanceStreamProtocol` returns pre-computed `StreamConnectionInfo`; `ResolveConnectionAsync` called multiple times returns identical reference. |
| `Engine_PropagatesCancellationFromResolve` | `ResolveConnectionAsync` throws `OperationCanceledException` → engine propagates / aborts connect. |

### 9. DI Registration — `AddKucoinExchange`

| Test | Verification |
|------|-------------|
| `AddKucoinExchange_ResolvesIExchangeClient` | Service provider resolves `IExchangeClient` keyed by `ExchangeId.Kucoin`. |
| `AddKucoinExchange_ThrowsOnMissingApiKey` | `ValidateOnStart` fails when `KucoinOptions.ApiKey` is empty. |
| `AddKucoinStreams_ResolvesStreamClient` | Service provider resolves streaming client keyed by `ExchangeId.Kucoin`. |

---

## Integration Test Areas (self-skip without env vars)

All integration tests call `KucoinExchangeClient.CreateFromEnvironment()` or the DI equivalent
and skip if `KUCOIN_API_KEY` is not set.

| Test | Scope |
|------|-------|
| `GetServerTime_ReturnsTimestamp` | REST smoke — public endpoint. |
| `GetTicker_BtcUsdt_ReturnsTicker` | REST market data — public. |
| `GetOrderBook_BtcUsdt_ReturnsOrderBook` | REST market data — public. |
| `GetBalances_WithCredentials_ReturnsBalances` | REST account — signed. |
| `PlaceAndCancelOrder_LimitBuy_Roundtrip` | REST trading — signed (places a far-out-of-market limit order then cancels). |
| `StreamTicker_BtcUsdt_ReceivesUpdate` | Streaming smoke: subscribe ticker, await one frame within 30s. |
| `StreamReconnect_TokenRenegotiated` | Force-close the socket; verify reconnect calls bullet-public again; verify callback resumes. |

---

## Regression Coverage

| Area | How |
|------|-----|
| Binance streaming after seam change | Existing `CryptoExchanges.Net.Binance.Tests.Integration` suite must pass unchanged. |
| `StreamEngine` reconnect logic | All existing `CryptoExchanges.Net.Http.Tests.Unit` streaming tests must pass; new tests extend them. |
| Core / Http unit suites | `dotnet test --filter 'Category!=Integration'` must stay 100% green. |

---

## Definition of Done for Tests

- All non-integration tests pass in CI (`dotnet test --filter 'Category!=Integration'`): 0 failures.
- New unit tests use NSubstitute fakes and stub HTTP handlers; no `Thread.Sleep`; no network.
- Integration tests carry `[Trait("Category", "Integration")]` and self-skip via `Skip.If` when
  env vars are absent.
- Build: 0 warnings, 0 errors (`TreatWarningsAsErrors`).
