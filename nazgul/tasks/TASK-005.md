---
id: TASK-005
status: PLANNED
---

# TASK-005: BybitHttpClient + interface

**Milestone**: M-BYBIT
**Wave**: 3
**Group**: 3
**Status**: PLANNED
**Depends on**: TASK-004
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (REST transport over shared resilience pipeline)
**Blast radius**: LOW — new files in Bybit project.

## Description
Implement `IBybitHttpClient` and `BybitHttpClient` (internal HTTP wrapper: `GetAsync<T>`, `PostAsync<T>`, `DeleteAsync<T>`) mirroring the Binance wrapper. Build query strings with `Uri.EscapeDataString`, mark signed requests via `BybitSigningRequest.MarkSigned`, deserialize with `System.Text.Json` (`PropertyNameCaseInsensitive = true`). POST uses JSON body (Bybit V5 is JSON-bodied), not form-encoding — this is the key transport delta from Binance.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs`
- `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs
- src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/IBinanceHttpClient.cs` (mockable internal interface)
- `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:26-68` (Get/Post/Delete, EscapeDataString, JSON options)

## Acceptance Criteria
1. `GetAsync<T>` builds an escaped query string and marks the request signed when `signed: true`; `PostAsync<T>` sends a JSON body and marks signed.
2. JSON deserialization is case-insensitive and returns the typed Bybit DTO; signing itself is left to `BybitSigningHandler` (no inline signing in the client).
3. `IBybitHttpClient` is `internal` and visible to the integration test project via `InternalsVisibleTo`.

## Test Requirements
- Integration tests in TASK-008 use a stub handler to assert URL/body/signed-marker shape; unit tests mock `IBybitHttpClient` for services.
