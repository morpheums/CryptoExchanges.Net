---
id: TASK-019
status: DONE
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

## Implementation Notes
- `internal sealed class BitgetSigningHandler : DelegatingHandler` mirroring `OkxSigningHandler` (primary ctor, ConfigureAwait(false), per-attempt re-sign below the retry strategy, unsigned pass-through).
- **ISignatureService ctor**: ctor depends on the Core `ISignatureService` interface (not the concrete `BitgetSignatureService`), matching the REF-002 OKX ctor — keeps signing swappable and the handler decoupled from the concrete service.
- **Path/query SPLIT for BuildPrehash**: Bitget's `BuildPrehash(timestamp, method, requestPath, queryString, body)` takes path and query SEPARATELY (it re-inserts the literal `?` only when query is non-empty), unlike OKX which takes a combined `PathAndQuery`. So I split the outgoing URI: `requestPath = RequestUri.AbsolutePath`, `queryString = RequestUri.Query.TrimStart('?')`. Reading the actual outgoing URI (rather than reconstructing) keeps the prehash byte-for-byte consistent with what `BitgetHttpClient` sends — sign-consistency.
- Fresh epoch-ms timestamp per attempt via `BitgetSignatureService.FormatTimestamp(UtcNow.AddMilliseconds(timeOffset()))`. Body read once for POST/PUT-with-content, else `""`.
- Strips any prior `ACCESS-KEY/SIGN/TIMESTAMP/PASSPHRASE` then adds all four (ACCESS-SIGN = base64 signature) → exactly one set on retry with a refreshed timestamp/signature.
- `Content-Type: application/json` set on the StringContent for POST/PUT (only when content present); no content-type request header added without content.
- **Passphrase fail-fast**: signed request with null/empty apiKey or passphrase throws `InvalidOperationException` (signing needs all 3 Bitget creds), mirroring OKX.

## Verification
- `dotnet build CryptoExchanges.Net.sln` → Build succeeded, 0 Warning(s), 0 Error(s).
- Tests arrive in TASK-022.

## Commits
- abdedd60b7664cd680775a05e3f8ffc26cab758a — feat(M4): TASK-019 BitgetSigningHandler
