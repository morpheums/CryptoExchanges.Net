---
id: TASK-012
status: PLANNED
---

# TASK-012: OkxSigningHandler (header-based)

**Milestone**: M-OKX
**Wave**: 9
**Group**: 9
**Status**: PLANNED
**Depends on**: TASK-011
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#okx (`OK-ACCESS-KEY/SIGN/TIMESTAMP/PASSPHRASE` headers; re-sign per attempt)
**Blast radius**: LOW — new file in Okx project; plugs into the generic Http `requestFinalizerFactory`.

## Description
Implement `OkxSigningHandler : DelegatingHandler` that, on each attempt (below retry), computes a fresh ISO-8601 timestamp (UtcNow + offset), builds the prehash via `OkxSignatureService`, and sets the four OKX headers: `OK-ACCESS-KEY`, `OK-ACCESS-SIGN` (base64 signature), `OK-ACCESS-TIMESTAMP`, `OK-ACCESS-PASSPHRASE`. Strips prior signing headers before re-adding so retries re-sign cleanly. Acts only when `OkxSigningRequest.IsSigned`. Reads request path+query (GET) or body (POST) to feed the signer; for POST reads the JSON content once.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs` (header-based signing reference established in TASK-003)
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:32-80` (re-sign per attempt, GET path/query vs POST body extraction)

## Acceptance Criteria
1. A signed request carries all four `OK-ACCESS-*` headers with `OK-ACCESS-SIGN` equal to the base64 signature over the correct prehash (path+query for GET, body for POST).
2. Two consecutive `SendAsync` on a retried request refresh the timestamp+signature and leave exactly one set of `OK-ACCESS-*` headers.
3. Missing passphrase on a signed request fails fast with a clear exception (signing requires all three credentials); unsigned requests get only `OK-ACCESS-KEY` (or pass through).

## Test Requirements
- Unit/e2e tests in TASK-015 assert header set for GET+POST and re-sign-on-retry via a stub handler.
