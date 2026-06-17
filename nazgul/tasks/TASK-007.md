---
id: TASK-007
status: PLANNED
---

# TASK-007: BybitErrorTranslator + BybitTimeSync

**Milestone**: M-BYBIT
**Wave**: 4
**Group**: 4
**Status**: PLANNED
**Depends on**: TASK-005
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (typed error surface + clock-skew handling)
**Blast radius**: LOW — new files in Bybit project; implements Core `IExchangeErrorTranslator`.

## Description
Implement `BybitErrorTranslator : IExchangeErrorTranslator` mapping Bybit's `retCode`/`retMsg` envelope (and HTTP 401/403/429) to the Core typed exception hierarchy (`AuthenticationException`, `RateLimitExceededException`, `InvalidOrderException`, `InsufficientBalanceException`, `ExchangeApiException`). Implement `BybitTimeSync` to compute the clock-skew offset from Bybit's server-time endpoint (writes into the shared `long[]` offset holder, same pattern as Binance). Use `RetryAfterReader` for rate-limit `RetryAfter`.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs
- src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceErrorTranslator.cs:19-24` ({code,msg} → typed exceptions, auth/rate-limit mapping)
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceTimeSync.cs` (offset computation into long[] holder)
- `src/CryptoExchanges.Net.Http/RetryAfterReader.cs`

## Acceptance Criteria
1. Bybit auth-failure ret codes and HTTP 401/403 map to `AuthenticationException`; 429/rate-limit codes map to `RateLimitExceededException` with `RetryAfter` populated from headers.
2. Unknown error codes map to `ExchangeApiException` carrying `retCode`/`retMsg`; success envelopes (`retCode == 0`) are NOT treated as errors.
3. `BybitTimeSync` computes a signed offset (server − local) and writes it via `Interlocked` into the shared holder.

## Test Requirements
- Unit tests (TASK-008) cover each error-code → exception-type mapping and the offset sign/magnitude from a stubbed server-time response.
