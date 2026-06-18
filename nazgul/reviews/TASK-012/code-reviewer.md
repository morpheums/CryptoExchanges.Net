# Code Review — TASK-012: OkxSigningHandler

VERDICT: APPROVED

---

## Findings

### Finding: `request.RequestUri!.PathAndQuery` null-forgiving on line 52
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:52`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — confidence 55)
- **Issue**: `HttpRequestMessage.RequestUri` is theoretically nullable per the BCL contract. The `!` null-forgiving operator suppresses the nullable warning. In practice `ResignAsync` is only reached after `OkxSigningRequest.IsSigned(request)` returns true, which requires the caller to have set the option on a fully-constructed request. `OkxHttpClient` always sets a non-null URI before marking the request signed, so the null path is unreachable from the current call stack. However, a hypothetical future caller could mark a request signed before assigning a URI, producing a `NullReferenceException` with no helpful message.
- **Fix**: Either add a guard at the top of `ResignAsync` — `ArgumentNullException.ThrowIfNull(request.RequestUri, nameof(request) + "." + nameof(request.RequestUri))` — or at minimum convert the `!` to a null-coalescing throw: `request.RequestUri?.PathAndQuery ?? throw new InvalidOperationException("Request URI must be set before signing.")`. The `BybitSigningHandler` uses `request.RequestUri?.Query ?? string.Empty` (line 51) as a softer pattern, but for signing a missing URI is a genuine logic error, so a throw is more correct.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:51`

---

### Finding: Primary constructor string parameters lack entry-point guards (`apiKey`, `passphrase`, `signatureService`, `timeOffset`)
- **Severity**: MEDIUM
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:17-18`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — confidence 70)
- **Issue**: The project convention (enforced at HIGH severity) requires `ArgumentNullException.ThrowIfNull` on every reference-type parameter and `ArgumentException.ThrowIfNullOrWhiteSpace` on every non-empty string at the point of entry. The primary constructor parameters `apiKey` (string), `passphrase` (string), `signatureService` (OkxSignatureService), and `timeOffset` (Func<long>) receive no validation in the constructor body. The validation for `apiKey` and `passphrase` is deferred to `ResignAsync`, which is the signing path only — a handler constructed with a null `signatureService` or null `timeOffset` would throw a `NullReferenceException` at line 47 or 63 rather than an `ArgumentNullException` at construction time.
- **Fix**: Add a constructor body (or use an init method pattern as done in `OkxSignatureService.InitializeSecretKey`) that validates all parameters:
  ```csharp
  internal sealed class OkxSigningHandler(
      string apiKey, string passphrase, OkxSignatureService signatureService, Func<long> timeOffset)
      : DelegatingHandler
  {
      private readonly string _apiKey = ValidateApiKey(apiKey);
      private readonly string _passphrase = ValidatePassphrase(passphrase);
      private readonly OkxSignatureService _signatureService = signatureService ?? throw new ArgumentNullException(nameof(signatureService));
      private readonly Func<long> _timeOffset = timeOffset ?? throw new ArgumentNullException(nameof(timeOffset));

      private static string ValidateApiKey(string v) { ArgumentException.ThrowIfNullOrWhiteSpace(v); return v; }
      private static string ValidatePassphrase(string v) { ArgumentException.ThrowIfNullOrWhiteSpace(v); return v; }
  ```
  The deferred `string.IsNullOrEmpty` check in `ResignAsync` (lines 38–43) could then be removed as redundant with construction-time validation. Confidence is 70 (non-blocking) because the Bybit counterpart (`BybitSigningHandler`) follows the same deferred-check approach for `apiKey`, and the composer's guard prevents a null `signatureService` from ever being injected, so the practical risk is low.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:62-65` (InitializeSecretKey pattern)

---

### Finding: `HttpContent.ReadAsStringAsync` buffering safety on retry
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:59`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: PASS — safe as implemented. `OkxHttpClient.PostAsync` uses `new StringContent(json, Encoding.UTF8, "application/json")` (line 59 of `OkxHttpClient.cs`). `StringContent` wraps an in-memory `MemoryStream` that is seekable and fully buffered. `ReadAsStringAsync` on a `StringContent` can be called multiple times with no stream-position problem. This is distinct from a non-buffered stream-based `HttpContent` subclass where a second read would return empty. The retry-then-resign path is therefore safe.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs:59`

---

### Finding: Header re-sign atomicity (strip-then-add pattern)
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:67-74`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: PASS — correct and consistent with established Bybit pattern. All four `OK-ACCESS-*` headers are removed before any are added, guaranteeing exactly one set on every attempt including retries. Order of remove-then-add for all four is identical to `BybitSigningHandler:59-64`.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:59-64`

---

### Finding: ConfigureAwait(false) on all awaits
- **Severity**: HIGH
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:28,30,59`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: PASS — all three `await` expressions carry `.ConfigureAwait(false)`.

---

### Finding: CancellationToken forwarding
- **Severity**: HIGH
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:22-31`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: PASS — `cancellationToken` is forwarded to both `ResignAsync` (as `ct`) and `base.SendAsync`. `ReadAsStringAsync(ct)` on line 59 also forwards the token correctly.

---

### Finding: `HttpMethod` comparison for body verbs
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:56`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: PASS. `HttpMethod` uses reference equality optimization via a static singleton cache for standard verbs; `HttpMethod.Post` and `HttpMethod.Put` are singleton instances, making `==` safe and idiomatic. `HttpMethod.Delete`, used by `OkxHttpClient.DeleteAsync`, correctly falls through to the `string.Empty` body path. OKX V5 uses DELETE with query-string (no body), and the `OkxHttpClient` does not use `HttpMethod.Patch`, so the `Post || Put` guard covers all body-bearing verbs.

---

### Finding: XML documentation completeness
- **Severity**: MEDIUM
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:5-16`
- **Category**: Code Style
- **Verdict**: PASS
- **Issue**: PASS. Class-level `<summary>` is thorough. All four primary constructor parameters have `<param>` docs. `SendAsync` uses `<inheritdoc />` (correct for an override). `ResignAsync` is private so no doc is required.

---

### Finding: Build clean (TreatWarningsAsErrors)
- **Severity**: HIGH
- **Confidence**: 99
- **File**: N/A
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: PASS. `dotnet build CryptoExchanges.Net.sln` produces `0 Warning(s), 0 Error(s)`.

---

### Finding: Unit tests for OkxSigningHandler
- **Severity**: MEDIUM
- **Confidence**: 75
- **File**: N/A (missing test file)
- **Category**: Testing
- **Verdict**: CONCERN (non-blocking — confidence 75)
- **Issue**: There is no `CryptoExchanges.Net.Okx.Tests.Unit` project and no test covering `OkxSigningHandler`. The Bybit counterpart has `BybitSigningTests.cs` covering HMAC vectors, sign-string assembly, retry header idempotency, and credential rejection. The OKX implementation is structurally analogous and introduces its own signing logic (`OkxSignatureService.BuildPrehash`, `FormatTimestamp`, base64 encoding). At minimum the following should be covered: (1) `ResignAsync` produces correct `OK-ACCESS-SIGN` header for a fixed prehash vector; (2) retry does not double headers; (3) unsigned requests pass through without auth headers; (4) `string.IsNullOrEmpty(apiKey)` guard path throws `InvalidOperationException`. The Nazgul rule (Rule 4) requires tests for every task.
- **Pattern reference**: `tests/CryptoExchanges.Net.Bybit.Tests.Unit/BybitSigningTests.cs`

---

### Finding: `sealed` class, primary constructor, `internal` visibility
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:17-19`
- **Category**: Code Style
- **Verdict**: PASS
- **Issue**: PASS. `internal sealed class` with a primary constructor matches the Bybit pattern exactly. No `public` mutable fields.

---

## Summary

- PASS: Build clean (0 warnings, 0 errors with TreatWarningsAsErrors=true)
- PASS: ConfigureAwait(false) on all three await sites
- PASS: CancellationToken forwarded to all awaits including ReadAsStringAsync
- PASS: Header strip-then-add pattern is correct; no header doubling on retry
- PASS: StringContent body is safely re-readable on retry (in-memory, seekable)
- PASS: HttpMethod comparison is correct; Delete correctly takes the empty-body path
- PASS: XML docs present on class and all constructor parameters; inheritdoc on override
- PASS: sealed/internal/primary-constructor style matches BybitSigningHandler exactly
- CONCERN: `request.RequestUri!` null-forgiving suppression — consider a throw-on-null guard instead of silent `!` (confidence: 55/100, non-blocking)
- CONCERN: Primary constructor parameters (`apiKey`, `passphrase`, `signatureService`, `timeOffset`) not validated at construction time — validation deferred to `ResignAsync` for strings, missing entirely for reference types; follow `OkxSignatureService.InitializeSecretKey` pattern (confidence: 70/100, non-blocking)
- CONCERN: No OKX signing unit test project exists — Nazgul Rule 4 mandates tests per task; HMAC vector tests, header idempotency on retry, and guard paths should be covered (confidence: 75/100, non-blocking)

---

## Final Verdict

VERDICT: APPROVED

All findings are below the blocking confidence threshold of 80 for HIGH/MEDIUM severity. The three CONCERN items are non-blocking improvements. The implementation is structurally sound, follows the Bybit pattern faithfully, and compiles clean with zero warnings under `TreatWarningsAsErrors=true`.
