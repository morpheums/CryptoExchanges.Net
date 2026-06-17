# Architecture Map

## Entry Points
- `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs` — public `Create(BinanceOptions)` factory method (container-free path)
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs` — `AddBinanceExchange()` / `AddCryptoExchanges()` DI entry points
- `samples/BasicUsage/Program.cs` — sample usage entry point

## Module Boundaries

### CryptoExchanges.Net.Core
- Path: `src/CryptoExchanges.Net.Core/`
- Purpose: Zero-dependency abstraction layer. Defines all public contracts (interfaces), domain models (records/structs), enums, and exceptions. No exchange-specific code. Zero transitive dependencies (only ME.Logging.Abstractions and ME.DI.Abstractions).
- Key files:
  - `Interfaces/IExchangeClient.cs` — `IExchangeClient`, `IMarketDataService`, `ITradingService`, `IAccountService`, `PlaceOrderRequest`
  - `Interfaces/ISymbolMapper.cs` — wire↔domain symbol translation contract
  - `Interfaces/IExchangeErrorTranslator.cs` — error translation contract
  - `Interfaces/IRateLimitGate.cs` — rate limit gate contract
  - `Models/Models.cs` — `Symbol`, `Ticker`, `OrderBook`, `Candlestick`, `Trade`, `Order`, `AssetBalance`, `ExchangeInfo`
  - `Models/Asset.cs` — `Asset` value type (open-ended ticker, curated constants)
  - `Models/SymbolFormat.cs` — `SymbolFormat`, `SymbolCasing` for per-exchange wire format config
  - `Enums/Enums.cs` — `OrderSide`, `OrderType`, `OrderStatus`, `KlineInterval`, `ExchangeId`, etc.
  - `Exceptions/ExchangeExceptions.cs` — typed exception hierarchy (`ExchangeException` → `ExchangeApiException`, `RateLimitExceededException`, `AuthenticationException`, `InvalidOrderException`, `InsufficientBalanceException`, `ExchangeConnectivityException`)
  - `SymbolMapper.cs` — generic `ISymbolMapper` implementation (parameterized by `SymbolFormat`)
  - `Resilience/ResilienceOptions.cs` — shared `ResilienceOptions` value type

### CryptoExchanges.Net.Http
- Path: `src/CryptoExchanges.Net.Http/`
- Purpose: Shared, exchange-agnostic HTTP resilience pipeline. Provides composable `DelegatingHandler` chain and a factory-less pipeline builder. Depends only on Core.
- Key files:
  - `HttpClientPipelineBuilder.cs` — `Build()` static factory composing the full handler chain
  - `ExchangeResiliencePipeline.cs` — `Configure()` (Polly retry+timeout) + `TransientExhaustionHandler`
  - `ErrorTranslationHandler.cs` — innermost handler; delegates to `IExchangeErrorTranslator` on 4xx
  - `RateLimitThrottleHandler.cs` — outermost handler; gates on `IRateLimitGate`
  - `ReactiveRateLimitGate.cs` — reactive backpressure gate (observes Retry-After/weight headers)
  - `PassThroughHandler.cs` — no-op finalizer for key-only (unsigned) clients
  - `RetryAfterReader.cs` — extracts `Retry-After` header as `TimeSpan`
  - `ResilientHttpClientServiceCollectionExtensions.cs` — `ApplyResiliencePipeline()` DI helper

### CryptoExchanges.Net.Binance
- Path: `src/CryptoExchanges.Net.Binance/`
- Purpose: Binance REST implementation. Depends on Core + Http. All internal types hidden; only `BinanceExchangeClient` and `BinanceOptions` are public API.
- Key files:
  - `BinanceExchangeClient.cs` — public entry point; `Create()` / `CreateFromEnvironment()` factory methods; `SyncServerTimeAsync()`
  - `BinanceHttpClient.cs` — internal HTTP wrapper (`GetAsync<T>`, `PostAsync<T>`, `DeleteAsync<T>`)
  - `IBinanceHttpClient.cs` — internal interface (mockable in integration tests)
  - `BinanceSymbolFormat.cs` — `BinanceSymbolFormat.Instance` (delimiter-less, upper-case)
  - `Auth/BinanceSignatureService.cs` — HMAC-SHA256 signing
  - `Resilience/BinanceSigningHandler.cs` — per-request signing delegating handler
  - `Resilience/BinanceSigningRequest.cs` — request marker (prevents double-signing on retry)
  - `Resilience/BinanceErrorTranslator.cs` — maps Binance `{code,msg}` JSON to typed exceptions
  - `Resilience/BinanceTimeSync.cs` — clock-skew offset computation
  - `Mapping/BinanceMappingProfiles.cs` — DeltaMapper profile: Binance DTOs → domain models
  - `Internal/BinanceClientComposer.cs` — single composition root (factory-less + DI paths)
  - `Internal/BinanceValueParsers.cs` — parse helpers for Binance string-encoded decimals/enums
  - `Internal/BinanceRequestValidation.cs` — pre-flight request validation
  - `Services/BinanceMarketDataService.cs` — `IMarketDataService` implementation
  - `Services/BinanceTradingService.cs` — `ITradingService` implementation
  - `Services/BinanceAccountService.cs` — `IAccountService` implementation
  - `GlobalUsings.cs` — global using directives

### CryptoExchanges.Net.DependencyInjection
- Path: `src/CryptoExchanges.Net.DependencyInjection/`
- Purpose: `IServiceCollection` extension methods for registering exchanges. Provides `AddBinanceExchange()`, `AddCryptoExchanges()`, and `ExchangeClientFactory`.
- Key files:
  - `ServiceCollectionExtensions.cs` — DI registration; named HttpClient; keyed singletons; options validation
  - `ExchangeClientFactory.cs` — `IExchangeClientFactory` implementation (resolves `IExchangeClient` by `ExchangeId`)

## Data Flow

```
Consumer Code
     |
     | new Symbol(Asset.Btc, Asset.Usdt)
     v
IExchangeClient  (BinanceExchangeClient)
     |
     |-- .MarketData.GetPriceAsync(symbol)
     |-- .Trading.PlaceOrderAsync(request)
     |-- .Account.GetBalancesAsync()
     v
BinanceMarketDataService / BinanceTradingService / BinanceAccountService
     |
     | ISymbolMapper.ToWire(symbol)  =>  "BTCUSDT"
     | builds Dictionary<string,string> parameters
     v
IBinanceHttpClient.GetAsync<T>("/api/v3/ticker/price", params, signed:false)
     |
     | builds URL with query string (+ recvWindow if signed)
     | HttpRequestMessage marked BinanceSigningRequest if signed
     v
HttpClient  [resilience handler chain]
     |
     v
RateLimitThrottleHandler           -- awaits IRateLimitGate
     v
TransientExhaustionHandler         -- maps exhausted transient outcomes to typed exceptions
     v
ResilienceHandler (Polly)          -- retry (GET only) + per-attempt timeout
     v
BinanceSigningHandler (optional)   -- adds timestamp + HMAC-SHA256 signature
     v
ErrorTranslationHandler            -- BinanceErrorTranslator: {code,msg} -> typed exception
     v
SocketsHttpHandler                 -- real TCP connection to api.binance.com
     |
     | raw HttpResponseMessage
     v
BinanceHttpClient                  -- ReadFromJsonAsync<T> -> Binance DTO
     v
DeltaMapper (BinanceResponseProfile) -- DTO -> domain model (via BinanceValueParsers, ISymbolMapper.FromWire)
     v
Consumer receives typed domain model (Ticker, Order, AssetBalance, etc.)
```

## Shared Code
- `SymbolMapper` — used by all exchange services; shared between factory-free and DI paths via `BinanceClientComposer`
- `BinanceValueParsers` (internal) — shared by all three Binance services and `BinanceMappingProfiles`
- `HttpClientPipelineBuilder` — used by factory-free `BinanceClientComposer.BuildResilientHttpClient()` and by DI via `ApplyResiliencePipeline()`
- `ExchangeResiliencePipeline.Configure()` — single definition of retry/timeout strategy used by both paths
- `RetryAfterReader` — used by `BinanceErrorTranslator` and `TransientExhaustionHandler`

## External Integrations
- **Binance REST API** (`https://api.binance.com`): `BinanceExchangeClient.cs:15` (default `BaseUrl`)
  - Endpoints: `/api/v3/ticker/24hr`, `/api/v3/depth`, `/api/v3/klines`, `/api/v3/ticker/price`, `/api/v3/trades`, `/api/v3/exchangeInfo`, `/api/v3/account`, `/api/v3/order`, `/api/v3/openOrders`, `/api/v3/time`
  - Authentication: HMAC-SHA256 signed requests; API key via `X-MBX-APIKEY` header

## Configuration
- **BinanceOptions** (direct): `BinanceExchangeClient.cs:14-28` — `BaseUrl`, `ApiKey`, `SecretKey`, `TimeoutSeconds`, `ReceiveWindow`
- **Environment variables**: `BINANCE_API_KEY`, `BINANCE_SECRET_KEY` — `BinanceExchangeClient.cs:93-94`, `ServiceCollectionExtensions.cs:116-120`
- **appsettings.json (DI path)**: `IOptions<BinanceOptions>` with `ValidateOnStart` — `ServiceCollectionExtensions.cs:43-48`
- **ResilienceOptions**: `ResilienceOptions.cs` — `MaxRetries`, `BaseDelay`, `MaxDelay`, `PerAttemptTimeout`, `UsageHeaderName`

## Dependency Graph

```
samples/BasicUsage
    └── CryptoExchanges.Net.Binance (via public API)

tests/CryptoExchanges.Net.Core.Tests.Unit
    └── CryptoExchanges.Net.Core

tests/CryptoExchanges.Net.Http.Tests.Unit
    ├── CryptoExchanges.Net.Core
    └── CryptoExchanges.Net.Http

tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit
    ├── CryptoExchanges.Net.Core
    └── CryptoExchanges.Net.DependencyInjection

tests/CryptoExchanges.Net.Binance.Tests.Integration
    ├── CryptoExchanges.Net.Core
    ├── CryptoExchanges.Net.Binance  (InternalsVisibleTo)
    └── CryptoExchanges.Net.Http

src/CryptoExchanges.Net.DependencyInjection
    ├── CryptoExchanges.Net.Core
    ├── CryptoExchanges.Net.Binance
    └── CryptoExchanges.Net.Http

src/CryptoExchanges.Net.Binance
    ├── CryptoExchanges.Net.Core
    └── CryptoExchanges.Net.Http

src/CryptoExchanges.Net.Http
    └── CryptoExchanges.Net.Core

src/CryptoExchanges.Net.Core
    (no project references — only ME.Logging.Abstractions + ME.DI.Abstractions)
```
