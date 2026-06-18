---
id: TASK-014
status: PLANNED
---

# TASK-014: OkxHttpClient + interface

**Milestone**: M-OKX
**Wave**: 9
**Group**: 9
**Status**: PLANNED
**Depends on**: TASK-013
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#okx (REST transport over shared resilience pipeline)
**Blast radius**: LOW — new files in Okx project.

## Description
Implement `IOkxHttpClient` and `OkxHttpClient` (`GetAsync<T>`, `PostAsync<T>`, `DeleteAsync<T>`) mirroring the Binance/Bybit wrapper. GET uses an escaped query string appended to the request path; POST sends a JSON body. Marks signed requests via `OkxSigningRequest.MarkSigned`. The request path passed downstream must match what the signer hashes (path + query for GET) — ensure path/query construction is consistent with `OkxSigningHandler`'s prehash assembly. Case-insensitive `System.Text.Json` deserialization.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs`
- `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs
- src/CryptoExchanges.Net.Okx/OkxHttpClient.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:26-68` (Get/Post/Delete, EscapeDataString, JSON options)
- `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs` (JSON-body POST + signed marker reference from TASK-005)

## Acceptance Criteria
1. `GetAsync<T>` builds an escaped path+query and marks signed when requested; `PostAsync<T>` sends a JSON body and marks signed.
2. The request path+query handed to the pipeline is byte-consistent with the prehash the `OkxSigningHandler` computes (no signature mismatch).
3. `IOkxHttpClient` is internal and exposed to the integration test project via `InternalsVisibleTo`.

## Test Requirements
- Integration tests in TASK-015 assert URL/body/signed-marker via stub handler; service unit tests mock `IOkxHttpClient`.
