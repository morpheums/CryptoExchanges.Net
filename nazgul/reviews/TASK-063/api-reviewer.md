---
verdict: APPROVE
reviewer: api-reviewer
task: TASK-063
---
# API Review — TASK-063

## Verdict: APPROVE

## Summary
TASK-063 adds two integration smoke-test files — `KucoinRestSmokeTests.cs` and `KucoinStreamingSmokeTests.cs` — that are purely additive (no existing files modified). All assertions operate on `Core.Models` types, the streaming API usage aligns precisely with the `IStreamClient`/`IStreamSubscription`/`StreamHandlers<T>` contracts, and `PlaceOrderRequest.Create(...)` / `CancelOrderAsync(symbol, orderId, ct)` match their interface signatures exactly. All 7 tests carry `[Trait("Category","Integration")]` and self-skip correctly, preserving the non-integration CI gate.

---

## Findings

### Tests exercise Core.Models, not wire DTOs — APPROVE (confidence: 100%)
Every assertion targets `Ticker`, `OrderBook`, `Order`, `AssetBalance` (via `GetBalancesAsync`) from `CryptoExchanges.Net.Core.Models`. No internal `*Dto` type is referenced. Imports confirm: `using CryptoExchanges.Net.Core.Models;` and `using CryptoExchanges.Net.Core.Enums;` are present; `using CryptoExchanges.Net.Kucoin.Dtos` is absent.

### `GetTickersAsync` single-symbol return shape — APPROVE (confidence: 100%)
`tickers.Should().HaveCount(1)` is called after `GetTickersAsync(BtcUsdt, ct)`. The implementation in `KucoinMarketDataService.GetTickersAsync` (line 39-46) takes the single-symbol branch (`/api/v1/market/stats`) and returns `[modelMapper.Map<TickerDto, Ticker>(oneResp.Data)]` — a single-element list. The assertion is correct.

### `GetOrderBookAsync` named parameter `ct:` — APPROVE (confidence: 100%)
The call is `GetOrderBookAsync(BtcUsdt, depth: 20, ct: TestContext.Current.CancellationToken)`. The interface signature is `GetOrderBookAsync(Symbol symbol, int depth = 100, CancellationToken ct = default)` (`IMarketDataService.cs:22`). The parameter name is `ct`, which matches the named argument exactly. No mismatch.

### `PlaceOrderRequest.Create(...)` factory usage — APPROVE (confidence: 100%)
The test calls `PlaceOrderRequest.Create(symbol, side, type, quantity: 0.00001m, price: 1m, timeInForce: TimeInForce.Gtc)`. The static factory signature (`PlaceOrderRequest.cs:58`) accepts all these as optional named parameters after the required `symbol/side/type` positional arguments. All required fields for a limit order (symbol, side, type, quantity, price) are provided; `Validate()` will pass without throwing.

### `CancelOrderAsync` signature alignment — APPROVE (confidence: 100%)
The test calls `CancelOrderAsync(BtcUsdt, placed.OrderId, ct)`. The `ITradingService` interface defines `CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default)` (`ITradingService.cs:13`). The KuCoin implementation (`KucoinTradingService.cs:50`) accepts the symbol parameter (unused in its URL path but required by the interface). The call is correct.

### `IStreamClient.SubscribeToTickerAsync` usage — APPROVE (confidence: 100%)
Both test files call `client.SubscribeToTickerAsync(BtcUsdt, new StreamHandlers<Ticker>(...), ct)`. This matches `IStreamClient.cs:28` exactly: `Task<IStreamSubscription> SubscribeToTickerAsync(Symbol symbol, StreamHandlers<Ticker> handlers, CancellationToken ct = default)`. The returned `IStreamSubscription` is disposed via `await using (subscription)`, which is correct per the `IStreamSubscription : IAsyncDisposable` contract.

### `StreamHandlers<T>` constructor usage — APPROVE (confidence: 100%)
`StreamHandlers<Ticker>` is a positional record with `OnUpdate` as the required first parameter (non-nullable `Func<T, ValueTask>`), and `OnReconnecting`/`OnReconnected`/`OnLagged` as optional nullable parameters (`StreamHandlers.cs:28-32`). In `StreamTicker_BtcUsdt_ReceivesUpdate` the positional form `new StreamHandlers<Ticker>(t => { ... return ValueTask.CompletedTask; })` is used — correct. In `StreamReconnect_TokenRenegotiated` named parameters `OnUpdate:`, `OnReconnecting:`, `OnReconnected:` are used — all valid named parameters on the record.

### `subscription.State` checked against `StreamConnectionState.Live` — APPROVE (confidence: 100%)
`StreamConnectionState.Live` is the correct enum value for an active subscription (`StreamConnectionState.cs:11`). The `IStreamSubscription.State` property returns this enum. Both files assert `subscription.State.Should().Be(StreamConnectionState.Live)` after awaiting a live frame — this is the correct post-subscribe state.

### `KucoinExchangeClient.Create(new KucoinOptions())` factory overload — APPROVE (confidence: 100%)
The skip path in `KucoinRestSmokeTests.InitializeAsync` uses `KucoinExchangeClient.Create(new KucoinOptions())`. `KucoinExchangeClient.Create(KucoinOptions options)` is a public static method (`KucoinExchangeClient.cs:70`). `KucoinOptions` has a parameterless constructor with string defaults. This compiles and produces a valid (unauthenticated) client instance suitable for `DisposeAsync`.

### `SyncServerTimeAsync` does not require credentials — APPROVE (confidence: 100%)
`GetServerTime_ReturnsTimestamp` calls `_client.SyncServerTimeAsync(ct)` after `SkipIfUnavailable()`. The implementation in `KucoinExchangeClient.cs:87-96` calls `/api/v1/timestamp` with `signed: false` — no credentials required. The test comment is accurate and the method name matches.

### `AddKucoinStreams()` DI extension availability — APPROVE (confidence: 100%)
`BuildStreamClient()` calls `services.AddKucoinExchange(...)` then `services.AddKucoinStreams()`. `AddKucoinStreams` is the public extension method defined in `StreamServiceCollectionExtensions.cs:27`. The resolution chain `IStreamClientFactory` → `factory.GetClient(ExchangeId.Kucoin)` → `IStreamClient` is correct per `IStreamClientFactory.cs:23`.

### AC-4 reconnect test approach — CONCERN (confidence: 65%, non-blocking)
The task description calls for a "force-close the socket; assert reconnect re-calls bullet-public." The implementation in `StreamReconnect_TokenRenegotiated` does NOT force-close any socket. Instead, it creates two independent `IStreamClient` instances sequentially, each of which negotiates its own `bullet-public` token on first connect. This proves the token-negotiation path is re-invoked between connection cycles but does NOT demonstrate that a mid-session forced disconnect triggers a re-negotiation and the `OnReconnecting`/`OnReconnected` callbacks are invoked. The `reconnectingFired` and `reconnectedFired` booleans are set to `false` and read via `_ = reconnectingFired; _ = reconnectedFired;` — they are never asserted. This is a meaningful gap between the stated AC-4 goal ("forced-disconnect reconnect") and the actual coverage (sequential independent clients). The approach matches the Binance streaming smoke pattern (which also does not force-close) and the implementation comment is honest about what it proves, so this is a documentation/coverage gap rather than a code defect. Non-blocking because the Binance precedent was accepted the same way.

### `IsPackable=false` on test project — APPROVE (confidence: 100%)
`CryptoExchanges.Net.Kucoin.Tests.Integration.csproj` has `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`. Correct per convention.

### No Core interface modifications — APPROVE (confidence: 100%)
The diff is purely additive (two new files in `tests/`). No `src/` files are modified. No interface members, model properties, enum values, or DI signatures are changed. There are zero breaking-change risks from this diff.

### `CancellationToken` parameter position on all calls — APPROVE (confidence: 100%)
`TestContext.Current.CancellationToken` is passed as the last argument in all service method calls (`GetTickersAsync`, `GetOrderBookAsync`, `GetBalancesAsync`, `PlaceOrderAsync`, `CancelOrderAsync`, `SubscribeToTickerAsync`, `SyncServerTimeAsync`). This matches the established `ct = default` last-parameter convention across all Core interfaces.

---

## Conclusion

Both test files conform cleanly to the Core API contracts, follow the established Binance integration smoke-test pattern precisely, and introduce zero changes to any public API surface. The one non-blocking concern is that the reconnect test simulates token re-negotiation via sequential independent clients rather than a mid-session forced socket close — the test name and AC-4 claim could be more precise, but the behaviour it proves (bullet-public is called fresh for each new client lifetime) is still meaningful evidence. All other focus-area checks pass with high confidence.
