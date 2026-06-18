# API Review — TASK-REF-002: Interface seams (IExchangeTimeSync + ISignatureService)

**Branch**: refactor/interface-seams  
**Diff**: 18 files, 1 commit (83da9ed)  
**Reviewer**: api-reviewer  
**Date**: 2026-06-18  

---

## Scope assessed

The diff introduces two new public Core contracts (`IExchangeTimeSync`, `ISignatureService`), converts `ExchangeTimeSync` from a public static class to a public sealed instance class, threads the seam through three exchange clients and their internal composers, and registers `IExchangeTimeSync` in the shared DI helper.

---

## Findings

### Finding 1: IExchangeTimeSync — well-formed public contract
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Resilience/IExchangeTimeSync.cs:1-13`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. Interface is in the correct namespace (`CryptoExchanges.Net.Core.Resilience`), is `public`, has XML summary on the interface itself and on both members. `ApplyOffset` documents its two thrown exceptions (`ArgumentNullException`, `ArgumentException`). Return types (`long`) are appropriate for millisecond offsets.

---

### Finding 2: ISignatureService — well-formed public contract
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Auth/ISignatureService.cs:1-8`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. Interface is in `CryptoExchanges.Net.Core.Auth`, is `public`, carries a summary on the interface and a `<paramref>` doc on the member. The single-method surface (`string Sign(string payload)`) is minimal and unambiguous.
- **Minor observation**: `ISignatureService.Sign` has no `<exception>` doc or `<returns>` doc. This is not blocking — the single-method contract is self-descriptive — but a `/// <returns>The encoded signature string.</returns>` tag would make the contract explicit for implementers. Non-blocking at pre-1.0.

---

### Finding 3: ExchangeTimeSync static-to-instance conversion — breaking change, pre-1.0
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Core/Resilience/ExchangeTimeSync.cs:1-19`
- **Category**: Compatibility
- **Verdict**: CONCERN (non-blocking — pre-1.0, intended)
- **Issue**: `ExchangeTimeSync` was previously `public static class ExchangeTimeSync` (verified on `main`). Callers who called `ExchangeTimeSync.ComputeOffset(...)` or `ExchangeTimeSync.ApplyOffset(...)` as static methods will no longer compile. This is a binary- and source-breaking change for any external consumer.
- **Mitigation already in place**: The library is `0.1.0-preview.1`; the task manifest explicitly documents this as an intentional, behavior-preserving DIP refactor. The preferred call pattern going forward is `IExchangeTimeSync` (injectable), not the concrete class.
- **Fix**: No code fix required. A changelog / release-notes entry noting the static→instance conversion of `ExchangeTimeSync` and pointing consumers to `IExchangeTimeSync` is recommended before publishing the NuGet package.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Resilience/IExchangeTimeSync.cs` (the correct interface to depend on post-refactor).

---

### Finding 4: XxxExchangeClient constructors gained IExchangeTimeSync param — not a consumer-facing break
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:54`, `src/CryptoExchanges.Net.Bybit/BybitExchangeClient.cs:34`, `src/CryptoExchanges.Net.Okx/OkxExchangeClient.cs:34`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: All three `XxxExchangeClient` primary constructors gained an `IExchangeTimeSync timeSync` parameter. Confirmed on `main` that these constructors were already `internal` before this diff (not `public`). Consumers cannot call them directly; they must use `Create(options)`, `CreateFromEnvironment()`, or DI. The added parameter does not affect any public API surface.

---

### Finding 5: Signature services remain internal — no accidental public leakage
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:10`, `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:11`, `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:10`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. All three concrete signature services are `internal sealed class`. The fact that they implement the `public` `ISignatureService` does not leak them — implementing a public interface from an internal type is valid C# and does not change the type's own accessibility.

---

### Finding 6: Composers remain internal static — no accidental public leakage
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs:14`, equivalent for Bybit/Okx
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. `BinanceClientComposer`, `BybitClientComposer`, `OkxClientComposer` are all `internal static`. The updated `ComposeWith`/`ComposeOver`/`ComposeForDi` signatures (gaining `IExchangeTimeSync timeSync`) are internal API and do not affect any external caller. Confirmed no new `InternalsVisibleTo` entries were added by this diff — existing grants (test projects + DI project) are unchanged.

---

### Finding 7: DI registration — TryAddSingleton semantics correct, overrideable
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:70`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. `services.TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>()` uses `TryAdd`, which means a prior consumer registration wins. This is the correct pattern for a swappable infrastructure service. Test `Consumer_Can_Override_ExchangeTimeSync` in `DiRegistrationTests` explicitly verifies the override semantics.

---

### Finding 8: BinanceSigningHandler private BuildSignedQuery helper — wire-output preserved
- **Severity**: LOW
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:172-176`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: The old `signatureService.BuildSignedQuery(...)` call (a method that was NOT on `ISignatureService`) has been inlined as a private `BuildSignedQuery` helper inside the handler. The inlined implementation matches exactly: `sign = Sign(q)`, then append `&signature={sign}`. Wire output is byte-identical to before. This is the correct approach — `BuildSignedQuery` is Binance-specific and has no place on the shared interface.

---

### Finding 9: No remaining static ExchangeTimeSync call sites in source
- **Severity**: LOW
- **Confidence**: 99
- **File**: All `src/**/*.cs` (grep confirmed)
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. All three `SyncServerTimeAsync` call sites in Binance/Bybit/Okx clients now call `_timeSync.ApplyOffset(...)`. The only remaining `ExchangeTimeSync` references in `src/` are the three `new ExchangeTimeSync()` instantiations inside the internal `Create(options)` factory-free paths — correct and expected.

---

### Finding 10: ISignatureService.Sign — no null-contract or exception documentation
- **Severity**: LOW
- **Confidence**: 75
- **File**: `src/CryptoExchanges.Net.Core/Auth/ISignatureService.cs:6-7`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 75)
- **Issue**: The interface does not document whether `null` is a valid argument for `payload`, what the return value contract is (non-null? empty string for empty payload?), or whether any exceptions can be thrown. External implementers have no guidance. At pre-1.0 with internal-only implementers this is acceptable.
- **Fix (non-blocking)**: Add `/// <returns>A non-null encoded signature string.</returns>` and optionally `/// <exception cref="ArgumentNullException">payload is null.</exception>` before the next minor release.

---

## Summary

- PASS: `IExchangeTimeSync` interface — well-formed, correctly namespaced, XML-documented including exception contracts.
- PASS: `ISignatureService` interface — well-formed, correctly namespaced, XML summary present.
- PASS: `ExchangeTimeSync` concrete class — correct `public sealed class : IExchangeTimeSync` shape, logic byte-identical to prior static version, `/// <inheritdoc cref="IExchangeTimeSync" />` on the class.
- PASS: All three signature services (`Binance/Bybit/Okx`) — remain `internal sealed`; implementing a public interface does not change their accessibility.
- PASS: All three `XxxExchangeClient` ctors — were already `internal`; added `IExchangeTimeSync` param is a non-public-surface change.
- PASS: Composers remain `internal static` — no new `InternalsVisibleTo` grants added.
- PASS: DI registration — `TryAddSingleton`, exchange-agnostic, overrideable by consumer.
- PASS: `BinanceSigningHandler` private `BuildSignedQuery` inlining — wire-output byte-identical.
- CONCERN: `ExchangeTimeSync` static→instance conversion — source/binary-breaking for any external consumer who called `ExchangeTimeSync.ComputeOffset/ApplyOffset` statically (confidence: 95/100, non-blocking at pre-1.0 if documented in changelog).
- CONCERN: `ISignatureService.Sign` missing null/return-value/exception doc — low risk at pre-1.0 with internal-only implementers (confidence: 75/100, non-blocking).

---

## Final Verdict

**APPROVED**

All public API surface changes are intentional and well-formed. The two blocking-confidence concerns are both pre-1.0 acceptable: the static→instance break on `ExchangeTimeSync` is documented in the task manifest as the explicit goal of the refactor, and there are no external static call sites within the repository. No accidental public surface was introduced; all signature service types remain internal.

Recommended follow-up (non-blocking, before NuGet publish):
1. Changelog entry for the `ExchangeTimeSync` static→instance conversion.
2. Add `<returns>` and `<exception>` tags to `ISignatureService.Sign`.
