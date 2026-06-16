# CryptoExchanges.Net

> **A unified .NET SDK for cryptocurrency exchanges — one interface, every exchange.**
>
> Built for .NET 10 with Clean Architecture, SOLID principles, and modern C# features.

[![NuGet](https://img.shields.io/badge/nuget-v0.1.0--preview.1-blue)](https://www.nuget.org/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

---

## Philosophy

Every crypto exchange has a different API. CryptoExchanges.Net gives you **one unified interface** across Binance, Coinbase, Bybit, and more. Write your trading logic once — run it anywhere.

```csharp
// Same code works on any exchange
IExchangeClient exchange = BinanceExchangeClient.Create(new BinanceOptions
{
    ApiKey = apiKey,
    SecretKey = secretKey
});
var btcusdt = new Symbol(Asset.Btc, Asset.Usdt);
var price = await exchange.MarketData.GetPriceAsync(btcusdt);
var order = await exchange.Trading.PlaceOrderAsync(new PlaceOrderRequest
{
    Symbol = btcusdt, Side = OrderSide.Buy, Type = OrderType.Limit,
    Quantity = 0.001m, Price = price * 0.99m
});
```

## Architecture

```
┌──────────────────────────────────────────────────┐
│                  Your Application                 │
├──────────────────────────────────────────────────┤
│  IExchangeClient  (unified interface)             │
│  ├── IMarketDataService  (tickers, candles, OB)  │
│  ├── ITradingService     (orders, positions)     │
│  └── IAccountService     (balances, history)     │
├──────────────────────────────────────────────────┤
│  Binance  │  Coinbase  │  Bybit  │  ...          │  ← exchange implementations
└──────────────────────────────────────────────────┘
```

### Design Principles

- **SOLID** — Interface segregation: market data, trading, and account are separate contracts
- **Clean Architecture** — Core abstractions have zero dependencies on exchange implementations
- **Modern .NET** — Primary constructors, required properties, records, `Span<T>`, collection expressions
- **Testable** — Every service behind an interface, `IHttpClientFactory` for mockable HTTP
- **DI-first** — First-class `IServiceCollection` integration with keyed services for multi-exchange

## Quick Start

### 1. Install

```bash
dotnet add package CryptoExchanges.Net.Binance
```

### 2. Use directly

```csharp
await using var exchange = BinanceExchangeClient.Create(new BinanceOptions
{
    ApiKey = "your-api-key",
    SecretKey = "your-secret-key"
});

var btcPrice = await exchange.MarketData.GetPriceAsync(new Symbol(Asset.Btc, Asset.Usdt));
Console.WriteLine($"BTC: ${btcPrice}");
```

### 3. Use with ASP.NET Core DI

```csharp
// Program.cs
builder.Services.AddCryptoExchanges(cfg =>
{
    cfg.BinanceApiKey = builder.Configuration["Binance:ApiKey"];
    cfg.BinanceSecretKey = builder.Configuration["Binance:SecretKey"];
});

// appsettings.json
{
  "CryptoExchanges": {
    "Binance": {
      "ApiKey": "your-api-key",
      "SecretKey": "your-secret-key"
    }
  }
}

// Inject anywhere
app.MapGet("/btc", async (IExchangeClient ex) =>
    await ex.MarketData.GetPriceAsync(new Symbol(Asset.Btc, Asset.Usdt)));
```

## Supported Operations

### Symbols and Assets

Symbols are typed — `new Symbol(Asset.Btc, Asset.Usdt)` rather than the string `"BTCUSDT"`.
Use the built-in constants for common assets, or `Asset.Of("...")` for long-tail tickers:

```csharp
var btcusdt = new Symbol(Asset.Btc, Asset.Usdt);
var pepeusdt = new Symbol(Asset.Of("PEPE"), Asset.Usdt); // long-tail asset

// Opt-in exchange validation (lazily fetches + caches exchangeInfo)
bool ok = await exchange.MarketData.IsSupportedAsync(btcusdt);
Symbol? canonical = await exchange.MarketData.ResolveSymbolAsync(btcusdt);
```

### Market Data
```csharp
var ethusdt = new Symbol(Asset.Eth, Asset.Usdt);
var btcusdt = new Symbol(Asset.Btc, Asset.Usdt);

// Tickers
var tickers = await exchange.MarketData.GetTickersAsync(ethusdt);

// Order book
var ob = await exchange.MarketData.GetOrderBookAsync(btcusdt, depth: 50);

// Candles
var candles = await exchange.MarketData.GetCandlesticksAsync(
    btcusdt, KlineInterval.OneHour, limit: 24);

// Latest price
var price = await exchange.MarketData.GetPriceAsync(btcusdt);

// Exchange info (trading rules, symbol details)
var info = await exchange.MarketData.GetExchangeInfoAsync();
```

### Trading
```csharp
// Place order
var btcusdt = new Symbol(Asset.Btc, Asset.Usdt);
var order = await exchange.Trading.PlaceOrderAsync(new PlaceOrderRequest
{
    Symbol = btcusdt, Side = OrderSide.Buy, Type = OrderType.Limit,
    Quantity = 0.001m, Price = 50000m
});

// Cancel order
await exchange.Trading.CancelOrderAsync(btcusdt, order.OrderId);

// Get open orders
var open = await exchange.Trading.GetOpenOrdersAsync();
```

### Account
```csharp
// Balances — balance.Asset is a typed Asset (e.g. Asset.Btc)
var balances = await exchange.Account.GetBalancesAsync();
foreach (var balance in balances)
    Console.WriteLine($"{balance.Asset}: {balance.Total}");

// Balance for a single asset
var btc = await exchange.Account.GetBalanceAsync(Asset.Btc);

// Trade history
var trades = await exchange.Account.GetTradeHistoryAsync(
    new Symbol(Asset.Btc, Asset.Usdt), limit: 100);
```

## Supported Exchanges

| Exchange  | Status      | Package                           |
|-----------|-------------|-----------------------------------|
| Binance   | ✅ Complete | `CryptoExchanges.Net.Binance`    |
| Coinbase  | 🔜 Planned  | `CryptoExchanges.Net.Coinbase`   |
| Bybit     | 🔜 Planned  | `CryptoExchanges.Net.Bybit`      |
| Kraken    | 🔜 Planned  | `CryptoExchanges.Net.Kraken`     |
| OKX       | 🔜 Planned  | `CryptoExchanges.Net.Okx`        |

## Project Structure

```
CryptoExchanges.Net/
├── src/
│   ├── CryptoExchanges.Net.Core/              # Interfaces, models, enums
│   ├── CryptoExchanges.Net.Binance/           # Binance implementation
│   ├── CryptoExchanges.Net.Coinbase/          # [planned]
│   ├── CryptoExchanges.Net.Bybit/             # [planned]
│   └── CryptoExchanges.Net.DependencyInjection/  # DI extensions
├── tests/
│   └── CryptoExchanges.Net.Core.Tests/
├── samples/
│   └── BasicUsage/
└── docs/                                      # [planned]
```

## Roadmap

- [x] Core abstractions (interfaces, models, enums)
- [x] Binance REST implementation
- [x] Dependency injection integration
- [x] Keyed services for multi-exchange
- [ ] Coinbase implementation
- [ ] Bybit implementation
- [ ] WebSocket streaming support
- [ ] MCP server wrapper
- [ ] Rate limiting middleware
- [ ] Caching layer
- [ ] Audit trail (Vigilex DNA)

## Building

```bash
dotnet build
dotnet test
```

Requires .NET 10.0 SDK.

## License

Apache 2.0 — see [LICENSE](LICENSE).

---

Built by [Morpheums](https://github.com/morpheums). Star the [Binance API client](https://github.com/morpheums/Binance.API.Csharp.Client) that started it all.
