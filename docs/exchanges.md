# Exchanges

CryptoExchanges.Net supports four exchanges today, with three more on the way.
All supported exchanges expose the same `IExchangeClient` interface (REST, spot).

---

## Supported exchanges

### Binance

<img src="assets/exchanges/binance.svg" alt="Binance" width="32" height="32" />

| | |
|---|---|
| **Package** | `CryptoExchanges.Net.Binance` |
| **Client class** | `BinanceExchangeClient` |
| **Credentials** | `ApiKey`, `SecretKey` |
| **Env vars** | `BINANCE_API_KEY`, `BINANCE_SECRET_KEY` |
| **Signing** | HMAC-SHA256 |
| **Endpoints** | REST v3 (spot) |

```bash
dotnet add package CryptoExchanges.Net.Binance
```

```csharp
using CryptoExchanges.Net.Binance;

// From explicit options
await using var client = BinanceExchangeClient.Create(new BinanceOptions
{
    ApiKey    = "...",
    SecretKey = "..."
});

// From environment variables (BINANCE_API_KEY / BINANCE_SECRET_KEY)
await using var client = BinanceExchangeClient.CreateFromEnvironment();
```

DI registration:

```csharp
services.AddBinanceExchange(opt =>
{
    opt.ApiKey    = configuration["Binance:ApiKey"];
    opt.SecretKey = configuration["Binance:SecretKey"];
});
```

---

### Bybit

<img src="assets/exchanges/bybit.svg" alt="Bybit" width="32" height="32" />

| | |
|---|---|
| **Package** | `CryptoExchanges.Net.Bybit` |
| **Client class** | `BybitExchangeClient` |
| **Credentials** | `ApiKey`, `SecretKey` |
| **Env vars** | `BYBIT_API_KEY`, `BYBIT_SECRET_KEY` |
| **Signing** | HMAC-SHA256 |
| **Endpoints** | REST v5 (spot) |

```bash
dotnet add package CryptoExchanges.Net.Bybit
```

```csharp
using CryptoExchanges.Net.Bybit;

await using var client = BybitExchangeClient.Create(new BybitOptions
{
    ApiKey    = "...",
    SecretKey = "..."
});

// From environment variables (BYBIT_API_KEY / BYBIT_SECRET_KEY)
await using var client = BybitExchangeClient.CreateFromEnvironment();
```

DI registration:

```csharp
services.AddBybitExchange(opt =>
{
    opt.ApiKey    = configuration["Bybit:ApiKey"];
    opt.SecretKey = configuration["Bybit:SecretKey"];
});
```

---

### OKX

<img src="assets/exchanges/okx.svg" alt="OKX" width="32" height="32" />

| | |
|---|---|
| **Package** | `CryptoExchanges.Net.Okx` |
| **Client class** | `OkxExchangeClient` |
| **Credentials** | `ApiKey`, `SecretKey`, **`Passphrase`** |
| **Env vars** | `OKX_API_KEY`, `OKX_SECRET_KEY`, `OKX_PASSPHRASE` |
| **Signing** | HMAC-SHA256 + ISO-8601 timestamp + passphrase header |
| **Endpoints** | REST v5 (spot) |

> **Passphrase required.** OKX API keys have a mandatory passphrase set at key-creation time.
> Supply it via the `Passphrase` option or `OKX_PASSPHRASE` environment variable.

```bash
dotnet add package CryptoExchanges.Net.Okx
```

```csharp
using CryptoExchanges.Net.Okx;

await using var client = OkxExchangeClient.Create(new OkxOptions
{
    ApiKey     = "...",
    SecretKey  = "...",
    Passphrase = "..."
});

// From environment variables (OKX_API_KEY / OKX_SECRET_KEY / OKX_PASSPHRASE)
await using var client = OkxExchangeClient.CreateFromEnvironment();
```

DI registration:

```csharp
services.AddOkxExchange(opt =>
{
    opt.ApiKey     = configuration["Okx:ApiKey"];
    opt.SecretKey  = configuration["Okx:SecretKey"];
    opt.Passphrase = configuration["Okx:Passphrase"];
});
```

---

### Bitget

<img src="assets/exchanges/bitget.svg" alt="Bitget" width="32" height="32" />

| | |
|---|---|
| **Package** | `CryptoExchanges.Net.Bitget` |
| **Client class** | `BitgetExchangeClient` |
| **Credentials** | `ApiKey`, `SecretKey`, **`Passphrase`** |
| **Env vars** | `BITGET_API_KEY`, `BITGET_SECRET_KEY`, `BITGET_PASSPHRASE` |
| **Signing** | HMAC-SHA256 + Unix timestamp + passphrase header |
| **Endpoints** | REST v2 (spot) |

> **Passphrase required.** Bitget API keys have a mandatory passphrase set at key-creation time.
> Supply it via the `Passphrase` option or `BITGET_PASSPHRASE` environment variable.

```bash
dotnet add package CryptoExchanges.Net.Bitget
```

```csharp
using CryptoExchanges.Net.Bitget;

await using var client = BitgetExchangeClient.Create(new BitgetOptions
{
    ApiKey     = "...",
    SecretKey  = "...",
    Passphrase = "..."
});

// From environment variables (BITGET_API_KEY / BITGET_SECRET_KEY / BITGET_PASSPHRASE)
await using var client = BitgetExchangeClient.CreateFromEnvironment();
```

DI registration:

```csharp
services.AddBitgetExchange(opt =>
{
    opt.ApiKey     = configuration["Bitget:ApiKey"];
    opt.SecretKey  = configuration["Bitget:SecretKey"];
    opt.Passphrase = configuration["Bitget:Passphrase"];
});
```

---

## Register all exchanges at once

`CryptoExchanges.Net.DependencyInjection` provides a single `AddCryptoExchanges()` call that
registers all four exchanges:

```csharp
using CryptoExchanges.Net.DependencyInjection;

services.AddCryptoExchanges(opt =>
{
    opt.BinanceApiKey    = configuration["Binance:ApiKey"];
    opt.BinanceSecretKey = configuration["Binance:SecretKey"];

    opt.BybitApiKey      = configuration["Bybit:ApiKey"];
    opt.BybitSecretKey   = configuration["Bybit:SecretKey"];

    opt.OkxApiKey        = configuration["Okx:ApiKey"];
    opt.OkxSecretKey     = configuration["Okx:SecretKey"];
    opt.OkxPassphrase    = configuration["Okx:Passphrase"];

    opt.BitgetApiKey     = configuration["Bitget:ApiKey"];
    opt.BitgetSecretKey  = configuration["Bitget:SecretKey"];
    opt.BitgetPassphrase = configuration["Bitget:Passphrase"];
});
```

Resolve by exchange ID at runtime:

```csharp
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Enums;

IExchangeClient binance = factory.GetClient(ExchangeId.Binance);
IExchangeClient bybit   = factory.GetClient(ExchangeId.Bybit);
IExchangeClient okx     = factory.GetClient(ExchangeId.Okx);
IExchangeClient bitget  = factory.GetClient(ExchangeId.Bitget);
```

---

## Coming soon

| Exchange | Package |
|----------|---------|
| <img src="assets/exchanges/coinbase.svg" alt="Coinbase" width="20" height="20" /> Coinbase | `CryptoExchanges.Net.Coinbase` |
| <img src="assets/exchanges/kraken.svg" alt="Kraken" width="20" height="20" /> Kraken | `CryptoExchanges.Net.Kraken` |
| <img src="assets/exchanges/kucoin.svg" alt="KuCoin" width="20" height="20" /> KuCoin | `CryptoExchanges.Net.KuCoin` |

These exchange IDs are present in the `ExchangeId` enum but are not yet implemented.

---

## Further reading

- [Getting Started](getting-started.md) — install and credential setup
- [Library Usage](library-usage.md) — code examples for every operation
- [Architecture](architecture.md) — how the layers fit together
