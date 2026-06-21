---
verdict: APPROVE
---

# Security Review — TASK-060: AddKucoinExchange DI + AddCryptoExchanges + MCP wiring

## Diff reviewed
`nazgul/reviews/TASK-060/diff.patch`

---

## Credential Safety

### Finding: SecretKey stored only inside KucoinSignatureService, never in a field on the handler
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:67`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `o.SecretKey` is passed directly to `new KucoinSignatureService(o.SecretKey)` where it is held in `_secretKey` as a private field inside the signing service. It is never stored on `KucoinSigningHandler` itself as a field, so the handler cannot accidentally expose it via reflection or ToString.
- **Pattern reference**: `src/CryptoExchanges.Net.Kucoin/Auth/KucoinSignatureService.cs:15` (`private readonly string _secretKey`)

### Finding: No credential logging in KucoinSigningHandler
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. The handler contains no ILogger injection, no Console/Debug writes, and no exception message that includes a credential value. The two `InvalidOperationException` messages at lines 43–47 say only that a key/passphrase is missing — they do not echo back the value.

### Finding: No credential logging in ApplyEnvDefaults
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:179-187`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `ApplyEnvDefaults` reads the three env vars and assigns them to options properties only if non-empty. No logging, no exception messages containing values.

### Finding: CryptoExchangesOptions new credential fields are nullable, no serialization attributes
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.DependencyInjection/CryptoExchangesOptions.cs:52-61`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `KucoinApiKey`, `KucoinSecretKey`, and `KucoinPassphrase` are all `string?` (nullable), no `[JsonInclude]`, no `[JsonPropertyName]`, no `[DataMember]`. No `ToString()` override on the class — consistent with the existing pattern for all other exchanges in this file.

### Finding: KucoinOptions lacks a ToString() override
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Kucoin/KucoinOptions.cs`
- **Category**: Security
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `KucoinOptions` (which is also `CryptoExchangesOptions`) does not override `ToString()` to redact its `SecretKey` and `Passphrase` properties. If a caller (or a DI framework diagnostic) calls `ToString()` on the options object, `record`-style auto-members would expose raw credential strings. The class is `sealed class` (not a `record`), so the default `Object.ToString()` does NOT enumerate properties — it just returns the type name. Risk is therefore low, but the review checklist flags this pattern. All existing exchange options classes (`BinanceOptions`, `BitgetOptions`, etc.) also lack an override, so this is a pre-existing pattern gap and not introduced by this task.
- **Fix**: Consider adding `public override string ToString() => $"KucoinOptions {{ ApiKey = {ApiKey[..Math.Min(4,ApiKey.Length)]}***, BaseUrl = {BaseUrl} }}"` in a follow-up hardening task. Not blocking for this task.
- **Pattern reference**: No existing precedent in the codebase (all Options classes lack this override).

---

## Signing Integrity

### Finding: Passphrase-v2 — signed passphrase transmitted, not plaintext
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:70,82`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `signatureService.SignPassphrase(passphrase)` HMAC-SHA256-signs the raw passphrase, and the result (`signedPassphrase`) is set as the `KC-API-PASSPHRASE` header. The plaintext passphrase is never transmitted over the wire.

### Finding: Mark-and-strip pattern implemented for KC-API headers on retry
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:74-83`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. Lines 74-78 remove all five KC-API headers before adding fresh ones, preventing duplicate headers on retry. Mirrors the Binance mark-and-strip pattern exactly.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs` (MarkSigned/strip pattern)

### Finding: PassThrough gate correctly requires BOTH SecretKey AND Passphrase
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:61-68`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. The gate `string.IsNullOrEmpty(o.SecretKey) || string.IsNullOrEmpty(o.Passphrase)` — using `||` — means if EITHER credential is missing, the client falls back to `PassThroughHandler`. This prevents partial-credential signing (e.g., secret key present but passphrase absent). The `KucoinSignatureService` constructor would throw on an empty secret, and `KucoinSigningHandler.ResignAsync` would throw on an empty passphrase — so even if the gate were bypassed, it would fail safe. Defense-in-depth is sound.

### Finding: ToCredentials() bypass is deliberate and safe
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:39-40`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. The comment documents explicitly why `ToCredentials()` is bypassed: it throws when the passphrase is empty, which would break secretless (public market data) registrations. The DI factory reads `o.SecretKey` directly and only creates a `KucoinSignatureService` after the both-or-nothing gate passes. Consistent with the intent.

---

## Query String Safety

No new query string construction is introduced in the changed files (the diff only covers DI wiring and test files). Query string construction remains in previously reviewed KuCoin service files.

---

## Input Validation

### Finding: ApiKey is passed as empty string when signing is active but ApiKey not set
- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:65`
- **Category**: Security
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: The gate at line 61 checks only `SecretKey` and `Passphrase`. If a caller sets `SecretKey` and `Passphrase` but omits `ApiKey`, the code constructs a `KucoinSigningHandler` with `apiKey = ""`. The handler guards this at line 42-44 with an `InvalidOperationException` at request time — so it fails safe — but the error surfaces at runtime (first signed request) rather than at DI resolution time. This is the same pattern used by the existing Bybit/OKX/Bitget exchanges so it is not a regression introduced here.
- **Fix**: Add `ApiKey` to the gate check (`string.IsNullOrEmpty(o.ApiKey) || ...`) or add it to options validation. Low priority, follow-up task.
- **Pattern reference**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:42-44`

---

## Secret Management Expansion

### Finding: Env vars KUCOIN_API_KEY / KUCOIN_SECRET_KEY / KUCOIN_PASSPHRASE follow established pattern
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs:179-187`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. Env var names follow the `{EXCHANGE}_{CREDENTIAL}` convention already used for Binance, Bybit, OKX, and Bitget. Values are read with `GetEnvironmentVariable` and assigned only when non-empty. No logging.

---

## Rate Limiting

### Finding: ReactiveRateLimitGate registered via KucoinClientComposer
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Internal/KucoinClientComposer.cs:82`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `KucoinClientComposer.ComposeForDi` (called from the DI `exchangeClientFactory` at line 177) registers a `ReactiveRateLimitGate`, consistent with other exchange registrations.

### Finding: KucoinErrorTranslator classifies HTTP 429 as RateLimitExceededException
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinErrorTranslator.cs:35-37`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. HTTP 429 is correctly translated to `RateLimitExceededException`.

---

## JSON Deserialization Safety

No new `JsonDocument.Parse()` or `ReadFromJsonAsync` calls are introduced in the diff. The changed files are DI wiring and test code only.

---

## No Opsec Leakage

No exchange strategy, roadmap, or competitive information detected in XML docs, comments, or commit messages visible in the diff.

---

## Summary

- PASS: Credential safety — SecretKey held only in `KucoinSignatureService._secretKey`; never logged, serialized, or echoed in exceptions.
- PASS: Passphrase wire transmission — `KC-API-PASSPHRASE` header carries the HMAC-signed passphrase, not plaintext.
- PASS: Mark-and-strip pattern — KC-API headers stripped before re-adding on every attempt.
- PASS: PassThrough gate — both SecretKey AND Passphrase required to activate signing; missing either falls back to `PassThroughHandler`.
- PASS: `ToCredentials()` bypass — documented and safe; avoids throw-on-empty-passphrase during secretless DI resolution.
- PASS: Env var handling — reads `KUCOIN_API_KEY` / `KUCOIN_SECRET_KEY` / `KUCOIN_PASSPHRASE` without logging.
- PASS: `CryptoExchangesOptions` new fields — nullable `string?`, no JSON attributes, no serialization path.
- PASS: Rate limiting — `ReactiveRateLimitGate` registered; `KucoinErrorTranslator` maps 429 to `RateLimitExceededException`.
- PASS: No opsec leakage in diff.
- CONCERN: `KucoinOptions` lacks a `ToString()` override to redact secrets (confidence: 55/100, non-blocking) — pre-existing pattern gap, not introduced by this task.
- CONCERN: Empty `ApiKey` with non-empty `SecretKey`+`Passphrase` constructs a signing handler that fails at request time rather than DI resolution time (confidence: 60/100, non-blocking) — same pattern as existing exchanges.

## Final Verdict

`APPROVED` — No blocking security issues. Both concerns are non-blocking, pre-existing pattern gaps shared with all other exchange integrations, and the signing pipeline is correctly implemented with proper mark-and-strip, PassThrough gate, and HMAC-signed passphrase transmission.
