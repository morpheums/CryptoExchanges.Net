---
reviewer: code-reviewer
task: TASK-059
verdict: APPROVE
---

# Code Review: TASK-059 — KuCoin REST services + HTTP client + composer + entry point

## Summary

TASK-059 delivers the full KuCoin REST surface: `IKucoinHttpClient` / `KucoinHttpClient`, three service
classes (`KucoinMarketDataService`, `KucoinAccountService`, `KucoinTradingService`), `KucoinClientComposer`,
`KucoinRequestValidation`, `KucoinExchangeClient`, and 149 unit tests. Build is clean (0W/0E with
`TreatWarningsAsErrors=true`). All 149 KuCoin unit tests pass. No blocking issues found.

---

## Key Checks Confirmed

### Build & Tests
- `dotnet build CryptoExchanges.Net.sln --no-incremental` → 0 warnings, 0 errors.
- `dotnet test CryptoExchanges.Net.Kucoin.Tests.Unit` → 149/149 passed, 0 failed, 0 skipped.

### LR-001: Guards on HTTP client endpoint parameters
- `KucoinHttpClient.GetAsync`, `PostAsync` (both overloads), `DeleteAsync`: each opens with
  `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` (lines 37, 49, 59, 79 in `KucoinHttpClient.cs`).
- Service method string parameters (`orderId`, `clientOrderId`) are not individually guarded, which is
  consistent with OKX / Bybit / Bitget peer pattern — not a defect.

### LR-002: Math.Min limit clamping (LSP / interface-default compliance)
- `KucoinTradingService.GetOrderHistoryAsync` (line 831 in diff): `Math.Min(limit, MaxHistoryLimit)` then
  validates the clamped value — interface default of 500 == MaxHistoryLimit so the common path never throws.
- `KucoinAccountService.GetTradeHistoryAsync` (line 461 in diff): same pattern — `Math.Min` first, validate
  second. Both mirror `BybitTradingService.GetOrderHistoryAsync`.
- Tests `Trading_GetOrderHistory_LimitClamped` and `Account_GetTradeHistory_LimitClamped` assert that
  passing 1000 results in `pageSize=500`.

### LR-003: Timestamp parsing in KucoinMarketDataService
- `GetCandlesticksAsync` (line 108 in `KucoinMarketDataService.cs`): uses `KucoinValueParsers.ParseMs`
  which calls `long.TryParse` and returns 0L on malformed input, then multiplies by `1000L` to convert
  unix-seconds to milliseconds. Correct and safe — matches the KuCoin candle array format
  `[startTime(s), open, close, high, low, vol, quoteVol]`.
- `GetRecentTradesAsync` (line 152): uses `KucoinValueParsers.ParseNsToMs` which divides by `1_000_000L`
  to convert nanoseconds to milliseconds. Correct — KuCoin `histories` returns nanosecond timestamps.
- Both parsers are guarded against null/empty/malformed input (return 0L rather than throw). Test
  `MarketData_GetCandlesticks_HappyPath_MapsOhlcvAndOpenTime` verifies the seconds-to-ms conversion
  (`1700000000L * 1000L`). Test `MarketData_GetRecentTrades_MapsTradesFromNsTimestamp` verifies the
  nanosecond-to-ms conversion with `1700000000000000000` → `1700000000000L`.

### LR-004: JSON ValueKind guards in error translator
- `KucoinErrorTranslator.ReadString` (line 88 in `KucoinErrorTranslator.cs`) checks
  `v.ValueKind == JsonValueKind.String` before calling `v.GetString()`. The `Parse` method wraps
  `JsonDocument.Parse` in `try/catch (JsonException)`. Both `code` and `msg` fields go through
  `ReadString`, so neither field has an unguarded typed accessor. Fully compliant with the
  BybitErrorTranslator.Parse pattern.

### LR-005: Test coverage
- 149 unit tests across:
  - `KucoinMarketDataService`: tickers (single + all + filter-unresolvable), order book (depth routing),
    candlesticks (happy path, OneMonth interval, unsupported interval → ArgumentOutOfRangeException),
    price, recent trades (ns-timestamp), exchange info (populate mapper, reject invalid currencies),
    IsSupportedAsync, ResolveSymbolAsync.
  - `KucoinAccountService`: balances (trim zeros), balance by asset (hit + miss), trade history
    (signed + mapped via FillDto, limit clamped).
  - `KucoinTradingService`: open orders (signed), order history (limit clamped), place limit order
    (body params), place market order (no price key), cancel by id (signed + refetch), cancel by
    clientOrderId (fallback refetch), cancel all (filter by ack ids), get order (signed).
  - Signed-request marking: private endpoints assert `signed=true`; public endpoints assert `signed=false`.
  - Error envelope: `ExchangeApiException` surfaces from market data; `InvalidOrderException` from trading.
  - Entry point: `KucoinExchangeClient.Create` (no credentials + with credentials), `PingAsync` returns
    false on exception, `SyncServerTimeAsync` calls `timeSync.ApplyOffset`.
  - `KucoinRequestValidation`: limit boundaries (0 → throws, 501 → throws), inverted window, valid window.

### Correctness checks
- Every `await` in all service files uses `.ConfigureAwait(false)`.
- `CancellationToken` is forwarded to every `http.GetAsync` / `PostAsync` / `DeleteAsync` call.
- `OperationCanceledException when ct.IsCancellationRequested` is re-thrown in `PingAsync`
  (`KucoinExchangeClient.cs` line 281); the broader `catch` that returns `false` is documented
  for the same reason as `BinanceExchangeClient.cs:127-129`.
- `HttpRequestMessage` and `HttpResponseMessage` are wrapped in `using` in `KucoinHttpClient.cs`.
- `IAsyncDisposable` implemented correctly in `KucoinExchangeClient`; `_httpClient.Dispose()` is guarded
  by `_ownsHttpClient` — DI path never disposes the factory-owned `HttpClient`.

### Documentation
- All public types and members have XML doc comments or `<inheritdoc />` on implementations.
- `KucoinTradingService` `<remarks>` block is load-bearing (explains the re-fetch contract that is
  non-obvious from the type signature); not noise.
- `[SuppressMessage]` on `KucoinExchangeClient._http` has a `Justification` explaining the IKucoinHttpClient
  interface hold is intentional for DI/testability — compliant with project suppression rules.

### Style / conventions
- One type per file throughout.
- Primary constructors used on all three services and `KucoinHttpClient` — consistent with the codebase.
- `KucoinClientComposer` is `internal static` — correct (no public surface needed).
- `KucoinErrorTranslator` is `internal sealed` per ADR-001 conv #2.

---

## Findings

### Finding: ParseMs used on unix-second candle timestamp — semantic naming minor concern
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Kucoin/Services/KucoinMarketDataService.cs:108`
- **Category**: Code Quality
- **Verdict**: PASS
- `ParseMs` is named "parse milliseconds" but in the candles context the input is unix seconds. The value
  is immediately multiplied by `1000L` and commented (`// KuCoin candle timestamp is unix SECONDS (not ms)`),
  so the intent is clear. Functionally correct — `ParseMs` is just `long.TryParse` with a safe fallback.
  The comment on line 107 provides the needed context. No action required.

### Finding: GetRecentTradesAsync limit guard uses Math.Max(1, limit) not Math.Min
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Kucoin/Services/KucoinMarketDataService.cs:146`
- **Category**: Correctness
- **Verdict**: PASS
- `GetRecentTradesAsync` uses `trades.Take(Math.Max(1, limit))`. This is correct for the KuCoin
  `/api/v1/market/histories` endpoint which returns a fixed recent-trade window (no server-side limit
  parameter). The client applies a post-fetch slice and uses `Math.Max(1, limit)` to avoid `Take(0)`.
  The interface default of 500 maps to "take up to 500 from the fixed window", which is reasonable.
  The pattern is different from `GetOrderHistoryAsync` because KuCoin does not accept a `pageSize`
  for the recent-trades endpoint.

---

## Result Table

| Rule | Check | Result |
|------|-------|--------|
| LR-001 | ThrowIfNullOrWhiteSpace on endpoint params | PASS |
| LR-002 | Math.Min clamping in GetOrderHistoryAsync + GetTradeHistoryAsync | PASS |
| LR-003 | ParseMs (seconds×1000) in GetCandlesticksAsync; ParseNsToMs in GetRecentTradesAsync | PASS |
| LR-004 | ValueKind guard in KucoinErrorTranslator.ReadString for both code + msg | PASS |
| LR-005 | 149/149 unit tests pass; all service methods + signed marking + error translation covered | PASS |
| Build | `dotnet build` 0W/0E with TreatWarningsAsErrors=true | PASS |
| ConfigureAwait | All awaits use .ConfigureAwait(false) | PASS |
| CT forwarding | CancellationToken forwarded everywhere; OCE re-thrown in PingAsync | PASS |
| Disposables | HttpRequestMessage/HttpResponseMessage in `using`; HttpClient guarded by ownsHttpClient | PASS |
| XML docs | <inheritdoc/> on all impl methods; public types have <summary> | PASS |
| Nullable | No unguarded nullable dereferences; all nullable annotations present | PASS |
| Exception handling | No silent swallowing; PingAsync suppression documented; FormatException yield-break documented | PASS |
