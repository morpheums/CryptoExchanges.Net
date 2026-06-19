# Architecture

CryptoExchanges.Net is a multi-project .NET 10 class library organized in four layers.
This page describes the shipped design only.

---

## Layer diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                      Consumer application                        │
└────────────────────────────┬────────────────────────────────────┘
                             │ depends on Core interfaces only
┌────────────────────────────▼────────────────────────────────────┐
│  CryptoExchanges.Net.Core                                        │
│  Interfaces · Models · Enums · Exceptions · ISymbolMapper        │
│  Zero production dependencies (only ME.Logging.Abstractions,     │
│  ME.DI.Abstractions)                                             │
└────────────┬───────────────────────────────────────────────────-┘
             │ depends on Core
┌────────────▼──────────────────────────────────────────────────--┐
│  CryptoExchanges.Net.Http                                        │
│  Shared resilience pipeline (Polly retry + timeout)              │
│  DelegatingHandler chain — signing, rate-limit, error-translate  │
└────────────┬────────────────────────────────────────────────────┘
             │ depends on Core + Http
┌────────────▼────────────────────────────────────────────────────┐
│  Per-exchange packages                                           │
│  Binance · Bybit · OKX · Bitget                                  │
│  Exchange-specific HTTP client, services, DTO mapping, signing   │
└────────────┬────────────────────────────────────────────────────┘
             │ depends on Core + per-exchange packages
┌────────────▼────────────────────────────────────────────────────┐
│  CryptoExchanges.Net.DependencyInjection                         │
│  AddCryptoExchanges() · AddBinanceExchange() · …                 │
│  IExchangeClientFactory · keyed-singleton registration           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Core (`CryptoExchanges.Net.Core`)

The foundation layer. Contains **all public contracts** and **all canonical domain models**.
No exchange implementation detail leaks here.

### Interfaces

| Interface | Purpose |
|-----------|---------|
| `IExchangeClient` | Unified entry point. Exposes `MarketData`, `Trading`, `Account`, and `PingAsync`. |
| `IMarketDataService` | Public-data operations: price, tickers, order book, candles, recent trades, exchange info, symbol validation. |
| `ITradingService` | Order lifecycle: place, cancel, query open/historical orders. |
| `IAccountService` | Account state: balances, single asset balance, trade history. |
| `IExchangeClientFactory` | Resolves a registered `IExchangeClient` by `ExchangeId`. |
| `ISymbolMapper` | Translates between the typed `Symbol` domain model and each exchange's wire format. |

### Domain models

All methods return these canonical types regardless of which exchange backs them.
Consumer code depends on models in `Core.Models`, never on exchange-specific DTOs.

| Model | Fields (key) |
|-------|-------------|
| `Symbol` | `Base` (Asset), `Quote` (Asset) |
| `Asset` | `Ticker` (string, normalized upper-case) — open-ended, with curated constants |
| `Ticker` | `Symbol`, `LastPrice`, `HighPrice`, `LowPrice`, `Volume`, `PriceChangePercent` |
| `OrderBook` | `LastUpdateId`, `Bids` (list of `OrderBookEntry`), `Asks` |
| `OrderBookEntry` | `Price`, `Quantity` |
| `Candlestick` | `OpenTime`, `Open`, `High`, `Low`, `Close`, `Volume`, `CloseTime` |
| `Trade` | `TradeId`, `Price`, `Quantity`, `Timestamp`, `IsBuyerMaker` |
| `Order` | `OrderId`, `Symbol`, `Side`, `Type`, `Status`, `Price`, `Quantity`, `FilledQuantity`, `CreatedAt` |
| `AssetBalance` | `Asset`, `Free`, `Locked`, `Total` |
| `ExchangeInfo` | `ExchangeName`, `Symbols` (list of `SymbolInfo`), `RateLimits` |

### Exception hierarchy

```
ExchangeException                     (abstract)
├── ExchangeApiException              — 4xx exchange error response
│   ├── AuthenticationException       — bad key, signature, or IP restriction
│   ├── RateLimitExceededException    — 429 / weight limit
│   ├── InvalidOrderException         — invalid order parameters
│   └── InsufficientBalanceException  — insufficient funds
└── ExchangeConnectivityException     — network/transport failure
```

---

## Http (`CryptoExchanges.Net.Http`)

Shared, exchange-agnostic HTTP infrastructure. Exchange packages build their resilient
`HttpClient` by composing this layer.

### Handler chain (innermost → outermost)

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

The handler chain is assembled by `HttpClientPipelineBuilder.Build()` and is shared by both the
container-free factory path and the DI registration path.

---

## Per-exchange packages

Each exchange follows the same internal structure:

| Component | Role |
|-----------|------|
| `*ExchangeClient` | Public entry point. Factory method `Create(*Options)` and `CreateFromEnvironment()`. Implements `IExchangeClient`. |
| `*Options` | Configuration: `ApiKey`, `SecretKey`, and (for OKX/Bitget) `Passphrase`. |
| `*MarketDataService` | `IMarketDataService` implementation. |
| `*TradingService` | `ITradingService` implementation. |
| `*AccountService` | `IAccountService` implementation. |
| `*HttpClient` | Internal typed HTTP client (`GetAsync<T>`, `PostAsync<T>`, `DeleteAsync<T>`). |
| `*SignatureService` | HMAC-SHA256 signing (Binance/Bybit) or HMAC-SHA256 with timestamp+passphrase (OKX/Bitget). |
| `*SigningHandler` | `DelegatingHandler` that attaches the signature to each outbound request. |
| `*ErrorTranslator` | `IExchangeErrorTranslator` impl — maps the exchange's error JSON to typed exceptions. |
| `*MappingProfiles` | DeltaMapper profile: exchange DTOs → canonical Core.Models. |
| `*SymbolFormat` | `SymbolFormat` config (delimiter, casing) consumed by the shared `SymbolMapper`. |
| `*ClientComposer` | Single internal composition root; called by both factory and DI paths. |

---

## Symbol mapping (`ISymbolMapper`)

`Symbol` is a typed domain model with no wire format. Conversion to and from exchange-specific
strings (e.g. `"BTCUSDT"` on Binance, `"BTC-USDT"` on OKX) is the exclusive job of
`ISymbolMapper`, parameterized by a per-exchange `SymbolFormat`:

```
new Symbol(Asset.Btc, Asset.Usdt)
        │
        │  ISymbolMapper.ToWire(symbol)
        ▼
"BTCUSDT"   (Binance — delimiter-less, upper-case)
"BTCUSDT"   (Bybit  — delimiter-less, upper-case)
"BTC-USDT"  (OKX    — hyphen delimiter, upper-case)
"BTCUSDT"   (Bitget — delimiter-less, upper-case)
```

`ISymbolMapper.FromWire(string)` is the inverse path, used during response mapping.

---

## DTO mapping (`DeltaMapper`)

Exchange REST responses are deserialized into exchange-specific DTO types and then mapped to
canonical `Core.Models` via [DeltaMapper](https://github.com/morpheums/DeltaMapper). Each
exchange package declares a `*MappingProfiles` class (e.g. `BinanceMappingProfiles`) that
registers all DTO → model conversions.

This keeps exchange-specific parsing (string-encoded decimals, custom enum labels, Unix
milliseconds) isolated from the domain layer.

---

## Signing pipeline

Signed endpoints require a timestamp and an HMAC-SHA256 signature appended to the query string
or request body.

1. The service method marks the outbound `HttpRequestMessage` as "needs signing".
2. The per-exchange `*SigningHandler` intercepts it, reads the current UTC timestamp (adjusted by
   any clock-skew offset), computes the signature, and injects the required headers or parameters.
3. The signing handler is inserted between the Polly retry layer and the error-translation layer so
   the signature is freshly computed on every retry attempt.

OKX and Bitget additionally include the passphrase in the signed headers.

---

## Dependency injection

`AddCryptoExchanges()` (in `CryptoExchanges.Net.DependencyInjection`) is a convenience aggregator
that delegates to each exchange's own `Add*Exchange()` method (defined in the exchange's package,
per ADR-001). Each exchange is registered as a keyed singleton under its `ExchangeId`.

`IExchangeClientFactory` resolves clients by `ExchangeId` without requiring consumer code to
depend on keyed-service infrastructure directly.

---

## Further reading

- [Getting Started](getting-started.md) — install and first calls
- [Library Usage](library-usage.md) — full code examples
- [Exchanges](exchanges.md) — per-exchange support status and packages
