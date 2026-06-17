---
id: TASK-021
status: PLANNED
---

# TASK-021: BitgetHttpClient + interface

**Milestone**: M-BITGET
**Wave**: 14
**Group**: 14
**Status**: PLANNED
**Depends on**: TASK-020
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bitget (REST transport over shared resilience pipeline)
**Blast radius**: LOW — new files in Bitget project.

## Description
Implement `IBitgetHttpClient` and `BitgetHttpClient` (`GetAsync<T>`, `PostAsync<T>`, `DeleteAsync<T>`) mirroring the OKX wrapper. GET appends an escaped query string to the request path; POST sends a JSON body. Marks signed requests via `BitgetSigningRequest.MarkSigned`. Path+query construction must match `BitgetSigningHandler`'s prehash (path + `?`+query for GET). Case-insensitive `System.Text.Json`.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bitget/IBitgetHttpClient.cs`
- `src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bitget/IBitgetHttpClient.cs
- src/CryptoExchanges.Net.Bitget/BitgetHttpClient.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs` (path+query consistency with header signer — TASK-014)
- `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:26-68` (Get/Post/Delete, EscapeDataString, JSON options)

## Acceptance Criteria
1. `GetAsync<T>` builds an escaped path+query and marks signed when requested; `PostAsync<T>` sends a JSON body and marks signed.
2. The request path+query handed to the pipeline is byte-consistent with the prehash `BitgetSigningHandler` computes.
3. `IBitgetHttpClient` is internal and exposed to the integration test project via `InternalsVisibleTo`.

## Test Requirements
- Integration tests in TASK-022 assert URL/body/signed-marker via stub handler; service unit tests mock `IBitgetHttpClient`.
