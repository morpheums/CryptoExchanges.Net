---
id: TASK-012
status: DONE
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
**Base SHA**: 6522afa2dd0db87d19240cfdb952a9c331fd3970

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

## Implementation Notes
- Created `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs` —
  `internal sealed class OkxSigningHandler : DelegatingHandler` (namespace
  `CryptoExchanges.Net.Okx.Resilience`), internal per ADR-001.
- **Constructor** (primary, mirroring `BybitSigningHandler`): `(string apiKey, string passphrase,
  OkxSignatureService signatureService, Func<long> timeOffset)` — plain credentials + service +
  ms-offset func, agnostic of `OkxOptions`. The composer (TASK-015) wires it.
- **Header set** (signed requests only, re-applied per attempt in `ResignAsync`, below the retry
  strategy): `OK-ACCESS-KEY` = apiKey, `OK-ACCESS-SIGN` = base64 signature, `OK-ACCESS-TIMESTAMP` =
  ISO-8601 UTC ms timestamp, `OK-ACCESS-PASSPHRASE` = passphrase.
- **Timestamp**: fresh per attempt — `DateTimeOffset.UtcNow.AddMilliseconds(timeOffset())` then
  `OkxSignatureService.FormatTimestamp(...)` → ISO-8601 UTC ms + `Z`.
- **requestPath = `request.RequestUri!.PathAndQuery`** — the ACTUAL outgoing path+query. Signing the
  real request URI (rather than reconstructing it) guarantees byte-for-byte consistency with whatever
  `OkxHttpClient` built, so the prehash matches exactly what OKX receives. Method via
  `request.Method.Method` (BuildPrehash upper-cases it).
- **Body**: read once via `ReadAsStringAsync` for POST/PUT with content; empty string `""` otherwise.
- **Re-sign on retry**: strips any prior `OK-ACCESS-KEY/SIGN/TIMESTAMP/PASSPHRASE` headers before
  re-adding all four, so two consecutive `SendAsync` on a retried request leave exactly ONE set with
  a fresh timestamp/signature.
- **Passphrase fail-fast**: a signed request with null/empty `apiKey` or `passphrase` throws
  `InvalidOperationException` with a clear message (signing needs all three OKX credentials — key,
  secret held by the service, and passphrase). Guarded here even though the composer's secret-gated
  finalizer normally prevents constructing a signing handler without full creds.
- **Unsigned pass-through**: unsigned/public requests are sent untouched — no `OK-ACCESS-*` headers
  added (OKX's `OK-ACCESS-KEY` is only meaningful for private/signed calls, unlike Bybit).
- ConfigureAwait(false) on all awaits; `ArgumentNullException.ThrowIfNull(request)`; full XML docs;
  no global mutable state; no `<see cref>` to nonexistent types.

## Verification
- `dotnet build CryptoExchanges.Net.sln` → Build succeeded, **0 Warning(s), 0 Error(s)**.
- No new tests this task (TASK-015 covers them).

## Commits
- f556191def86be9eaa094d6dc4f6b44c76286707 — feat(M3): TASK-012 OkxSigningHandler (header-based, re-sign per attempt)

## Review
- **Review Gate**: PASSED (round 1) — all 4 APPROVE, no blocking items. Verified: re-sign per attempt (one header set, fresh ts), signs RequestUri.PathAndQuery (no drift), base64 via OkxSignatureService (no inline crypto), passphrase+apiKey fail-fast, unsigned pass-through (no creds leaked), ConfigureAwait(false).
- **Non-blocking (deferred)**: DELETE-with-body not handled (none in OKX V5 today); RequestUri null-forgiving (matches Bybit); ctor lacks null-guards (DI enforces). Owed: signing unit tests in TASK-015.
