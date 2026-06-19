# Getting Started

CryptoExchanges.Net provides a unified .NET 10 interface to multiple cryptocurrency exchanges.
This guide covers installation, credential configuration, and your first API calls.

---

## Prerequisites

- .NET 10.0 SDK or later
- API keys from your chosen exchange (market-data endpoints are public; trading and account
  endpoints require authentication)

---

## Install

Each exchange ships as its own NuGet package. Install only the exchange(s) you need:

```bash
# Binance
dotnet add package CryptoExchanges.Net.Binance

# Bybit
dotnet add package CryptoExchanges.Net.Bybit

# OKX
dotnet add package CryptoExchanges.Net.Okx

# Bitget
dotnet add package CryptoExchanges.Net.Bitget
```

To register all four exchanges in one call (ASP.NET Core / hosted services):

```bash
dotnet add package CryptoExchanges.Net.DependencyInjection
```

---

## Configure credentials

### Environment variables (recommended)

Set exchange credentials as environment variables before running your application:

| Exchange | Variables |
|----------|-----------|
| Binance  | `BINANCE_API_KEY`, `BINANCE_SECRET_KEY` |
| Bybit    | `BYBIT_API_KEY`, `BYBIT_SECRET_KEY` |
| OKX      | `OKX_API_KEY`, `OKX_SECRET_KEY`, `OKX_PASSPHRASE` |
| Bitget   | `BITGET_API_KEY`, `BITGET_SECRET_KEY`, `BITGET_PASSPHRASE` |

OKX and Bitget require a **passphrase** — a third credential set in the exchange's API management
console — in addition to the key and secret.

### Direct options (container-free)

Pass credentials explicitly when constructing the client:

```csharp
using CryptoExchanges.Net.Binance;

await using var client = BinanceExchangeClient.Create(new BinanceOptions
{
    ApiKey    = "your-api-key",
    SecretKey = "your-secret-key"
});
```

For OKX or Bitget, add the passphrase:

```csharp
using CryptoExchanges.Net.Okx;

await using var client = OkxExchangeClient.Create(new OkxOptions
{
    ApiKey     = "your-api-key",
    SecretKey  = "your-secret-key",
    Passphrase = "your-passphrase"
});
```

---

## Create a client

### From environment variables

The quickest path — reads credentials automatically from the environment:

```csharp
using CryptoExchanges.Net.Binance;
using CryptoExchanges.Net.Core.Models;

await using var client = BinanceExchangeClient.CreateFromEnvironment();
Console.WriteLine($"Exchange: {client.ExchangeId}");
```

### From explicit options

```csharp
await using var client = BinanceExchangeClient.Create(new BinanceOptions
{
    ApiKey    = Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? "",
    SecretKey = Environment.GetEnvironmentVariable("BINANCE_SECRET_KEY") ?? ""
});
```

---

## First calls

### Ping the exchange

```csharp
bool alive = await client.PingAsync();
Console.WriteLine($"Binance reachable: {alive}");
```

### Get a price

Symbols are typed — construct them with `Asset` constants rather than raw strings:

```csharp
using CryptoExchanges.Net.Core.Models;

var btcUsdt = new Symbol(Asset.Btc, Asset.Usdt);
decimal price = await client.MarketData.GetPriceAsync(btcUsdt);
Console.WriteLine($"BTC/USDT: ${price:N2}");
```

### Get a 24-hour ticker

```csharp
var ethUsdt = new Symbol(Asset.Eth, Asset.Usdt);
var tickers = await client.MarketData.GetTickersAsync(ethUsdt);
foreach (var t in tickers)
    Console.WriteLine($"{t.Symbol}: last=${t.LastPrice:N2}, 24h change={t.PriceChangePercent:F2}%");
```

---

## Next steps

- [Library Usage](library-usage.md) — fuller examples: market data, trading, account, DI,
  and error handling
- [Exchanges](exchanges.md) — per-exchange packages, credential requirements, and support status
- [Architecture](architecture.md) — how the layers fit together
