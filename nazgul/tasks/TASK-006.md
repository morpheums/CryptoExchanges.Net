---
id: TASK-006
status: IMPLEMENTED
---

# TASK-006: Bybit services + mapping + composer + ExchangeClient

**Milestone**: M-BYBIT
**Wave**: 4
**Group**: 4
**Status**: PLANNED
**Depends on**: TASK-005
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (spot REST parity: market data, trading, account); DeltaMapper mandate
**Blast radius**: MEDIUM — multiple new files; defines the public `BybitExchangeClient` API surface (api-reviewer).

## Description
Implement the three services (`BybitMarketDataService`, `BybitTradingService`, `BybitAccountService`) against `IMarketDataService`/`ITradingService`/`IAccountService`, the DeltaMapper `BybitMappingProfiles` (Bybit DTO → domain models via `IMapper`, using `BybitValueParsers` + `ISymbolMapper.FromWire`), the `BybitClientComposer` (`ComposeForDi`, `ComposeWith`, `Create`, `BuildResilientHttpClient`, `CreateMapper`), and the public `BybitExchangeClient` (`Create(BybitOptions)`, `CreateFromEnvironment()`, `SyncServerTimeAsync()`). Mapping MUST use DeltaMapper — no AutoMapper, no manual mapping.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bybit/Services/BybitMarketDataService.cs`
- `src/CryptoExchanges.Net.Bybit/Services/BybitTradingService.cs`
- `src/CryptoExchanges.Net.Bybit/Services/BybitAccountService.cs`
- `src/CryptoExchanges.Net.Bybit/Mapping/BybitMappingProfiles.cs`
- `src/CryptoExchanges.Net.Bybit/Internal/BybitClientComposer.cs`
- `src/CryptoExchanges.Net.Bybit/BybitExchangeClient.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bybit/Services/BybitMarketDataService.cs
- src/CryptoExchanges.Net.Bybit/Services/BybitTradingService.cs
- src/CryptoExchanges.Net.Bybit/Services/BybitAccountService.cs
- src/CryptoExchanges.Net.Bybit/Mapping/BybitMappingProfiles.cs
- src/CryptoExchanges.Net.Bybit/Internal/BybitClientComposer.cs
- src/CryptoExchanges.Net.Bybit/BybitExchangeClient.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/Services/BinanceMarketDataService.cs` (+ Trading/Account services)
- `src/CryptoExchanges.Net.Binance/Mapping/BinanceMappingProfiles.cs` (DeltaMapper profile bound to ISymbolMapper)
- `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs:16-87` (CreateMapper, Create, ComposeOver/With/ForDi, BuildResilientHttpClient)
- `BinanceExchangeClient` public factory shape (architecture-map.md:43-44)

## Acceptance Criteria
1. All three services implement their Core interfaces and return mapped domain models (`Ticker`, `Order`, `AssetBalance`, etc.); `MapperConfiguration.AssertConfigurationIsValid()` passes in `CreateMapper`.
2. `BybitExchangeClient.Create(BybitOptions)` composes a working client over a secret-gated signing finalizer (PassThrough when secretless), mirroring Binance's `BuildResilientHttpClient`.
3. Only `BybitExchangeClient` and `BybitOptions` are public; all internals are non-public; build is clean under TreatWarningsAsErrors with full XML docs.

## Test Requirements
- Unit tests (TASK-008) mock `IBybitHttpClient` to verify each service maps a representative DTO payload correctly via DeltaMapper.

## Implementation Notes

### Files created
- `Services/BybitMarketDataService.cs` — `IMarketDataService` over V5 spot. DTOs: `BybitResponse<T>` (uniform `{retCode,retMsg,result}` envelope), `BybitListResult<T>` (generic `result.list`), `BybitTickerResult`, `BybitTicker`, `BybitOrderBookResult`, `BybitTrade`, `BybitInstrument`.
- `Services/BybitTradingService.cs` — `ITradingService` over V5 spot. DTOs: `BybitOrderAck` (create/cancel return ids only), `BybitOrder` (full order from realtime/history).
- `Services/BybitAccountService.cs` — `IAccountService` over V5 unified account. DTOs: `BybitWalletAccount`, `BybitCoinBalance`, `BybitExecution`.
- `Mapping/BybitMappingProfiles.cs` — DeltaMapper `BybitResponseProfile : Profile` bound to `ISymbolMapper`.
- `Internal/BybitClientComposer.cs` — `CreateMapper`, `Create`, `ComposeOver`/`ComposeWith`/`ComposeForDi`, `BuildResilientHttpClient`.
- `BybitExchangeClient.cs` — public client (`Create`, `CreateFromEnvironment`, `SyncServerTimeAsync`, `PingAsync`) + internal `BybitServerTimeResult` DTO.

### Pipeline wiring (matches Binance composer)
- `BuildResilientHttpClient`: `SocketsHttpHandler` (2-min pooled lifetime) → `HttpClientPipelineBuilder.Build(inner, ResilienceOptions{UsageHeaderName="X-Bapi-Limit-Status"}, BybitErrorTranslator, ReactiveRateLimitGate, requestFinalizer)`.
- **Error translator**: `BybitErrorTranslator` passed as the pipeline translator (same seam as Binance) so non-zero `retCode` envelopes / HTTP errors become typed exceptions before reaching the services.
- **Time sync**: shared single-element `long[] offsetHolder`; `SyncServerTimeAsync` reads `/v5/market/time`, computes offset via `BybitTimeSync.ComputeOffset`, and `Interlocked.Exchange`es it into `offsetHolder[0]`. The signing handler's `Func<long> timeOffset` closure reads the same holder via `Interlocked.Read`.
- **Signing handler**: secret-gated finalizer — `PassThroughHandler` when `SecretKey` is empty (public market-data works credential-less), else `BybitSigningHandler(apiKey, BybitSignatureService, recvWindow, () => Interlocked.Read(ref offsetHolder[0]))`.
- **recvWindow formatting**: `BybitOptions.ReceiveWindow` (decimal) → `ToString(CultureInfo.InvariantCulture)` for the handler's `recvWindow` arg (constraint #1).
- **BybitHttpClient ctor** takes only `HttpClient` (constraint #2); recv-window is owned by the signing handler, not the client.
- Bybit signing types are internal; the composer lives inside the assembly, so no public exposure (constraint #3).

### DeltaMapper profile
`BybitResponseProfile(ISymbolMapper)` defines 4 maps; `MapperConfiguration.AssertConfigurationIsValid()` is invoked in `CreateMapper`:
- `BybitOrder → Order` — symbol via `FromWire`; decimals/enums via `BybitValueParsers`; `CumExecValue → CumulativeQuoteQuantity`; `triggerPrice → StopPrice` (optional); `IcebergQuantity` ignored (V5 spot has no iceberg).
- `BybitTicker → Ticker` — `prevPrice24h → OpenPrice`; `turnover24h → QuoteVolume`; `PriceChange = lastPrice - prevPrice24h`; `PriceChangePercent = price24hPcnt * 100` (V5 reports a fraction); `Timestamp` ignored (ticker has no per-row time).
- `BybitInstrument → SymbolInfo` — `FromComponents(baseCoin, quoteCoin)`; `AllowedOrderTypes = [Limit, Market]`; numeric lot/price filters ignored (nested filter objects not yet surfaced).
- `BybitCoinBalance → AssetBalance` — `ParseAssetOrNone(coin)`; `Free = walletBalance - locked`; `Locked = locked`.

### V5 response-shape decisions (non-obvious)
- **Create/cancel return ids only**: `POST /v5/order/create` and `/v5/order/cancel` return `{orderId, orderLinkId}`, not a full order. To honour the `ITradingService` contract (return a populated `Order`), `PlaceOrderAsync`/`CancelOrderAsync`/`CancelOrderByClientIdAsync`/`GetOrderAsync` call a private `FetchOrderAsync` that queries `/v5/order/realtime` then falls back to `/v5/order/history`, with a minimal `Order(symbol, orderId)` last-resort so a successful action never throws/returns null.
- **CancelAllOrders**: `cancel-all` returns id list; re-fetches via `/v5/order/history` and filters to the canceled ids.
- **GetPrice**: V5 has no dedicated last-price endpoint — uses the spot ticker's `lastPrice`.
- **Klines**: returned as `result.list` of string arrays `[start, open, high, low, close, volume, turnover]`; `CloseTime`/`TradeCount` left null (not in payload).
- **Balances**: `/v5/account/wallet-balance?accountType=UNIFIED` returns `result.list[].coin[]`; flattened, zero-total balances trimmed to match the non-zero contract.
- **Kline intervals**: Bybit V5 spot has no 8h or 3d interval — those throw `ArgumentOutOfRangeException` (documented in `MapKlineInterval`).
- **ExchangeInfo rate limits**: V5 instruments-info carries no per-endpoint rate-limit rules; returns empty `RateLimits` (runtime limits handled by `ReactiveRateLimitGate` via response headers).

### Deviations from the Binance pattern (with justification)
- `BybitSigningHandler` ctor has a 4th `recvWindow` arg (Bybit carries recv-window in a header per attempt, not the query) → composer formats `ReceiveWindow` to an invariant string. Binance's handler is 3-arg.
- `BuildResilientHttpClient` uses an explicit `PassThroughHandler` when secretless (per task constraint #6) rather than passing `null` as Binance does; functionally equivalent and aligns with the DI resolution-time gate.
- No static API-key default header on the HttpClient: Bybit's API key travels in the per-attempt `X-BAPI-API-KEY` header set by the signing handler.
- Trading service re-fetches orders after create/cancel (Binance create returns the full order inline); forced by the V5 ids-only response shape.

### Verification
- `dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s) 0 Error(s)** under `TreatWarningsAsErrors=true`.
- API surface confirmed: only `BybitExchangeClient` + `BybitOptions` are public among the created files (the 3 public resilience helpers `BybitErrorTranslator`/`BybitSigningRequest`/`BybitTimeSync` predate this task and mirror Binance's public posture).
- `MapperConfiguration.AssertConfigurationIsValid()` invoked in `CreateMapper` (fails fast on a misconfigured profile).

## Commits
- **Commit**: 057d6d2 feat(M2): TASK-006 Bybit services + DeltaMapper profiles + composer + ExchangeClient
