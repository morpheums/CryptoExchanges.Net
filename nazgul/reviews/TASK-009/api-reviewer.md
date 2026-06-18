# API Review — TASK-009: OKX-era credential/signing generalization (Core/Auth)

**Reviewer**: api-reviewer  
**Commit**: 63b0006  
**Branch**: feat/m3-okx  
**Date**: 2026-06-18  

---

## Findings

### Finding 1: Additive-only surface — no existing types modified
- **Severity**: N/A
- **Confidence**: 100
- **File**: commit 63b0006 (--name-status)
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: The commit shows `A` (added) for all three source files and `M` for the task manifest only. No Binance, Bybit, Http, or DI source file is touched. All existing interfaces in `src/CryptoExchanges.Net.Core/Interfaces/` are unchanged. No existing models, enums, or records are modified.

---

### Finding 2: `ExchangeCredentials` — shape and safety
- **Severity**: N/A
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:13-65`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `sealed record` with `{ get; }` (not `init`) on all three properties. `with` expressions cannot be used to bypass the constructor guards. Passphrase is `string?` with `null` as the canonical "absent" value, properly guarded against non-null whitespace. `HasPassphrase` is a clean convenience member. `ToString()` correctly overrides the synthesized printer before `PrintMembers` can expose raw secrets, consistent with ADR-001.

---

### Finding 3: `HmacSignature.Compute` — shape and extensibility
- **Severity**: N/A
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs:38-53`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: Static helper with a single HMAC-SHA256 primitive and an enum selector is the correct call. No instance state, no mutable global state, pure deterministic function. Per-exchange signature services (injectable) wrap this primitive and select their encoding. `SignatureEncoding` enum with `Hex`/`Base64` covers the actual axis of variation. The `_` arm throwing `ArgumentOutOfRangeException` future-proofs against undefined values, matching the `OrderStatus.Unknown` pattern in `src/CryptoExchanges.Net.Core/Enums/Enums.cs:51`.

---

### Finding 4: OKX/Bitget forward-compatibility of the contract
- **Severity**: N/A
- **Confidence**: 93
- **File**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:25`, `SignatureEncoding.cs:10-17`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: OKX requires API-KEY, API-SECRET, API-PASSPHRASE, and Base64(HMAC-SHA256(...)) — all three credential fields and the `Base64` encoding path are present. Passphrase default of `null` means Binance and Bybit require zero changes. `HasPassphrase` correctly guides OKX sign-string builders to include the passphrase header. If a future exchange requires HMAC-SHA384/RSA, a peer `EcSignature`/`RsaSignature` static class in the same namespace is the correct path; `HmacSignature` name signals scope clearly and does not foreclose that.

---

### Finding 5: XML doc minor inaccuracy — param summaries vs actual guard behavior
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs:32-33`; `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:30-31`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 90/100)
- **Issue**: The `<param name="secret">` and `<param name="payload">` summaries in `HmacSignature.Compute` read "Must be non-empty." The actual guard is `ThrowIfNullOrWhiteSpace`, which also rejects whitespace-only strings. The `<exception cref="ArgumentException">` element correctly says "null/empty/whitespace", creating an inconsistency between the param and exception docs. Same inconsistency in `ExchangeCredentials.cs` for `apiKey` and `secretKey` param summaries. The `passphrase` param doc is correctly worded ("when supplied it must be non-whitespace") and serves as the correct pattern.
- **Fix**: Change "Must be non-empty." to "Must be non-null, non-empty, and non-whitespace." on `secret`, `payload`, `apiKey`, and `secretKey` param docs. Four one-line edits across two files.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:34` (passphrase param doc has correct wording)

---

### Finding 6: Namespace consistency
- **Severity**: N/A
- **Confidence**: 92
- **File**: `src/CryptoExchanges.Net.Core/Auth/`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: All existing Core subdirectories follow `CryptoExchanges.Net.Core.<Subdirectory>` (.Enums, .Models, .Exceptions, .Resilience, .Interfaces). The new `.Auth` namespace is consistent with this pattern.

---

### Finding 7: `with`-expression bypass protection
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:16-25`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: All three properties are `{ get; }` without `init`. C# `with` expressions on records require `init` setters. The compiler blocks `creds with { SecretKey = "bad" }` at compile time, so construction guards are unbypassable. Correct choice for an immutable credential value object.

---

### Finding 8: Record `Equals`/`GetHashCode` exposes secrets in plaintext comparison
- **Severity**: LOW
- **Confidence**: 65
- **File**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:13`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 65/100, below REJECT threshold)
- **Issue**: Synthesized `record` `Equals`/`GetHashCode` compare `SecretKey` and `Passphrase` by value using `string.Equals`/`string.GetHashCode`. In a timing-sensitive security context this could theoretically be a side channel. In practice `ExchangeCredentials` is a configuration object, not compared in authentication-critical paths. Not an exploitable concern in this SDK's threat model.
- **Fix**: None required for this version. If raised in future, `IEquatable<ExchangeCredentials>` with `CryptographicOperations.FixedTimeEquals` on the secret bytes is the correct upgrade path.

---

### Finding 9: Test coverage quality
- **Severity**: N/A
- **Confidence**: 98
- **File**: `tests/CryptoExchanges.Net.Core.Tests.Unit/AuthTests.cs`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: 24 test cases covering: pinned hex vector (Binance/Bybit compatibility), independent re-derivation of raw primitive, base64 reference vector, cross-encoding same-hash assertion, blank-secret/blank-payload/undefined-encoding guards, credential construction without/with passphrase, all blank-apikey/blank-secret/blank-passphrase guard combinations, `ToString` redaction of secret and passphrase, last-four masking of API key, and short-key full masking. All acceptance criteria asserted. Test file correctly uses only `CryptoExchanges.Net.Core.Auth` namespace without referencing `BinanceSignatureService`.

---

## Summary

- PASS: Additive-only surface — zero modifications to any existing public type, interface, model, or enum
- PASS: `ExchangeCredentials` shape — sealed record, `{ get; }` guards, passphrase correctly optional, `ToString` redaction correct per ADR-001
- PASS: `HmacSignature.Compute` — static utility is the correct design choice; enum encoding selector covers Binance/Bybit/OKX/Bitget axis without over-engineering
- PASS: OKX/Bitget forward-compatibility — passphrase default-null, Base64 path present, `HasPassphrase` guides header construction
- PASS: Namespace follows `CryptoExchanges.Net.Core.<Subdirectory>` pattern exactly
- PASS: `with`-expression bypass blocked at compile time by `{ get; }` properties
- PASS: Test coverage — 24 cases, all acceptance criteria asserted, correct project boundary
- CONCERN: XML param doc says "Must be non-empty" where guard is `ThrowIfNullOrWhiteSpace` (also rejects whitespace-only) — four one-line doc fixes in two files (confidence: 90/100, non-blocking)
- CONCERN: Record `Equals`/`GetHashCode` compares secrets by value — acceptable for a configuration type, not an exploitable side channel in this SDK (confidence: 65/100, non-blocking, no fix required)

---

## Final Verdict

APPROVED (confidence: 96/100)

The change is cleanly additive, non-breaking, correctly shaped for its stated purpose, and well-tested. The only finding worth a follow-up is the four-line XML doc inconsistency (param summary says "non-empty" where the guard is "non-whitespace") — that is a documentation polish item, not a blocking API issue. No existing public surface was touched. The static `HmacSignature` helper design is justified and appropriate for a pure cryptographic primitive in a published SDK.
