---
id: TASK-003
status: IMPLEMENTED
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

## Implementation Notes

### Created
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs` — `BybitSigningHandler : DelegatingHandler` mirroring `BinanceSigningHandler`, but carrying the signature in headers instead of the query.

### Touched (allowed optional nicety)
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs` — upgraded the plain-text "the Bybit signing handler" doc reference to `<see cref="BybitSigningHandler"/>` now that the type exists. Rebuilt clean (0 warnings).

### Behavior
- Sets `X-BAPI-API-KEY` on every request (mirrors Binance setting `X-MBX-APIKEY` on all requests, gated on a non-empty apiKey).
- For signed requests, on EACH attempt (`ResignAsync`, below the retry strategy):
  - Computes a fresh timestamp: `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeOffset()`, formatted with `CultureInfo.InvariantCulture` (identical posture to Binance).
  - GET/DELETE: signs over `timestamp+apiKey+recvWindow+queryString` (query read from `RequestUri.Query`, leading `?` trimmed) via `BybitSignatureService.BuildGetSignString` + `Sign`.
  - POST: reads body once via `ReadAsStringAsync` and signs over `timestamp+apiKey+recvWindow+jsonBody` via `BuildPostSignString` + `Sign`.
  - Removes any prior `X-BAPI-TIMESTAMP` / `X-BAPI-RECV-WINDOW` / `X-BAPI-SIGN` before re-adding, so two consecutive `SendAsync` calls on the same retried request yield a fresh timestamp and a SINGLE (not doubled) set of headers.
- Unsigned requests pass through unchanged except for the api-key header.

### Deviation from Binance pattern (justified)
- Binance reads its body for re-signing and replaces `request.Content` with a new `StringContent` (form-urlencoded, signature appended). Bybit does NOT mutate the body — the signature lives in a header — so the handler only reads the body (once) and adds headers. No `StringContent` swap / `Dispose` is needed, and the POST body is left intact for `BuildPostSignString` correctness.
- Constructor signature adds a `string recvWindow` parameter (alongside `apiKey`, `signatureService`, `timeOffset`) because Bybit's recvWindow is a required header value; Binance has no equivalent. It is passed as a pre-formatted string to keep the handler agnostic of `BybitOptions` (mirrors how Binance passes `apiKey` as a plain string rather than the whole options object). The composer (TASK to wire) is responsible for formatting `BybitOptions.ReceiveWindow` (decimal) to invariant string.

### Verification
- `dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s)** (run twice: after creating the handler, and again after the `<see cref>` upgrade).

## Rework (review round 1)
- **Blocking** (api-reviewer REJECT@95): signing types were `public`. **Decision**: accepted — changed `BybitSigningHandler` and `BybitSignatureService` to `internal sealed`. Consumer-facing contract unchanged (InternalsVisibleTo already covers Tests.Integration + DependencyInjection). This gives the new module correct API hygiene from day one.
- **Blocking** (api-reviewer REJECT@80, conditional): `recvWindow` string ctor param — auto-downgrades to non-blocking now the type is internal (per the reviewer's own condition). Left as invariant string (handler stays agnostic of BybitOptions).
- **Non-blocking addressed**: added `<param>` XML docs to the 4 ctor params (api-reviewer @90).
- **Non-blocking NOT applied** (deliberate): ctor `apiKey` guard (security @82) — would break market-data-only clients constructed without credentials that make only unsigned calls; current "throw only when actually signing" posture is correct.
- **Cross-module follow-up (tracked, out of scope)**: Binance signing types (`BinanceSigningHandler`/`BinanceSignatureService`/`BinanceSigningRequest`) remain `public`. Harmonize to `internal` during the TASK-009 credential/signing generalization to restore Binance/Bybit symmetry.
- **Note for TASK-008**: signature/handler unit tests must access internals via `InternalsVisibleTo` (use the Bybit.Tests.Integration project, already granted, or add the unit-test project to the csproj IVT list).
- Build + tests after rework: 0w/0e; 135 tests pass.

## Commits
- **Commit**: 283bcf0 feat(M2): TASK-003 BybitSigningHandler (header-based, re-sign per attempt)
- **Commit (rework)**: pending
