---
id: TASK-003
status: PLANNED
---

# TASK-003: BybitSigningHandler

**Milestone**: M-BYBIT
**Wave**: 3
**Group**: 3
**Status**: PLANNED
**Depends on**: TASK-002
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (signature via HEADERS, re-sign on each attempt)
**Blast radius**: LOW — new file in Bybit project; plugs into the existing generic Http `requestFinalizerFactory` (no Http change).

## Description
Implement `BybitSigningHandler : DelegatingHandler` mirroring `BinanceSigningHandler` but placing the signature in HEADERS, not the query. On each attempt (below the retry strategy) it computes a fresh timestamp (UtcNow + offset), builds the canonical sign-string via `BybitSignatureService`, and sets the Bybit auth headers (`X-BAPI-API-KEY`, `X-BAPI-TIMESTAMP`, `X-BAPI-RECV-WINDOW`, `X-BAPI-SIGN`). Strips/recomputes prior signing headers so retries re-sign cleanly. Only acts when `BybitSigningRequest.IsSigned` is set.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:12-30` (DelegatingHandler shape, apiKey header set, IsSigned gate, ConfigureAwait(false))
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:32-80` (re-sign on each attempt, GET vs POST body handling)

## Acceptance Criteria
1. For a signed GET, the handler sets `X-BAPI-SIGN` over `timestamp+apiKey+recvWindow+queryString`; for a signed POST it signs over `timestamp+apiKey+recvWindow+jsonBody` (reads body once via `ReadAsStringAsync`).
2. Two consecutive `SendAsync` calls on the same retried request produce a fresh timestamp and a single (not doubled) set of `X-BAPI-*` headers.
3. Unsigned requests pass through unchanged except for the api-key header; no signing headers are added.

## Test Requirements
- Unit/e2e tests in TASK-008 assert header presence/values for GET+POST and re-sign-on-retry through a stub handler (mirroring `BinancePipelineEndToEndTests`).
