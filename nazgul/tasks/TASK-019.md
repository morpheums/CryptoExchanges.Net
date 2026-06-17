---
id: TASK-019
status: PLANNED
---

# TASK-019: BitgetSigningHandler (header-based)

**Milestone**: M-BITGET
**Wave**: 14
**Group**: 14
**Status**: PLANNED
**Depends on**: TASK-018
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bitget (`ACCESS-KEY/SIGN/TIMESTAMP/PASSPHRASE` headers; re-sign per attempt)
**Blast radius**: LOW — new file in Bitget project; plugs into the generic Http `requestFinalizerFactory`.

## Description
Implement `BitgetSigningHandler : DelegatingHandler` that, on each attempt (below retry), computes a fresh epoch-ms timestamp (UtcNow + offset), builds the prehash via `BitgetSignatureService`, and sets Bitget headers: `ACCESS-KEY`, `ACCESS-SIGN` (base64), `ACCESS-TIMESTAMP`, `ACCESS-PASSPHRASE`, plus `Content-Type: application/json`. Strips prior signing headers before re-adding for clean retries. Acts only when `BitgetSigningRequest.IsSigned`. Reads path+query (GET) or body (POST) consistently with the signer.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs` (header-based, passphrase, re-sign per attempt — established TASK-012)
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:32-80` (GET path/query vs POST body extraction)

## Acceptance Criteria
1. A signed request carries `ACCESS-KEY`, `ACCESS-SIGN`, `ACCESS-TIMESTAMP`, `ACCESS-PASSPHRASE` with `ACCESS-SIGN` equal to the base64 signature over the correct prehash.
2. Two consecutive `SendAsync` on a retried request refresh timestamp+signature and leave exactly one set of `ACCESS-*` headers.
3. Missing passphrase on a signed request fails fast with a clear exception; unsigned requests do not get signing headers.

## Test Requirements
- Unit/e2e tests in TASK-022 assert header set for GET+POST and re-sign-on-retry via stub handler.
