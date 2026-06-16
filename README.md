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
var price = await exchange.MarketData.GetPriceAsync("BTCUSDT");
var order = await exchange.Trading.PlaceOrderAsync(new PlaceOrderRequest
{
    Symbol = "BTCUSDT", Side = OrderSide.Buy, Type = OrderType.Limit,
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

var btcPrice = await exchange.MarketData.GetPriceAsync("BTCUSDT");
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
    await ex.MarketData.GetPriceAsync("BTCUSDT"));
```

## Supported Operations

### Market Data
```csharp
// Tickers
var tickers = await exchange.MarketData.GetTickersAsync("ETHUSDT");

// Order book
var ob = await exchange.MarketData.GetOrderBookAsync("BTCUSDT", depth: 50);

// Candles
var candles = await exchange.MarketData.GetCandlesticksAsync(
    "BTCUSDT", KlineInterval.OneHour, limit: 24);

// Latest price
var price = await exchange.MarketData.GetPriceAsync("BTCUSDT");

// Exchange info (trading rules, symbol details)
var info = await exchange.MarketData.GetExchangeInfoAsync();
```

### Trading
```csharp
// Place order
var order = await exchange.Trading.PlaceOrderAsync(new PlaceOrderRequest
{
    Symbol = "BTCUSDT", Side = OrderSide.Buy, Type = OrderType.Limit,
    Quantity = 0.001m, Price = 50000m
});

// Cancel order
await exchange.Trading.CancelOrderAsync("BTCUSDT", order.OrderId);

// Get open orders
var open = await exchange.Trading.GetOpenOrdersAsync();
```

### Account
```csharp
// Balances
var balances = await exchange.Account.GetBalancesAsync();

// Trade history
var trades = await exchange.Account.GetTradeHistoryAsync("BTCUSDT", limit: 100);
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
