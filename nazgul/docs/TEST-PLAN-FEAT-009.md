# Test Plan — FEAT-009: WebSocket Streaming for Bybit, OKX, Bitget

## Strategy

Mirror the existing Binance and KuCoin test suites exactly. Each exchange gets three
test files in its existing unit-test project, plus one integration smoke test. No new
test projects are created — the existing `CryptoExchanges.Net.Bybit.Tests.Unit`,
`.Integration`, and equivalent OKX/Bitget projects are extended.

Unit tests use xunit.v3 + AwesomeAssertions (FluentAssertions alias used in codebase) +
NSubstitute. No sockets, no HTTP.

---

## Per-Exchange Test Breakdown

The same three unit-test files and one integration file are required for each of the
three exchanges. The content mirrors the Binance equivalents exactly — only frame bytes,
topic strings, and routing keys change.

### File 1: `XxxStreamProtocolTests.cs` — Protocol unit tests

**Location:** `tests/CryptoExchanges.Net.Xxx.Tests.Unit/Streaming/`

**Reference:** `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamProtocolTests.cs`

Required test cases:

| Test | Assertion |
|------|-----------|
| `Classify_DataFrame_ReturnsDataWithRoutingKey` | Canned JSON bytes for a ticker frame → `FrameKind.Data`, routing key matches expected topic string |
| `Classify_SubscribeAckFrame_ReturnsAck` | Ack frame bytes → `FrameKind.Ack`, null routing key |
| `Classify_ErrorFrame_ReturnsError` | Error frame bytes → `FrameKind.Error` |
| `Classify_UnrecognisedFrame_ReturnsError` | `{"mystery":true}` → `FrameKind.Error` |
| `Classify_EmptyFrame_ReturnsError` | Empty span → `FrameKind.Error` |
| `BuildSubscribe_Ticker_ProducesCorrectTopic` | `RoutingKeyFor` result appears in `BuildSubscribe` output |
| `BuildSubscribe_Trade_ProducesCorrectTopic` | Same as above for trade |
| `BuildSubscribe_OrderBook_ProducesCorrectTopic` | Same for order-book (with depth if applicable) |
| `BuildSubscribe_Kline_ProducesCorrectTopic` | Same for kline (with interval token) |
| `BuildUnsubscribe_Ticker_ProducesCorrectTopic` | Unsubscribe uses the same topic / correct op |
| `BuildSubscribeBatch_TwoRequests_ProducesOneFrame` | Two requests → single batched frame, both topics present |
| `RoutingKeyFor_MatchesClassify_DataFrame` | `RoutingKeyFor(request)` equals the routing key from `Classify` for the matching data frame |
| `ResolveConnectionAsync_ReturnsStaticEndpoint` | Endpoint URI matches configured base URL |
| `ResolveConnectionAsync_HeartbeatDirection_MatchesVenuePolicy` | Correct direction per ADR-009-003 |
| `ResolveConnectionAsync_MinOutboundInterval_Is100ms` | Confirms pacing floor |

For Bybit: add a `Classify_PongFrame` test if the venue sends a distinct pong JSON shape.
For OKX: add `Classify_TextPongFrame_ReturnsPong` — the bare `"pong"` text frame.
For Bitget: add equivalent per confirmed heartbeat behaviour.

### File 2: `XxxStreamDecodeTests.cs` — Decoder unit tests

**Location:** `tests/CryptoExchanges.Net.Xxx.Tests.Unit/Streaming/`

**Reference:** `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDecodeTests.cs`

Each test provides a canned full push-frame byte array (as would arrive from the socket,
including any outer envelope), calls the decoder closure from `XxxStreamDecoders.Build(...)`,
and asserts the `Core.Models` output.

| Test | Input | Asserts |
|------|-------|---------|
| `Decode_Ticker_MapsToTicker` | Canned ticker frame | `Ticker.Symbol`, `LastPrice` non-zero |
| `Decode_Trade_MapsToTrade` | Canned trade frame | `Trade.Price`, `Trade.Quantity` non-zero, `IsBuyerMaker` correct |
| `Decode_OrderBook_MapsToBids` | Canned depth frame with 1+ bid | `OrderBook.Bids` non-empty, price/qty parseable |
| `Decode_OrderBook_MapsToAsks` | Same frame | `OrderBook.Asks` non-empty |
| `Decode_OrderBook_SymbolResolved` | Frame with symbol field | `OrderBook.Symbol` matches expected canonical symbol |
| `Decode_Kline_MapsToOhlcv` | Canned kline frame | `Candlestick.Open/High/Low/Close/Volume` all non-zero |

Use `FakeSymbolMapper` (already in `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/`)
or create a minimal inline substitute for the exchange's wire-symbol format.

### File 3: `XxxStreamDiTests.cs` — DI wiring tests

**Location:** `tests/CryptoExchanges.Net.Xxx.Tests.Unit/Streaming/`

**Reference:** `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDiTests.cs`

| Test | Assertion |
|------|-----------|
| `AddXxxStreams_ResolvesStreamClientFactory` | `sp.GetService<IStreamClientFactory>()` is not null |
| `AddXxxStreams_FactoryGetClient_ReturnsXxxClient` | `factory.GetClient(ExchangeId.Xxx).ExchangeId == ExchangeId.Xxx` |
| `AddXxxStreams_AvailableExchanges_ContainsXxx` | `factory.Available` contains the exchange's `ExchangeId` |

All three tests use an in-process `ServiceCollection` with `AddXxxExchange(...)` +
`AddXxxStreams()`. No network calls.

### File 4: `XxxStreamingSmokeTests.cs` — Integration smoke tests

**Location:** `tests/CryptoExchanges.Net.Xxx.Tests.Integration/`

**Reference:** `tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs`

`[Trait("Category", "Integration")]` on the class — excluded from the standard gate.
All tests self-skip via `Assert.SkipWhen` when the venue's WebSocket endpoint is
unreachable (8-second probe, same as Binance/KuCoin pattern).

| Test | Assertion |
|------|-----------|
| `Ticker_LiveStream_DeliversAtLeastOneUpdate` | `Ticker` callback fires ≥1 time within 20 s |
| `Trade_LiveStream_DeliversAtLeastOneUpdate` | `Trade` callback fires ≥1 time within 20 s |
| `Kline_LiveStream_DeliversAtLeastOneUpdate` | `Candlestick` callback fires ≥1 time within 20 s |
| `OrderBook_MultiSymbol_DeliversAtLeastOneUpdate` | Subscribe to ≥8 symbols; `OrderBook` callback fires ≥1 time within 30 s with no reconnect loop |

The multi-symbol order-book test is the **critical regression gate** for FEAT-008: it
confirms that paced subscribe replay does not trigger a rate-limit reconnect loop on
the new venue.

Multi-symbol set for each venue: use a mix of BTC/ETH/SOL/XRP/ADA/DOGE/AVAX/LTC
against USDT, in the venue's wire symbol format.

---

## Test Data (Wire Frame Examples)

Each `XxxStreamProtocolTests` and `XxxStreamDecodeTests` file must include inline
UTF-8 byte arrays for representative frames. Do not use external JSON files — inline
string literals like the Binance tests. Frames should be captured from the official
API documentation or by hand-inspection of the live feed during development.

---

## Excluded from Scope

- Order-book sequence consistency tests (no local-book maintenance in scope)
- Private/authenticated stream tests
- Futures or options stream tests
- StreamEngine-level tests (engine is tested in `Http.Tests.Unit/Streaming/`)
- Any modification to existing Binance or KuCoin test files

---

## Run Commands

```
# Unit tests only (CI gate)
dotnet test --filter 'Category!=Integration'

# Integration tests (manual / CI nightly)
dotnet test --filter 'Category=Integration'
```
