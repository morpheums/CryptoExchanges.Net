---
id: TASK-002
status: PLANNED
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
