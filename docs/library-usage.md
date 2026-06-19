# Library Usage

Full reference with code examples for every major operation.
All examples compile against the shipped API — signatures are verified from source.

See [Getting Started](getting-started.md) for installation and credential setup.

---

## Symbols and Assets

Symbols are typed value types — never raw strings. Use the built-in `Asset` constants for common
tickers, or `Asset.Of(string)` for anything not in the curated list:

```csharp
using CryptoExchanges.Net.Core.Models;

// Typed constants
var btcUsdt  = new Symbol(Asset.Btc,  Asset.Usdt);
var ethUsdt  = new Symbol(Asset.Eth,  Asset.Usdt);
var solUsdc  = new Symbol(Asset.Sol,  Asset.Usdc);

// Long-tail tickers not in the curated list
var pepeUsdt = new Symbol(Asset.Of("PEPE"), Asset.Usdt);

// Asset.Of validates: non-empty, A-Z/0-9, max 32 chars
// Non-throwing variant
bool ok = Asset.TryOf("BTC", out var btc);
```

Built-in `Asset` constants include: `Btc`, `Eth`, `Bnb`, `Sol`, `Xrp`, `Ada`, `Doge`, `Trx`,
`Usdt`, `Usdc`, `Fdusd`, `Dai`, `Eur`, `Gbp`.

### Opt-in exchange validation

Validation against the exchange's supported symbol set is **opt-in** and is not called implicitly
by other methods. Results are cached from `GetExchangeInfoAsync`:

```csharp
// Check whether a symbol is listed
bool supported = await client.MarketData.IsSupportedAsync(btcUsdt);

// Resolve to the canonical form as returned by the exchange, or null if unsupported
Symbol? canonical = await client.MarketData.ResolveSymbolAsync(btcUsdt);
```

---

## Market Data

All market-data endpoints are public — no API key required.

### Latest price

```csharp
decimal price = await client.MarketData.GetPriceAsync(new Symbol(Asset.Btc, Asset.Usdt));
```

### 24-hour ticker

Pass a symbol for a single ticker, or omit it to receive all tickers:

```csharp
// Single ticker
IReadOnlyList<Ticker> tickers = await client.MarketData.GetTickersAsync(
    new Symbol(Asset.Eth, Asset.Usdt));

foreach (var t in tickers)
{
    Console.WriteLine(
        $"{t.Symbol}: last={t.LastPrice:N2}  " +
        $"high={t.HighPrice:N2}  low={t.LowPrice:N2}  " +
        $"change={t.PriceChangePercent:F2}%  vol={t.Volume:F4}");
}

// All tickers (no symbol filter)
IReadOnlyList<Ticker> all = await client.MarketData.GetTickersAsync();
```

### Order book

```csharp
OrderBook book = await client.MarketData.GetOrderBookAsync(
    new Symbol(Asset.Btc, Asset.Usdt), depth: 10);

Console.WriteLine($"Last update ID: {book.LastUpdateId}");
foreach (var ask in book.Asks.Take(5))
    Console.WriteLine($"  Ask {ask.Price:N2} qty {ask.Quantity:F6}");
foreach (var bid in book.Bids.Take(5))
    Console.WriteLine($"  Bid {bid.Price:N2} qty {bid.Quantity:F6}");
```

### Candlesticks

```csharp
using CryptoExchanges.Net.Core.Enums;

IReadOnlyList<Candlestick> candles = await client.MarketData.GetCandlesticksAsync(
    new Symbol(Asset.Btc, Asset.Usdt),
    KlineInterval.OneHour,
    limit: 24);

foreach (var c in candles)
{
    Console.WriteLine(
        $"[{c.OpenTime:HH:mm}] O={c.Open:N2} H={c.High:N2} L={c.Low:N2} C={c.Close:N2} V={c.Volume:F4}");
}
```

`KlineInterval` values: `OneMinute`, `ThreeMinutes`, `FiveMinutes`, `FifteenMinutes`,
`ThirtyMinutes`, `OneHour`, `TwoHours`, `FourHours`, `SixHours`, `EightHours`, `TwelveHours`,
`OneDay`, `ThreeDays`, `OneWeek`, `OneMonth`.

Optional `startTime`/`endTime` (`DateTimeOffset?`) filter the result window.

### Recent public trades

```csharp
IReadOnlyList<Trade> trades = await client.MarketData.GetRecentTradesAsync(
    new Symbol(Asset.Btc, Asset.Usdt), limit: 10);

foreach (var t in trades)
{
    var side = t.IsBuyerMaker ? "SELL" : "BUY";
    Console.WriteLine($"[{t.Timestamp:HH:mm:ss}] {side} {t.Quantity:F6} @ {t.Price:N2}");
}
```

### Exchange info

Returns trading rules, allowed order types, rate limits, and the full symbol list:

```csharp
ExchangeInfo info = await client.MarketData.GetExchangeInfoAsync();
Console.WriteLine($"Exchange: {info.ExchangeName}, symbols: {info.Symbols.Count}");

foreach (var s in info.Symbols.Take(3))
    Console.WriteLine($"  {s.Symbol}: {string.Join(", ", s.AllowedOrderTypes)}");
```

---

## Trading

Trading endpoints require an API key and secret.

### Place an order

Use `PlaceOrderRequest` — a `record` with required properties and an optional `Create` factory
that validates before returning:

```csharp
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Enums;

var btcUsdt = new Symbol(Asset.Btc, Asset.Usdt);

// Object initializer (inline validation via Validate())
Order order = await client.Trading.PlaceOrderAsync(new PlaceOrderRequest
{
    Symbol   = btcUsdt,
    Side     = OrderSide.Buy,
    Type     = OrderType.Limit,
    Quantity = 0.001m,
    Price    = 50_000m
});

Console.WriteLine($"Order ID: {order.OrderId}, status: {order.Status}");
```

Or use the factory method for explicit pre-call validation:

```csharp
var request = PlaceOrderRequest.Create(
    symbol:   btcUsdt,
    side:     OrderSide.Sell,
    type:     OrderType.Market,
    quantity: 0.001m);

Order order = await client.Trading.PlaceOrderAsync(request);
```

Market orders accept either `Quantity` (base asset) or `QuoteOrderQuantity` (quote asset spend),
not both.

### Cancel an order

```csharp
// Cancel by exchange-assigned order ID
Order cancelled = await client.Trading.CancelOrderAsync(btcUsdt, order.OrderId);

// Cancel by client-assigned ID
Order cancelled2 = await client.Trading.CancelOrderByClientIdAsync(btcUsdt, "my-client-id");

// Cancel ALL open orders for a symbol
IReadOnlyList<Order> cancelled3 = await client.Trading.CancelAllOrdersAsync(btcUsdt);
```

### Query orders

```csharp
// Retrieve a specific order
Order fetched = await client.Trading.GetOrderAsync(btcUsdt, order.OrderId);

// All currently open orders (all symbols)
IReadOnlyList<Order> open = await client.Trading.GetOpenOrdersAsync();

// Open orders for a specific symbol
IReadOnlyList<Order> openForBtc = await client.Trading.GetOpenOrdersAsync(btcUsdt);

// Historical orders
IReadOnlyList<Order> history = await client.Trading.GetOrderHistoryAsync(
    btcUsdt, limit: 50,
    startTime: DateTimeOffset.UtcNow.AddDays(-7));
```

---

## Account

Account endpoints require an API key and secret.

### Balances

```csharp
// All non-zero balances
IReadOnlyList<AssetBalance> balances = await client.Account.GetBalancesAsync();
foreach (var b in balances)
    Console.WriteLine($"{b.Asset}: free={b.Free:F8}, locked={b.Locked:F8}, total={b.Total:F8}");

// Single asset
AssetBalance btcBalance = await client.Account.GetBalanceAsync(Asset.Btc);
Console.WriteLine($"BTC total: {btcBalance.Total:F8}");
```

`AssetBalance.Asset` is a typed `Asset` value (not a raw string).

### Trade history

```csharp
IReadOnlyList<Trade> myTrades = await client.Account.GetTradeHistoryAsync(
    new Symbol(Asset.Btc, Asset.Usdt),
    limit: 100,
    startTime: DateTimeOffset.UtcNow.AddDays(-30));
```

---

## Dependency Injection (ASP.NET Core / hosted services)

### Register all exchanges at once

```csharp
// Program.cs
builder.Services.AddCryptoExchanges(opt =>
{
    opt.BinanceApiKey    = builder.Configuration["Binance:ApiKey"];
    opt.BinanceSecretKey = builder.Configuration["Binance:SecretKey"];
    opt.BybitApiKey      = builder.Configuration["Bybit:ApiKey"];
    opt.BybitSecretKey   = builder.Configuration["Bybit:SecretKey"];
    opt.OkxApiKey        = builder.Configuration["Okx:ApiKey"];
    opt.OkxSecretKey     = builder.Configuration["Okx:SecretKey"];
    opt.OkxPassphrase    = builder.Configuration["Okx:Passphrase"];
    opt.BitgetApiKey     = builder.Configuration["Bitget:ApiKey"];
    opt.BitgetSecretKey  = builder.Configuration["Bitget:SecretKey"];
    opt.BitgetPassphrase = builder.Configuration["Bitget:Passphrase"];
});
```

### Register a single exchange

```csharp
// AddBinanceExchange / AddBybitExchange / AddOkxExchange / AddBitgetExchange
// live in the per-exchange package, not DependencyInjection
builder.Services.AddBinanceExchange(opt =>
{
    opt.ApiKey    = builder.Configuration["Binance:ApiKey"];
    opt.SecretKey = builder.Configuration["Binance:SecretKey"];
});
```

### Resolve via factory

```csharp
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Enums;

app.MapGet("/btc-price", async (IExchangeClientFactory factory) =>
{
    IExchangeClient ex = factory.GetClient(ExchangeId.Binance);
    return await ex.MarketData.GetPriceAsync(new Symbol(Asset.Btc, Asset.Usdt));
});
```

`IExchangeClientFactory.Available` lists registered exchange IDs. `TryGet` is the non-throwing
variant.

### Resolve a specific exchange directly (keyed DI)

```csharp
using Microsoft.AspNetCore.Mvc;

app.MapGet("/eth-price", async ([FromKeyedServices(ExchangeId.Binance)] IExchangeClient ex) =>
    await ex.MarketData.GetPriceAsync(new Symbol(Asset.Eth, Asset.Usdt)));
```

---

## Error Handling

All exchange failures surface as typed exceptions derived from `ExchangeException`:

```
ExchangeException
├── ExchangeApiException          — 4xx exchange error response
│   ├── AuthenticationException   — bad key / signature / IP restriction
│   ├── RateLimitExceededException — 429 / weight-limit breached
│   ├── InvalidOrderException     — bad order parameters
│   └── InsufficientBalanceException — insufficient funds
└── ExchangeConnectivityException — network/transport failure
```

Catch by specificity:

```csharp
using CryptoExchanges.Net.Core.Exceptions;

try
{
    var order = await client.Trading.PlaceOrderAsync(new PlaceOrderRequest
    {
        Symbol   = new Symbol(Asset.Btc, Asset.Usdt),
        Side     = OrderSide.Buy,
        Type     = OrderType.Limit,
        Quantity = 0.001m,
        Price    = 50_000m
    });
}
catch (AuthenticationException ex)
{
    Console.Error.WriteLine($"Authentication failed: {ex.Message}");
}
catch (RateLimitExceededException ex)
{
    Console.Error.WriteLine($"Rate limit hit: {ex.Message}");
}
catch (InvalidOrderException ex)
{
    Console.Error.WriteLine($"Bad order parameters: {ex.Message}");
}
catch (InsufficientBalanceException ex)
{
    Console.Error.WriteLine($"Insufficient funds: {ex.Message}");
}
catch (ExchangeApiException ex)
{
    // Catch-all for other 4xx errors
    Console.Error.WriteLine($"API error (code {ex.Code}): {ex.Message}");
}
catch (ExchangeConnectivityException ex)
{
    Console.Error.WriteLine($"Network error: {ex.Message}");
}
```

`ExchangeApiException` exposes an optional integer `Code` (the exchange's raw numeric error code)
and the raw response body for diagnostics.

---

## Disposal

`IExchangeClient` implements `IAsyncDisposable`. When constructed directly (factory-free), dispose
the client to release the underlying `HttpClient`:

```csharp
await using var client = BinanceExchangeClient.CreateFromEnvironment();
// ...use client...
// Disposed automatically at end of scope
```

When registered via DI, the container owns the lifetime — do not dispose the injected instance.

---

## Further reading

- [Getting Started](getting-started.md) — install and first calls
- [Architecture](architecture.md) — how the layers fit together
- [Exchanges](exchanges.md) — per-exchange packages, credentials, and status
- [MCP server](mcp-server.md) — AI-agent read-only access via the Model Context Protocol
