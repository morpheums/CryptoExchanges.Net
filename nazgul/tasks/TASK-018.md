---
id: TASK-018
status: IMPLEMENTED
---

# TASK-018: BitgetSignatureService (base64 prehash incl. query) + signing marker

**Milestone**: M-BITGET
**Wave**: 13
**Group**: 13
**Status**: PLANNED
**Depends on**: TASK-017
**Retry count**: 0/3
**Base SHA**: 0dbe22f884a8d9709e74549d829bb9973e570dbd
**Delegates to**: none
**Traces to**: research#bitget (HMAC-SHA256 base64; prehash `timestamp+UPPERCASE-method+requestPath+['?'+queryString]+body`)
**Blast radius**: LOW — new files in Bitget project; reuses the TASK-009 base64 encoder.

## Description
Implement `BitgetSignatureService` using the Core `SignatureEncoding` base64 HMAC-SHA256 (from TASK-009) over the Bitget prehash `timestamp + UPPERCASE-method + requestPath + ('?' + queryString when present) + body`. Bitget timestamp is epoch-milliseconds (string), distinct from OKX's ISO format — handle that here. The query string is appended to the path with a literal `?` only when non-empty. Add `BitgetSigningRequest` marker.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs`
- `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs
- src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs` (base64 HMAC from TASK-009)
- `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs` (composite prehash builder reference from TASK-011)
- `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:37-41` (secret guard)

## Acceptance Criteria
1. `Sign(...)` returns base64 HMAC-SHA256 over `timestamp+UPPER(method)+requestPath[+'?'+query]+body` matching a known Bitget test vector.
2. The `?`+query segment is included only when the query string is non-empty; method is upper-cased; timestamp is epoch-ms string.
3. `BitgetSigningRequest.IsSigned`/`MarkSigned` round-trips and is idempotent across retries.

## Test Requirements
- Unit tests in TASK-022 assert the base64 vector and prehash assembly for GET (with/without query) and POST (body).

## Implementation Notes
- `internal sealed BitgetSignatureService : ISignatureService` (Core.Auth). `Sign(payload)` is `<inheritdoc />` and delegates to `HmacSignature.Compute(secret, payload, SignatureEncoding.Base64)` — no re-implemented crypto. Secret guarded via `InitializeSecretKey` (ThrowIfNullOrWhiteSpace), mirroring OKX/Bybit.
- Prehash shape (`BuildPrehash(timestamp, method, requestPath, queryString, body)`): `timestamp + UPPER(method) + requestPath + (queryString.Length > 0 ? "?" + queryString : "") + body`. Bitget delta vs OKX: query is appended to the path with a literal `?` only when non-empty (OKX folds query into requestPath); body empty for GET/DELETE. Identity inputs (timestamp/method/requestPath) guarded with ThrowIfNullOrWhiteSpace; queryString + body ThrowIfNull only (may be empty).
- Timestamp: epoch-MILLISECONDS string via `FormatTimestamp(DateTimeOffset) => ToUnixTimeMilliseconds().ToString(InvariantCulture)` (Bitget delta vs OKX ISO-8601).
- `Sign` returns the base64 signature; the handler places it in the `ACCESS-SIGN` header (not appended).
- `BitgetSigningRequest`: internal static marker mirroring OkxSigningRequest; `HttpRequestOptionsKey<bool>` keyed `"bitget.signed"`; MarkSigned/IsSigned idempotent.
- Lean comments per ADR-001 conv 7.

## Verification
- `dotnet build CryptoExchanges.Net.sln` → Build succeeded, 0 Warning(s), 0 Error(s). No new tests (arrive in TASK-022).

## Commits
- (pending — see below)
