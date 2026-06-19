# API Reviewer — TASK-037

## Verdict: CHANGES_REQUESTED

---

## Findings

### REJECT — Trade model table shows `TradeId` instead of `Id` (confidence: 100%)

`architecture.md` line 72, domain models table:

```
| `Trade` | `TradeId`, `Price`, `Quantity`, `Timestamp`, `IsBuyerMaker` |
```

The actual `Trade` record in `src/CryptoExchanges.Net.Core/Models/Trade.cs` is:

```csharp
public sealed record Trade(
    Symbol Symbol,
    string? Id = null,
    decimal Price = 0,
    decimal Quantity = 0,
    DateTimeOffset? Timestamp = null,
    bool IsBuyerMaker = false,
    string? OrderId = null);
```

The field is `Id` (nullable `string?`), not `TradeId`. The table also omits `Symbol` and `OrderId` from the key fields. The field name `TradeId` does not exist on this type.

Fix: Change `TradeId` to `Id` in the Trade row. The table entry for key fields could read: `Symbol`, `Id` (nullable), `Price`, `Quantity`, `Timestamp`, `IsBuyerMaker`, `OrderId`.

---

### REJECT — Order model table shows `Quantity, FilledQuantity` instead of `OriginalQuantity, ExecutedQuantity` (confidence: 100%)

`architecture.md` line 73, domain models table:

```
| `Order` | `OrderId`, `Symbol`, `Side`, `Type`, `Status`, `Price`, `Quantity`, `FilledQuantity`, `CreatedAt` |
```

The actual `Order` record in `src/CryptoExchanges.Net.Core/Models/Order.cs`:

```csharp
public sealed record Order(
    Symbol Symbol,
    string OrderId,
    string? ClientOrderId = null,
    decimal Price = 0,
    decimal OriginalQuantity = 0,
    decimal ExecutedQuantity = 0,
    ...
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? UpdatedAt = null)
```

Neither `Quantity` nor `FilledQuantity` exist on the `Order` type. Callers using these field names will get a compile error. The correct names are `OriginalQuantity` and `ExecutedQuantity`.

Fix: Replace `Quantity, FilledQuantity` with `OriginalQuantity, ExecutedQuantity` in the Order row.

---

### REJECT — Handler chain diagram labels the signing slot `BinanceSigningHandler`, which is not a type in the Http layer (confidence: 90%)

`architecture.md` lines 99–108:

```
ErrorTranslationHandler       — maps 4xx to typed ExchangeException via IExchangeErrorTranslator
  ↑
BinanceSigningHandler         — adds timestamp + HMAC-SHA256 signature (per-exchange impl)
  ↑
ResilienceHandler (Polly)     — retry (GET only) + per-attempt timeout
  ↑
TransientExhaustionHandler    — detects transient exhaustion, throws ExchangeConnectivityException
  ↑
RateLimitThrottleHandler      — proactive back-pressure from ReactiveRateLimitGate
```

The shared `HttpClientPipelineBuilder.Build()` in `CryptoExchanges.Net.Http` accepts a `DelegatingHandler? requestFinalizer` parameter for the per-exchange signing slot — it is not named or typed as `BinanceSigningHandler` at the Http layer. Labeling it `BinanceSigningHandler` in a diagram purporting to show the shared Http infrastructure is both inaccurate (the type is internal to the Binance package, not a shared layer type) and misleading (implies the chain is Binance-specific, not shared). The prose below the diagram does correctly note "per-exchange impl," but the diagram label itself will confuse readers who look for `BinanceSigningHandler` in the Http package or assume this is a shared abstract class.

Fix: Rename the slot label to `*SigningHandler (per-exchange DelegatingHandler)` or `[exchange signing handler]` to make clear this is the injection point for a per-exchange implementation, not a concrete type in the Http layer.

---

### CONCERN — `IExchangeClientFactory` placement in the layer diagram (confidence: 75%)

`architecture.md` lines 35–38 (DI layer box):

```
│  CryptoExchanges.Net.DependencyInjection                         │
│  AddCryptoExchanges() · AddBinanceExchange() · …                 │
│  IExchangeClientFactory · keyed-singleton registration           │
```

The `IExchangeClientFactory` interface lives in `CryptoExchanges.Net.Core.Interfaces` (namespace `CryptoExchanges.Net.Core.Interfaces`). Its implementation `ExchangeClientFactory` (internal sealed class) lives in `CryptoExchanges.Net.Http`. Neither lives in `CryptoExchanges.Net.DependencyInjection`.

Placing `IExchangeClientFactory` in the DI layer box is architecturally misleading — the interface is a Core contract, not a DI artifact. A consumer who depends only on Core can reference `IExchangeClientFactory` without taking a DI package dependency.

This is non-blocking (the interface itself is correctly described later in the Interfaces table, and the consumer-facing usage examples are correct), but the diagram is inaccurate about where the contract lives.

Fix: Move `IExchangeClientFactory` from the DI layer box to the Core layer box, and note in the DI box that it registers the `ExchangeClientFactory` implementation.

---

### CONCERN — Dead link to `mcp-server.md` in library-usage.md (confidence: 95%)

`library-usage.md` line 391 (Further reading section):

```
- [MCP server](mcp-server.md) — AI-agent read-only access via the Model Context Protocol
```

`mcp-server.md` is not one of the four files delivered in this task and does not exist in the `docs/` directory. This is a broken cross-reference.

Fix: Remove the `mcp-server.md` link from the Further reading section, or only add it once the file is delivered.

---

### PASS — All method signatures on IMarketDataService correct (confidence: 100%)

`GetTickersAsync(Symbol? symbol = null)`, `GetOrderBookAsync(Symbol, int depth = 100)`, `GetCandlesticksAsync(Symbol, KlineInterval, DateTimeOffset? startTime, DateTimeOffset? endTime, int limit)`, `GetPriceAsync(Symbol)`, `GetRecentTradesAsync(Symbol, int limit)`, `GetExchangeInfoAsync()`, `IsSupportedAsync(Symbol)`, `ResolveSymbolAsync(Symbol)` — all match source exactly. Return types (`IReadOnlyList<T>` for collections, `Task<decimal>` for price, `Task<OrderBook>` for order book, `Task<ExchangeInfo>` for exchange info) are correct.

---

### PASS — ITradingService signatures correct (confidence: 100%)

`PlaceOrderAsync`, `CancelOrderAsync`, `CancelOrderByClientIdAsync`, `CancelAllOrdersAsync`, `GetOrderAsync`, `GetOpenOrdersAsync(Symbol? symbol = null)`, `GetOrderHistoryAsync` — all names, parameter names, and return types match source.

---

### PASS — IAccountService signatures correct (confidence: 100%)

`GetBalancesAsync()`, `GetBalanceAsync(Asset)`, `GetTradeHistoryAsync(Symbol, int limit, DateTimeOffset? startTime, DateTimeOffset? endTime)` — all match source.

---

### PASS — PlaceOrderRequest API usage correct (confidence: 100%)

`library-usage.md` examples use `Symbol`, `Side`, `Type`, `Quantity`, `Price` as object initializer properties (all `required` or optional on the record) and `PlaceOrderRequest.Create(symbol, side, type, quantity)` factory invocation — consistent with source. `QuoteOrderQuantity` mention for market orders is correct.

---

### PASS — All exchange client factory methods correct (confidence: 100%)

`BinanceExchangeClient.Create(BinanceOptions)`, `CreateFromEnvironment()`, `BybitExchangeClient.Create(BybitOptions)`, `CreateFromEnvironment()`, `OkxExchangeClient.Create(OkxOptions)`, `CreateFromEnvironment()`, `BitgetExchangeClient.Create(BitgetOptions)`, `CreateFromEnvironment()` — all match source. All are `static` methods on sealed concrete classes.

---

### PASS — All environment variable names correct (confidence: 100%)

`BINANCE_API_KEY`, `BINANCE_SECRET_KEY`, `BYBIT_API_KEY`, `BYBIT_SECRET_KEY`, `OKX_API_KEY`, `OKX_SECRET_KEY`, `OKX_PASSPHRASE`, `BITGET_API_KEY`, `BITGET_SECRET_KEY`, `BITGET_PASSPHRASE` — verified against `CreateFromEnvironment()` implementations in all four exchange clients.

---

### PASS — CryptoExchangesOptions property names correct (confidence: 100%)

`BinanceApiKey`, `BinanceSecretKey`, `BybitApiKey`, `BybitSecretKey`, `OkxApiKey`, `OkxSecretKey`, `OkxPassphrase`, `BitgetApiKey`, `BitgetSecretKey`, `BitgetPassphrase` — all match `CryptoExchangesOptions.cs`.

---

### PASS — Exception hierarchy and ExchangeApiException properties correct (confidence: 100%)

Hierarchy (`ExchangeException` → `ExchangeApiException`, `ExchangeConnectivityException`; `ExchangeApiException` → `AuthenticationException`, `RateLimitExceededException`, `InvalidOrderException`, `InsufficientBalanceException`) matches source. `ExchangeApiException.Code` (int?) and `RawBody` (string?) are correct.

---

### PASS — IExchangeClientFactory interface surface correct (confidence: 100%)

`Available` (`IReadOnlyCollection<ExchangeId>`), `GetClient(ExchangeId)`, `TryGet(ExchangeId, out IExchangeClient?)` — all match source. Usage examples with `factory.GetClient(ExchangeId.Binance)` are correct.

---

### PASS — Asset model and constants correct (confidence: 100%)

`Asset.Of(string)`, `Asset.TryOf(string, out Asset)`, `Asset.None`, `Asset.IsNone`, curated constants (`Btc`, `Eth`, `Bnb`, `Sol`, `Xrp`, `Ada`, `Doge`, `Trx`, `Usdt`, `Usdc`, `Fdusd`, `Dai`, `Eur`, `Gbp`) — all match source.

---

### PASS — AssetBalance fields correct (confidence: 100%)

`Asset`, `Free`, `Locked`, `Total` — match source (note: `Total` is a computed property `Free + Locked`, not a constructor parameter, but it is correctly represented as a field in the docs).

---

### PASS — ExchangeInfo and SymbolInfo fields correct (confidence: 100%)

`ExchangeInfo`: `ExchangeName`, `Symbols`, `RateLimits`. `SymbolInfo`: `Symbol`, `AllowedOrderTypes`. Both match source.

---

### PASS — KlineInterval enum values correct (confidence: 100%)

`OneMinute`, `ThreeMinutes`, `FiveMinutes`, `FifteenMinutes`, `ThirtyMinutes`, `OneHour`, `TwoHours`, `FourHours`, `SixHours`, `EightHours`, `TwelveHours`, `OneDay`, `ThreeDays`, `OneWeek`, `OneMonth` — match source.

---

### PASS — Namespace usings in code samples correct (confidence: 100%)

`CryptoExchanges.Net.Core.Models`, `CryptoExchanges.Net.Core.Interfaces`, `CryptoExchanges.Net.Core.Enums`, `CryptoExchanges.Net.Core.Exceptions`, `CryptoExchanges.Net.Binance`, `CryptoExchanges.Net.Bybit`, `CryptoExchanges.Net.Okx`, `CryptoExchanges.Net.Bitget`, `CryptoExchanges.Net.DependencyInjection` — all match actual namespace declarations in source.

---

### PASS — Opsec check clean (confidence: 100%)

No WebSocket references, no gateway, no AI positioning, no monetization, no roadmap/strategy content outside "Coming soon" (exchanges only: Coinbase, Kraken, KuCoin). All three listed as "coming soon" are indeed present in the `ExchangeId` enum as unimplemented values (Coinbase, Kraken, Kucoin).

---

### PASS — Trade.Quantity field usage in library-usage.md correct (confidence: 100%)

`library-usage.md` line 125 uses `t.Quantity` on a `Trade` instance. The `Trade` record does have a `Quantity` field — confirmed in source.

---

## Summary

Two hard-incorrect model field names in `architecture.md`'s domain models table require a fix before merge: `TradeId` should be `Id`, and `Quantity`/`FilledQuantity` should be `OriginalQuantity`/`ExecutedQuantity`. The handler chain diagram in `architecture.md` also incorrectly labels the per-exchange signing slot as `BinanceSigningHandler`, a concrete internal type that does not exist in the shared Http layer — this should be relabeled as a generic injection point. A dead cross-reference to a non-existent `mcp-server.md` file should be removed. Everything else — method signatures, return types, env var names, DI option properties, exception hierarchy, enum values, and exchange client factory API — is accurate against source.
