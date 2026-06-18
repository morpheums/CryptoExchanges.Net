---
id: TASK-011
status: IMPLEMENTED
---

# TASK-011: OkxSignatureService (base64 prehash) + signing marker

**Milestone**: M-OKX
**Wave**: 8
**Group**: 8
**Status**: PLANNED
**Depends on**: TASK-009, TASK-010
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#okx (HMAC-SHA256 base64; prehash `timestamp+METHOD+requestPath+body`)
**Blast radius**: LOW — new files in Okx project; consumes the TASK-009 base64 encoder.

## Description
Implement `OkxSignatureService` computing HMAC-SHA256 with **base64** output (via the Core `SignatureEncoding` from TASK-009) over the OKX prehash string `timestamp + METHOD + requestPath + body` (METHOD upper-case; `requestPath` includes the query string for GET; `body` is the raw JSON for POST, empty otherwise). The OKX timestamp is ISO-8601 UTC (e.g. `2026-06-17T12:00:00.000Z`), not epoch-ms — handle that here. Add `OkxSigningRequest` marker.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs`
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs
- src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs` (base64 HMAC output — created in TASK-009)
- `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:37-41` (secret-key guard)
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs` (marker)

## Acceptance Criteria
1. `Sign(...)` returns base64 HMAC-SHA256 over `timestamp+METHOD+requestPath+body` matching a known OKX test vector.
2. Timestamp is ISO-8601 UTC with milliseconds and trailing `Z`; METHOD is upper-cased; for GET the query string is part of `requestPath`, for POST the JSON body is appended.
3. `OkxSigningRequest.IsSigned`/`MarkSigned` round-trips and is idempotent across retries.

## Test Requirements
- Unit tests in TASK-015 assert the base64 vector, prehash assembly (GET path+query vs POST body), and ISO timestamp format.

## Base SHA
- c9243437133b98700aa5ffd1cd0f55615fd3549b

## Implementation Notes
- **Files created** (both `internal`, per ADR-001 conv #2 + post-009B precedent — in-assembly `AddOkxExchange` constructs them):
  - `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs` (namespace `CryptoExchanges.Net.Okx.Auth`)
  - `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs` (namespace `CryptoExchanges.Net.Okx.Resilience`)
- **HMAC via Core, no re-implementation**: `Sign(prehash)` delegates to
  `HmacSignature.Compute(_secretKey, prehash, SignatureEncoding.Base64)` from
  `CryptoExchanges.Net.Core.Auth`. No `HMACSHA256`/`Convert.ToBase64String` lives in the OKX service
  (unlike Bybit which still hand-rolls hex — OKX uses the Core primitive from TASK-009). The Core
  `Compute` already UTF-8 encodes the secret and renders base64, so the service stores the secret as a
  plain `string`.
- **Prehash builder** `static string BuildPrehash(timestamp, method, requestPath, body)`:
  returns `$"{timestamp}{method.ToUpperInvariant()}{requestPath}{body}"`. Identity inputs
  (timestamp/method/requestPath) guarded with `ArgumentException.ThrowIfNullOrWhiteSpace`; `body`
  guarded with `ArgumentNullException.ThrowIfNull` only (empty body is valid for GET/DELETE). Static so
  TASK-015 can unit-test assembly independently of the secret. `requestPath` is passed in already
  including the leading `/` and the GET query string; `method` is upper-cased inside the builder.
- **ISO-8601 timestamp helper** `static string FormatTimestamp(DateTimeOffset)`:
  `timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)` →
  millisecond precision with trailing literal `Z` (e.g. `2026-06-17T12:00:00.000Z`). NOT epoch-ms. The
  later signing handler (TASK-012) supplies the timestamp string; both the builder and the helper take
  a string/`DateTimeOffset` so they are testable without the handler.
- **Signature returned, not appended**: `Sign(...)` RETURNS the base64 string; the handler will place
  it in the `OK-ACCESS-SIGN` header (documented in `<remarks>`, plain text — no `<see cref>` to the
  not-yet-existing OkxSigningHandler to avoid CS1574 under TreatWarningsAsErrors).
- **Secret-key guard**: mirrors Bybit's `InitializeSecretKey` shape — `ArgumentException.ThrowIfNullOrWhiteSpace`
  in a private initializer invoked from the primary-constructor field initializer.
- **OkxSigningRequest**: mirrors `BybitSigningRequest` exactly — `internal static` class,
  `HttpRequestOptionsKey<bool>` keyed `"okx.signed"`, `MarkSigned`/`IsSigned` with `ArgumentNullException`
  guards, idempotent across retries (`Set` overwrites; `TryGetValue` read).
- Full XML docs on all members (CS1591 is in NoWarn but documented anyway, matching Bybit).

## Verification
- `dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s).**
- No new tests this task (they arrive in TASK-015); solution builds clean.

## Commits
- (recorded below after commit)
