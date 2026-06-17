# API Review: TASK-002 — BybitSignatureService + BybitSigningRequest

**Reviewer**: API Reviewer
**Task**: TASK-002 — BybitSignatureService + signing request marker
**Commit**: 5654d93
**Branch**: feat/m2-exchange-expansion
**Date**: 2026-06-17

## Files Reviewed

- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs`

## Pattern References Used

- `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs`
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs`

---

### Finding 1: Public visibility matches Binance pattern — no mismatch

- **Severity**: LOW
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:13`, `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:5`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: `BybitSignatureService` is `public sealed`; `BybitSigningRequest` is `public static`. The Binance equivalents are identically declared: `BinanceSignatureService` is `public sealed` (line 9), `BinanceSigningRequest` is `public static` (line 5), and `BinanceSigningHandler` is `public sealed` (line 12). Visibility is consistent. The `InternalsVisibleTo` grants in both `.csproj` files are for test and DI assemblies — these types being `public` is intentional and matches the Binance package's established pattern.
- **Fix**: None required.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:9`

---

### Finding 2: Static sign-string builders are public — unnecessarily wide API surface

- **Severity**: MEDIUM
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:37,50`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 72 < 80)
- **Issue**: `BuildGetSignString` and `BuildPostSignString` are `public static` on a `public sealed` class. Their only consumer in the repository will be the Bybit signing handler (a future task). Making them `public` exposes them as permanent library API. Any external caller can depend on these signatures, creating a constraint when OKX's signer generalization (planned in research doc) potentially wants to introduce a common `ISignStringBuilder` abstraction. Binance's equivalent `BuildSignedQuery` is also `public`, but it appends the signature and is legitimately user-facing; the Bybit builders produce only an intermediate sign-string that the signing handler should own internally.
- **Fix**: Consider making `BuildGetSignString` and `BuildPostSignString` `internal`. The signing handler (arriving in a later task) is the only consumer; external callers have no reason to build Bybit sign-strings. Unit tests (TASK-008) can still reach them via `InternalsVisibleTo`. Alternatively, if keeping `public`, document that these are protocol-level primitives and treat them as stable API from this point forward.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:30-35`

---

### Finding 3: `timestamp` and `recvWindow` typed as `string` rather than `long`

- **Severity**: MEDIUM
- **Confidence**: 78
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:37,50`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 78 < 80)
- **Issue**: `BuildGetSignString(string timestamp, string apiKey, string recvWindow, string queryString)` accepts `timestamp` and `recvWindow` as `string`. The Bybit protocol defines both as integers (Unix milliseconds and window milliseconds respectively). Accepting `string` pushes format validation to the caller — a malformed timestamp produces a valid HMAC that Bybit's server then rejects with an opaque timestamp-window error. Accepting `long timestamp` and `long recvWindow` and formatting via implicit `ToString()` in the interpolation would be type-safer with no change to the produced sign-string value. The task manifest does not mandate `string` types for these parameters.
- **Fix**: Change signatures to `BuildGetSignString(long timestamp, string apiKey, long recvWindow, string queryString)` and `BuildPostSignString(long timestamp, string apiKey, long recvWindow, string jsonBody)`. Since no signing handler yet consumes these, this is a non-breaking change within the current scope. If the methods remain `public`, make this change before the handler lands to avoid a future breaking API change.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:13` (handler uses `Func<long> timeOffset` — `long` is the native timestamp type in this codebase)

---

### Finding 4: Naming convention — fully consistent with Binance

- **Severity**: LOW
- **Confidence**: 99
- **File**: Both files
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `BybitSignatureService` / `BybitSigningRequest` follow the exact `{Exchange}SignatureService` / `{Exchange}SigningRequest` naming pattern. `MarkSigned` / `IsSigned` method names are identical. The options key string `"bybit.signed"` mirrors `"binance.signed"`. No deviation.
- **Fix**: None required.

---

### Finding 5: `sealed` modifier — appropriate and consistent

- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:13`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `sealed` is correct. The Binance equivalent is also `sealed`. The signing primitives are internal details not intended for extension. OKX's signer generalization will introduce a new type, not subclass this one. `sealed` prevents inheritance without preventing composition.
- **Fix**: None required.

---

### Finding 6: XML documentation — present on all public members

- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:6-12,17-27,29-40,42-53`; `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:3-5,9,12`
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: All public members have XML doc summaries and `<param>` / `<returns>` tags. `BybitSignatureService` has a class-level `<remarks>` explaining the X-BAPI-SIGN header placement. The unresolvable `<see cref>` to the not-yet-existing `BybitSigningHandler` is correctly replaced with plain text — the right call under `TreatWarningsAsErrors` (CS1574 would have been a build error). `GenerateDocumentationFile` is enabled globally via `Directory.Build.props:8`.
- **Fix**: None required. Upgrade plain text to `<see cref="BybitSigningHandler"/>` once the handler lands in a later task.

---

### Finding 7: OKX generalization — public surface may require a breaking change later

- **Severity**: LOW
- **Confidence**: 68
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:37,50`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 68 < 80)
- **Issue**: The research doc states OKX drives "pluggable per-exchange sign-string builder" generalization. If OKX introduces `ISignStringBuilder` or a delegate-based abstraction, the `public static BuildGetSignString` / `BuildPostSignString` on `BybitSignatureService` are already committed API. However, at `0.1.0-preview.1` breaking changes are acceptable with notice, and `BinanceSignatureService.BuildSignedQuery` has the same latent issue. This is a systemic question for the OKX task to resolve.
- **Fix**: No immediate action required. When the OKX task defines the signing abstraction, evaluate introducing an `IBybitSignStringBuilder` interface and having `BybitSignatureService` implement it, or leave these as static helpers with a separate abstraction type.

---

### Finding 8: `BybitSigningRequest` idempotency — correctly implemented

- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:10-21`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: `MarkSigned` sets the key to `true`. `IsSigned` reads it with `TryGetValue && v`. Calling `MarkSigned` twice is idempotent. Calling `IsSigned` on an unmarked request returns `false`. Acceptance criterion 3 is met. `ArgumentNullException.ThrowIfNull` guards on both methods match the Binance pattern exactly.
- **Fix**: None required.

---

### Finding 9: No new `InternalsVisibleTo` grants introduced

- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:16-22`
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: Existing grants for `CryptoExchanges.Net.Bybit.Tests.Integration` and `CryptoExchanges.Net.DependencyInjection` were already present. This task adds no new grants. No consumer application projects are granted visibility.
- **Fix**: None required.

---

### Finding 10: All acceptance criteria structurally satisfied

- **Severity**: LOW
- **Confidence**: 95
- **File**: Both files
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: AC1 — `Sign(string)` returns lowercase-hex via `Convert.ToHexStringLower` with `ArgumentException.ThrowIfNullOrWhiteSpace` on the secret key. AC2 — `BuildGetSignString` produces `timestamp+apiKey+recvWindow+queryString`; `BuildPostSignString` produces `timestamp+apiKey+recvWindow+jsonBody`; neither appends the signature. AC3 — `IsSigned`/`MarkSigned` are idempotent via `HttpRequestOptions` key. Test coverage is deferred to TASK-008 per the manifest — explicitly accepted arrangement.
- **Fix**: None required.

---

## Summary

- PASS: Visibility pattern — `public sealed` / `public static` matches Binance equivalents exactly
- PASS: Naming convention — all names follow `{Exchange}SignatureService` / `{Exchange}SigningRequest` pattern; key string `"bybit.signed"` mirrors `"binance.signed"`
- PASS: `sealed` modifier — appropriate, mirrors Binance, does not conflict with planned OKX generalization
- PASS: XML documentation — all public members documented; unresolvable cref handled correctly with plain text
- PASS: `InternalsVisibleTo` — no new grants, no regression
- PASS: Idempotency / IsSigned round-trip — correctly implemented
- PASS: Acceptance criteria — structurally met; test coverage properly deferred to TASK-008
- CONCERN: `BuildGetSignString` / `BuildPostSignString` are `public` — leaks an intermediate-step detail as stable API with no external consumer value (confidence: 72/100, non-blocking)
- CONCERN: `timestamp` and `recvWindow` parameters typed as `string` rather than `long` — foregoes type safety for protocol integer fields (confidence: 78/100, non-blocking)
- CONCERN: Public static sign-string builders may require a breaking change when OKX drives the signing abstraction generalization (confidence: 68/100, non-blocking)

---

## Final Verdict

**APPROVED**

The implementation is structurally sound, fully consistent with the Binance pattern reference, correctly implements all three acceptance criteria, passes the build, and introduces no breaking changes to existing public API. All three concerns are non-blocking (confidence below 80). The version is `0.1.0-preview.1` with no external consumers. The type-safety concern on `timestamp`/`recvWindow` is worth addressing before the signing handler lands in a later task to avoid a future breaking API change, but does not block this task.
