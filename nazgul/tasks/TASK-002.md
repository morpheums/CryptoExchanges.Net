---
id: TASK-002
status: DONE
---

# TASK-002: BybitSignatureService + signing request marker

**Milestone**: M-BYBIT
**Wave**: 2
**Group**: 2
**Status**: PLANNED
**Depends on**: TASK-001
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (HMAC-SHA256 hex; sign-string `timestamp+apiKey+recvWindow+queryString` GET / `+jsonBody` POST)
**Blast radius**: LOW — new files inside the Bybit project only.

## Description
Implement `BybitSignatureService` — HMAC-SHA256 over the Bybit sign-string, **hex** output (same primitive as Binance: `HMACSHA256.HashData` + `Convert.ToHexStringLower`). Unlike Binance, the signature is NOT appended to the query; it is returned for the handler to place in headers. Provide a method that builds the canonical sign-string for GET (`timestamp + apiKey + recvWindow + queryString`) and for POST (`timestamp + apiKey + recvWindow + jsonBody`). Add `BybitSigningRequest` marker (mirrors `BinanceSigningRequest`) to flag signed requests and prevent double-signing on retry.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs
- src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:18-35` (HMAC-SHA256 hex, secret-key guard)
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs` (signed-request marker / IsSigned)

## Acceptance Criteria
1. `Sign(string)` returns lowercase-hex HMAC-SHA256 matching a known Bybit test vector; secret-key guard via `ArgumentException.ThrowIfNullOrWhiteSpace`.
2. Sign-string builders produce exactly `timestamp+apiKey+recvWindow+queryString` (GET) and `timestamp+apiKey+recvWindow+jsonBody` (POST), with no signature appended to the payload.
3. `BybitSigningRequest.IsSigned`/`MarkSigned` round-trips on an `HttpRequestMessage` and is idempotent across retries.

## Test Requirements
- Unit tests authored in TASK-008 cover signature hex output against a fixed vector and sign-string assembly for GET/POST.

## Implementation Notes

### Files created
- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs`

### BybitSignatureService
- Mirrors `BinanceSignatureService` exactly for the HMAC primitive: `HMACSHA256.HashData(_secretKeyBytes, signBytes)` + `Convert.ToHexStringLower(hash)` for lowercase-hex output.
- Secret-key guard via `ArgumentException.ThrowIfNullOrWhiteSpace` in the same `InitializeSecretKey` helper pattern (key bytes cached in a `readonly` field via primary-constructor initializer).
- `Sign(string signString)` returns the hex signature; it does NOT append it to any payload (the key behavioral difference from Binance, which has `BuildSignedQuery`). A `<remarks>` note documents that the caller places the signature in the `X-BAPI-SIGN` header.
- Two static sign-string builders produce the canonical strings exactly:
  - `BuildGetSignString` → `timestamp + apiKey + recvWindow + queryString`
  - `BuildPostSignString` → `timestamp + apiKey + recvWindow + jsonBody`
  These are `static` because they have no dependency on the secret key, keeping assembly testable independently of signing.

### BybitSigningRequest
- Mirrors `BinanceSigningRequest` exactly: static class, `HttpRequestOptionsKey<bool>` keyed `"bybit.signed"` (vs `"binance.signed"`), `MarkSigned`/`IsSigned` with `ArgumentNullException.ThrowIfNull` guards. `IsSigned` round-trips and is idempotent across retries.

### Deviation from Binance pattern
- `BinanceSigningRequest` has `<see cref="BinanceSigningHandler"/>` in its summary. The equivalent `BybitSigningHandler` does not exist yet (arrives in a later task), and an unresolvable cref fails the build under `TreatWarningsAsErrors` (CS1574). Replaced the cref with plain text "the Bybit signing handler" to keep the build green; can be upgraded to a `<see cref>` once the handler lands.

### Verification
- `dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s).**

## Rework (review round 1)
- **Blocking item** (code-reviewer REJECT @85): static sign-string builders lacked input guards.
- **Fix**: added `ArgumentException.ThrowIfNullOrWhiteSpace` for `timestamp`/`apiKey`/`recvWindow` and `ArgumentNullException.ThrowIfNull` for `queryString`/`jsonBody` (the latter may legitimately be empty for parameterless GETs / empty-body POSTs) in both `BuildGetSignString` and `BuildPostSignString`.
- Non-blocking concerns (below threshold 80) left as-is to preserve Binance parity; `string`→`long` typing deferred to the OKX generalization phase.
- Build after fix: `dotnet build CryptoExchanges.Net.sln` → 0 Warning(s), 0 Error(s).

## Commits
- **Commit**: 5654d93 feat(M2): TASK-002 BybitSignatureService + BybitSigningRequest marker
- **Commit (rework)**: e9fabc5 fix(M2): TASK-002 add input guards to sign-string builders (review round 1)

## Review
- **Review Gate**: PASSED (round 2)
- **Reviewers**: architect-reviewer (APPROVE), code-reviewer (round 1 REJECT@85 → round 2 APPROVE@98 after guard fix), security-reviewer (APPROVE), api-reviewer (APPROVE)
- **Pre-checks**: build 0w/0e, tests 135 passed / 0 failed
- **Blocking item fixed**: input guards added to `BuildGetSignString`/`BuildPostSignString`.
- **Non-blocking concerns (below threshold 80, deferred)**: `string`→`long` typing of `timestamp`/`recvWindow`; public static builders leaking signing detail; absence of `ISignatureService` interface — all to be revisited in the OKX credential/signing generalization (TASK-009).
