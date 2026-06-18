---
id: TASK-009
status: DONE
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

## Implementation Notes

### Created
- `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs` — public `sealed record ExchangeCredentials`.
- `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs` — public `enum SignatureEncoding { Hex, Base64 }` + public `static class HmacSignature`.
- `tests/CryptoExchanges.Net.Core.Tests.Unit/AuthTests.cs` — 24 new Core unit tests (`HmacSignatureTests`, `ExchangeCredentialsTests`).

### API shape
- `ExchangeCredentials(string apiKey, string secretKey, string? passphrase = null)` with `ApiKey`, `SecretKey`, `Passphrase` (nullable) and convenience `bool HasPassphrase`. Guards: `ArgumentException.ThrowIfNullOrWhiteSpace` on apiKey/secretKey; passphrase, when non-null, must also be non-whitespace (null is the valid "absent" state for Binance/Bybit).
- `HmacSignature.Compute(string secret, string payload, SignatureEncoding encoding) -> string`. Single HMAC-SHA256 primitive; switches on encoding to `Convert.ToHexStringLower` or `Convert.ToBase64String`. Guards secret/payload via `ThrowIfNullOrWhiteSpace`; undefined enum -> `ArgumentOutOfRangeException`.

### SignatureEncoding design choice (and why)
Chose an **enum + static `HmacSignature.Compute`** over per-method `SignHex`/`SignBase64` so there is exactly ONE HMAC primitive and the output form is a first-class, named parameter that per-exchange signature services pass through (Binance/Bybit -> `Hex`, OKX/Bitget -> `Base64`). This keeps the hash uncopied and makes the hex/base64 difference the only axis of variation, matching the research note ("base64-vs-hex output" as a pluggable knob). Kept it a static helper (no instance state, no global mutable state) mirroring how the existing services hold only the immutable secret.

### ToString redaction approach
`ExchangeCredentials.ToString()` is overridden to fully replace the record's synthesized printer (so the auto-generated `PrintMembers` — which would emit every property including the secret — is never reached). Output: `ExchangeCredentials { ApiKey = ****<last4>, SecretKey = [REDACTED], Passphrase = [REDACTED]|(none) }`. SecretKey and Passphrase values are never rendered; ApiKey is masked to its last four chars (fully `****` when <= 4 chars). Documented in XML remarks referencing ADR-001.

### Http seam confirmation (no change needed)
Read `src/CryptoExchanges.Net.Http/ResilientHttpClientServiceCollectionExtensions.cs`. The `requestFinalizerFactory: Func<IServiceProvider, DelegatingHandler>?` is exchange-agnostic and already inserts an arbitrary signing handler into the pipeline (Bybit's header-based `X-BAPI-SIGN` signer uses exactly this seam today). A base64 + passphrase + header signer for OKX needs no new Http code — it is just another `DelegatingHandler` supplied through the same factory. No Http file was modified.

### hex == Binance verification
`HmacSignature.Compute("secret", "hello", Hex)` returns `88aab3ede8d3adf94d26ab90d3bafd4a2083070c3bcce9c014ee04a443847c0b` — the exact pinned vector asserted by the existing Binance and Bybit signing tests (`HMAC-SHA256("hello", key="secret")`), and independently re-derived in-test via `HMACSHA256.HashData(...) + Convert.ToHexStringLower` (the identical primitive `BinanceSignatureService.Sign` uses). Base64 of the same hash is pinned to `iKqz7ejTrflNJquQ07r9SiCDBww7zOnAFO4EpEOEfAs=` (independently computed) and asserted to share the same underlying bytes as the hex output. (Core tests cannot reference `BinanceSignatureService` — Core.Tests.Unit references Core only — so equality is proven against the shared pinned vector + the raw primitive rather than by constructing the Binance type.)

### Verification output
- `dotnet build CryptoExchanges.Net.sln` -> Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test --filter 'Category!=Integration'` -> all pass: Core 92 (was 68; +24), Http 12, DI.Unit 10, Bybit.Unit 80, Binance.Integration 45 (untagged, ran and passed -> Binance non-breaking confirmed).
- `git status` (excluding bin/obj) shows ONLY `src/CryptoExchanges.Net.Core/Auth/` + `tests/CryptoExchanges.Net.Core.Tests.Unit/AuthTests.cs` — no Binance/Bybit/Http/DI source touched.

## Commits
- **Commit**: 63b0006 feat(M3): TASK-009 Core auth generalization — ExchangeCredentials + HmacSignature

## Review
- **Review Gate**: PASSED (round 1) — all 4 APPROVE: architect 97, security 99, api 96, code PASS. No blocking items.
- **Verified**: additive/non-breaking (zero Binance/Bybit/Http/DI edits, conf 100); HMAC via BCL (no homemade crypto, hex==Binance vector); ToString fully suppresses synthesized PrintMembers — secrets never rendered (security 99); Http finalizer seam hosts OKX base64+passphrase+header signer with no Http change.
- **Polish applied post-gate (non-blocking CONCERNs @90/@85)**: tightened 4 `<param>` docs ("non-empty" → "non-null, non-empty, and non-whitespace"); added the exactly-4-char ApiKey masking boundary test (Core 92→93). Build 0w/0e; all pass.
- **Deferred (non-blocking)**: record Equals compares secrets by value (api @65, not in threat model — FixedTimeEquals is the upgrade path if raised).
