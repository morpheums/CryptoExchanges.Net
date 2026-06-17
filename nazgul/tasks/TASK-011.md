---
id: TASK-011
status: PLANNED
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
