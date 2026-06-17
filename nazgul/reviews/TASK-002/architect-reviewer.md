# Architect Review — TASK-002: BybitSignatureService + BybitSigningRequest

**Reviewer**: architect-reviewer
**Commit**: 5654d93
**Branch**: feat/m2-exchange-expansion
**Date**: 2026-06-17

## Files Reviewed
- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs`

## Pattern References Consulted
- `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs`
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs`
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs`
- `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs`
- `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs`
- `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj`

---

### Finding 1: Access modifiers are `public` — architectural invariant #3 says exchange internals should be `internal`
- **Severity**: MEDIUM
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:13` / `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:5`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence below 80)
- **Issue**: Both `BybitSignatureService` and `BybitSigningRequest` are declared `public`. Architectural invariant #3 states that only `BybitExchangeClient` and `BybitOptions` are public; "all DTOs, HTTP wrappers, services, and handlers are `internal`." A signature service and signing-request marker are internal plumbing, not part of the public surface. The Binance pattern (the stated reference) also declares `BinanceSignatureService` and `BinanceSigningRequest` as `public`, but that is arguably a precedent that itself violates invariant #3 — the DI project consumes these types directly (`ServiceCollectionExtensions.cs:100`), which is permitted only because `InternalsVisibleTo` covers `CryptoExchanges.Net.DependencyInjection`. The correct approach would be `internal` on both types, relying on `InternalsVisibleTo` (already declared in `CryptoExchanges.Net.Bybit.csproj:18-21`) for the DI and test assemblies.
- **Fix**: Declare both types `internal sealed` / `internal static` in the Bybit project. The existing `InternalsVisibleTo` grants in the csproj already allow the DI project and test assembly to access them. This mirrors the proper application of invariant #3, even if the Binance equivalents happen to be `public`.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs:7` (internal static), `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:8` (internal static)

---

### Finding 2: `BuildGetSignString` and `BuildPostSignString` lack null/empty guards on their parameters
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:37-53`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence below 80)
- **Issue**: The static sign-string builders silently accept `null` for any of their four parameters. A `null` `timestamp` or `apiKey` produces a malformed sign-string that will silently generate an invalid HMAC and surface as an opaque 403 from the Bybit API. The `Sign(string)` method has no null-guard either, though `Encoding.UTF8.GetBytes` would throw a NullReferenceException rather than giving a diagnostic. Binance's `Sign` also lacks explicit guards here, but the Bybit builders are new surface being introduced and would benefit from `ArgumentNullException.ThrowIfNull` (or at minimum `ArgumentException.ThrowIfNullOrEmpty`) on `timestamp` and `apiKey`, which are never legitimately empty for a live request.
- **Fix**: Add `ArgumentException.ThrowIfNullOrEmpty(timestamp)`, `ArgumentException.ThrowIfNullOrEmpty(apiKey)` at the top of each builder method. `recvWindow` and `queryString`/`jsonBody` may be empty by contract (e.g. GET with no params), so leave those as-is.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:57` (`InitializeSecretKey` shows the existing guard style)

---

### Finding 3: Documented deviation — plain-text `cref` replacement is acceptable
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:3-4`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: The summary uses "the Bybit signing handler" instead of `<see cref="BybitSigningHandler"/>` because the handler does not exist yet and `TreatWarningsAsErrors` would fail the build on CS1574. This is explicitly documented in the manifest (Implementation Notes, Deviation section).
- **Fix**: None required. Upgrade to `<see cref="BybitSigningHandler"/>` when the signing handler task lands.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs:4`

---

### Finding 4: Layering / dependency direction
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: Both new files live in `CryptoExchanges.Net.Bybit.*` namespaces, with no cross-layer `using` imports. The csproj references only `Core` and `Http` — identical to the Binance project. No upward dependency on DI was introduced. The only `using` directives in the new files are `System.Security.Cryptography` and `System.Text` (both BCL). `HttpRequestMessage` and `HttpRequestOptionsKey<T>` are from `System.Net.Http` (BCL), not from the Http project.
- **Fix**: None.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:11-14`

---

### Finding 5: Blast radius is correctly scoped
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`, `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: The diff touches exactly the two new files plus the task manifest. No modifications to Core, Http, DI, or Binance. The solution builds with 0 warnings and 0 errors under `TreatWarningsAsErrors`. Global usings in the Bybit project do not leak these types anywhere. Blast radius is LOW as declared.
- **Fix**: None.

---

### Finding 6: HMAC primitive and hex encoding match acceptance criteria
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:24-27`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: `HMACSHA256.HashData(_secretKeyBytes, signBytes)` + `Convert.ToHexStringLower(hash)` exactly mirrors the Binance primitive. The signature is returned, not appended, which is the correct Bybit behavior (signature goes into `X-BAPI-SIGN` header, not query string). The `InitializeSecretKey` guard pattern is identical.
- **Fix**: None.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:20-22`

---

### Finding 7: `BybitSigningRequest` option key is distinct and idempotent
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:7`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: The key string `"bybit.signed"` is unique and distinct from `"binance.signed"`, preventing cross-exchange contamination on shared pipelines. `MarkSigned` is idempotent (sets the same bool to `true` on repeat calls). `IsSigned` uses `TryGetValue` correctly. `ArgumentNullException.ThrowIfNull` guards are on both methods.
- **Fix**: None.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs:7`

---

### Finding 8: Forward-compatibility with signing/credential generalization (OKX phase)
- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence below 80)
- **Issue**: The config objective states "the signing/credential abstraction is generalized after Bybit, against OKX." The current `BybitSignatureService` is a concrete sealed class with no interface. When the generalization task arrives, if OKX also uses HMAC-SHA256 hex, there will be pressure to either extract `ISignatureService` or share the HMAC primitive. Since `BybitSignatureService` is concrete-only, the generalization task will need to introduce an interface or abstract base without breaking the Bybit composer. This is manageable — the composer constructs `BybitSignatureService` directly (same as `BinanceClientComposer` does for Binance), so adding an interface later is additive-only. This is a forward-design concern, not a current defect.
- **Fix**: Consider adding an `ISignatureService` interface with a `Sign(string)` method in the `Auth` folder now, or noting explicitly in the OKX generalization task manifest. Not blocking.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs` (same issue exists in Binance; consistency is maintained)

---

### Finding 9: Static sign-string builders vs. instance placement — design acceptable
- **Severity**: N/A
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:37-53`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: The builders are `static` because they have no key dependency, making them testable independently of signing. The Binance equivalent (`BuildSignedQuery`) is an instance method because it both constructs the string AND calls `Sign`. By keeping the Bybit builders purely static and decoupled from `Sign`, the implementation makes sign-string composition independently unit-testable (TASK-008). This is an architectural improvement over Binance, not a deviation.
- **Fix**: None.

---

### Summary

- PASS: Dependency layering — both files contain only BCL usings; csproj references only Core and Http. No cross-layer pollution.
- PASS: Blast radius — exactly two new files added inside the Bybit project; no existing files modified; build passes with 0 warnings.
- PASS: HMAC primitive — `HMACSHA256.HashData` + `Convert.ToHexStringLower` matches Binance pattern exactly; signature returned (not appended) per Bybit protocol.
- PASS: Signing marker — `"bybit.signed"` key is distinct from `"binance.signed"`; `MarkSigned`/`IsSigned` are idempotent and null-guarded; round-trip works.
- PASS: Documented deviation (plain-text cref) — correctly handled given `TreatWarningsAsErrors`; upgrade path documented.
- PASS: Static sign-string builders — design decision is sound; makes GET/POST composition independently testable.
- CONCERN: Both types are `public` instead of `internal` (confidence: 55, non-blocking) — violates invariant #3; should be `internal` with `InternalsVisibleTo` (already configured) doing the heavy lifting for DI and test access.
- CONCERN: No null/empty guards on `timestamp` and `apiKey` parameters of the static builders (confidence: 70, non-blocking) — silent null produces malformed sign-string.
- CONCERN: No `ISignatureService` interface (confidence: 60, non-blocking) — forward-compatibility concern for the OKX generalization phase; not a current defect.

---

## Final Verdict

**APPROVED**

All three findings are CONCERNs with confidence below the blocking threshold of 80. The architectural invariants that matter most (layering direction, blast radius, HMAC correctness, signing marker idempotency, namespace placement, build cleanliness) are all satisfied. The `public` modifier concern is the most substantive: it is a pattern inconsistency relative to invariant #3, but it mirrors the existing Binance precedent exactly, and the `InternalsVisibleTo` gates are already in place. Blocking on this would require also blocking the equivalent Binance types, which are already shipped. The implementation is ready to proceed to the signing handler task.
