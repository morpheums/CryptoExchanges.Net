---
id: TASK-009
status: PLANNED
---

# TASK-009: OKX-era credential/signing generalization (Core/Http)

**Milestone**: M-OKX
**Wave**: 6
**Group**: 6
**Status**: PLANNED
**Depends on**: TASK-008
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#architectural-implication (the signature abstraction generalizes after Bybit, against OKX: pluggable sign-string builder + base64-vs-hex output + optional passphrase credential + header-based signature)
**Blast radius**: HIGH — touches shared Core abstractions and Http conventions. MUST NOT break Binance or Bybit. REQUIRES architect-reviewer + security-reviewer + api-reviewer.

## Description
Introduce the minimal shared abstraction that OKX (and later Bitget) need WITHOUT disturbing Binance/Bybit:
1. A passphrase-capable credential model in Core — `ExchangeCredentials` (ApiKey, SecretKey, optional Passphrase) — additive, no change to existing options.
2. A signature-encoding helper in Core supporting both **hex** (`Convert.ToHexStringLower`) and **base64** (`Convert.ToBase64String`) output over an HMAC-SHA256 hash, so per-exchange signature services select encoding without re-implementing the primitive.
3. Confirm the Http `requestFinalizerFactory` seam (`Func<IServiceProvider, DelegatingHandler>`) already supports header-based signers (it does — it is exchange-agnostic). Add only documentation/no-op shared scaffolding if a common header-signing base is warranted; do NOT force Binance/Bybit onto it.

This task is deliberately small and Binance-safe: existing `BinanceSignatureService` (hex) and `BybitSignatureService` (hex, header) continue to compile and pass unchanged. OKX consumes the new credential model + base64 encoder.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs`
- `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs`
### Modifies
- (none — Binance/Bybit left untouched to prove non-breaking generalization)

## Files modified
- src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs
- src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:18-23` (HMAC-SHA256 primitive, hex output to generalize to hex|base64)
- `src/CryptoExchanges.Net.Http/ResilientHttpClientServiceCollectionExtensions.cs:47-70` (generic `requestFinalizerFactory` seam — confirm it accepts header-based signers)
- `src/CryptoExchanges.Net.Core/Models/SymbolFormat.cs` (existing additive Core value-type pattern + casing)

## Acceptance Criteria
1. `SignatureEncoding` produces lowercase-hex output byte-identical to `BinanceSignatureService.Sign` for the same key+payload, AND a correct base64 output for the same hash (verified against a known vector).
2. `ExchangeCredentials` is a public Core type with optional `Passphrase`; existing `BinanceOptions`/`BybitOptions` and their tests compile and pass UNCHANGED (no Binance/Bybit edits in this task).
3. Full solution builds clean under TreatWarningsAsErrors; no public Binance/Bybit API surface changes (api-reviewer confirms additive-only Core surface).

## Test Requirements
- Core unit tests: hex output equals Binance's for shared vectors; base64 output matches an independent reference; `ExchangeCredentials` carries/omits passphrase correctly. Add to `CryptoExchanges.Net.Core.Tests.Unit`.
