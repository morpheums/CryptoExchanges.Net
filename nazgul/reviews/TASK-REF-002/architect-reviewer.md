# Architect Review — TASK-REF-002
## Interface seams for time-sync + signing (DIP, behavior-preserving refactor)
**Reviewer**: Architect  
**Date**: 2026-06-18  
**Branch**: refactor/interface-seams  
**Diff**: 18 files changed (21 file-level changes incl. nazgul artifacts)

---

## Pre-review build / test gate

- `dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s)** (TreatWarningsAsErrors in effect)
- `dotnet test --filter "Category!=Integration"` → **335 pass, 0 fail** (baseline 333 + 2 new DI tests)

---

## Findings

### Finding 1: BinanceSignatureService.BuildSignedQuery is dead code (known candidate)
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:27-32`
- **Category**: Architecture / Dead code
- **Verdict**: REJECT (blocking — severity MEDIUM, confidence 95)
- **Issue**: `BinanceSignatureService.BuildSignedQuery(string)` (lines 27-32) is unreachable from any production or test call site. Before this refactor the `BinanceSigningHandler` called `signatureService.BuildSignedQuery(...)` directly on the concrete type. After the refactor the handler holds `ISignatureService` (which does not expose `BuildSignedQuery`) and inlines an equivalent private helper. The method now exists only on the concrete class, is `public` on an `internal sealed` type (accessible only within the assembly), and has **zero callers** confirmed by grep across `src/` and `tests/`. The task manifest acknowledges this explicitly ("Extra members left as-is per scope: Binance `BuildSignedQuery` (kept)") but does not justify why a dead method should be kept. Dead code on an internal type is not a public-API concern, but it does: (a) mislead future readers into thinking the method is used, (b) duplicate logic already correctly implemented in the handler's private `BuildSignedQuery`, and (c) create a silent divergence risk if either copy is ever edited. The comment in `BinanceSigningHandler` at line 83 acknowledges that `BuildSignedQuery` is "intentionally off ISignatureService", which correctly explains the interface boundary choice but does not justify keeping the unreachable service-side copy.
- **Fix**: Remove `BinanceSignatureService.BuildSignedQuery` (lines 22-32 in the post-diff file). The interface-inlined private helper in `BinanceSigningHandler` (lines 82-89) is the correct location. If a future test needs direct unit-coverage of the query-build logic, the test should exercise it via the handler (or inline the logic in a test helper), not via a dead public method on the service.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs` — Bybit's service correctly exposes only `Sign(string)` (the interface method) plus the static `BuildGetSignString`/`BuildPostSignString` helpers that ARE called from its signing handler. The pattern is: only expose what is consumed; the sign-string construction helpers live where they are called.

---

### Finding 2: IExchangeTimeSync — interface correct, placement correct, single registration confirmed
- **Severity**: —
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Resilience/IExchangeTimeSync.cs`, `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:70`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Interface lives in Core (no upstream dependencies). Registration is exactly one `TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>()` in the Http layer's `ExchangeServiceRegistration.AddExchange`, which is the shared per-exchange DI helper. It is exchange-agnostic and consumer-overridable (TryAdd semantics, confirmed by the `Consumer_Can_Override_ExchangeTimeSync` DI test). No duplicate registration in any per-exchange `ServiceCollectionExtensions`. No Core→Http dependency introduced.

---

### Finding 3: ExchangeTimeSync static→instance conversion — behavior identical
- **Severity**: —
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Resilience/ExchangeTimeSync.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The logic is byte-identical to the former static implementation: `ComputeOffset` is the same subtraction, `ApplyOffset` uses the same `ArgumentNullException.ThrowIfNull` + length guard + `Interlocked.Exchange`. The `ExchangeTimeSyncTests` suite retained all four original assertions with only mechanical changes (static calls → instance calls on `_sut`).

---

### Finding 4: Factory-free Create() path supplies `new ExchangeTimeSync()` — correct
- **Severity**: —
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs:31`, `src/CryptoExchanges.Net.Bybit/Internal/BybitClientComposer.cs:35`, `src/CryptoExchanges.Net.Okx/Internal/OkxClientComposer.cs:34`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Each exchange's `Create(options)` factory-free path correctly supplies `new ExchangeTimeSync()` — the concrete implementation without DI, consistent with the no-container path not having a service provider. DI path uses `sp.GetRequiredService<IExchangeTimeSync>()` in all three `ComposeForDi` methods.

---

### Finding 5: ISignatureService interface — correct layering, no DI registration
- **Severity**: —
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Auth/ISignatureService.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The interface lives in Core.Auth, the three implementations are `internal sealed` in their respective exchange assemblies, and the handlers depend on `ISignatureService` (not the concrete). No DI registration added (correct: the concrete is composer-constructed and passed through the handler chain). The task mandate ("No DI registration — composer-constructed") is honored.

---

### Finding 6: Bybit and OKX handlers retain `using` of concrete auth namespace — correct
- **Severity**: —
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:2`, `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:2`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The Bybit handler calls `BybitSignatureService.BuildGetSignString` and `BuildPostSignString` (static helpers for canonical sign-string assembly — these are pure string formatters, not swappable behavior). The OKX handler calls `OkxSignatureService.FormatTimestamp` and `OkxSignatureService.BuildPrehash` for the same reason. These static methods are pure formatters that are exchange-protocol-specific and appropriately not on the interface. Retaining the concrete import for static helper access is correct and the same pattern Binance's handler uses for its own private `BuildSignedQuery` helper.

---

### Finding 7: No over-conversion — HmacSignature, SignatureEncoding, XxxClientComposer, ExchangeServiceRegistration, ServiceCollectionExtensions kept static
- **Severity**: —
- **Confidence**: 100
- **File**: Multiple (confirmed by diff review — none of these types changed)
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Every type mandated as static by the architect ruling (invariant #11 carve-out) was untouched. The diff shows no changes to `HmacSignature.cs`, `SignatureEncoding.cs`, the three `ServiceCollectionExtensions.cs`, `HttpClientPipelineBuilder.cs`, or `ExchangeResiliencePipeline.cs`.

---

### Finding 8: No public-surface regression on exchange packages
- **Severity**: —
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj`, Bybit/Okx equivalents
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The three signature services remain `internal sealed`. `ISignatureService` and `IExchangeTimeSync` are new `public` interfaces in Core — appropriate since Core's public surface is the extension point contract. `ExchangeTimeSync` was already `public static`; it remains `public sealed`. No previously-internal type was made public.

---

### Finding 9: No existing interface (IMarketDataService, ITradingService, IAccountService, IExchangeClient) modified
- **Severity**: —
- **Confidence**: 100
- **File**: (none of these files appear in the diff)
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.

---

### Finding 10: Package dependency graph unchanged
- **Severity**: —
- **Confidence**: 100
- **File**: `*.csproj` files (no `ProjectReference` changes in any .csproj)
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The diff touches no `.csproj` files. The `using CryptoExchanges.Net.Core.Resilience` added to Http's `ExchangeServiceRegistration` is within the existing Core→Http dependency direction (Http already references Core). No new package-level dependency was introduced.

---

### Finding 11: `_offsetHolder` pattern unchanged — clock-skew sharing intact
- **Severity**: —
- **Confidence**: 100
- **File**: All three `XxxExchangeClient.cs`, all three `XxxClientComposer.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The `long[] _offsetHolder` single-element array closure shared between the signing handler and `SyncServerTimeAsync` is untouched. The diff adds `_timeSync` as a parallel field alongside `_offsetHolder`; it does not replace or alias the holder.

---

### Finding 12: Test coverage for both seams
- **Severity**: —
- **Confidence**: 100
- **File**: `tests/CryptoExchanges.Net.Core.Tests.Unit/ExchangeTimeSyncTests.cs`, `tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/DiRegistrationTests.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The existing `ExchangeTimeSyncTests` suite (4 tests) updated to instance style. Two new DI tests added: `Registers_ExchangeTimeSync_AsDefault` and `Consumer_Can_Override_ExchangeTimeSync`. The `BinanceSigningHandlerTests` and `BinancePipelineEndToEndTests` continue to pass, constructing `BinanceSigningHandler` with a `new BinanceSignatureService("secret")` — the concrete instance is assignable to `ISignatureService`, confirming behavior continuity.

---

## Summary

| # | Item | Verdict | Confidence |
|---|------|---------|------------|
| 1 | `BinanceSignatureService.BuildSignedQuery` dead code | REJECT (blocking) | 95 |
| 2 | IExchangeTimeSync interface + registration | PASS | 100 |
| 3 | ExchangeTimeSync static→instance, behavior identical | PASS | 100 |
| 4 | Factory-free Create() supplies new ExchangeTimeSync() | PASS | 100 |
| 5 | ISignatureService interface layering, no DI reg | PASS | 100 |
| 6 | Bybit/OKX handlers retain concrete auth using for static helpers | PASS | 100 |
| 7 | No over-conversion — static types correctly left static | PASS | 100 |
| 8 | No public-surface regression on exchange packages | PASS | 100 |
| 9 | No existing public interface modified | PASS | 100 |
| 10 | Package dependency graph unchanged | PASS | 100 |
| 11 | _offsetHolder clock-skew sharing intact | PASS | 100 |
| 12 | Test coverage for both seams | PASS | 100 |

---

### Blocking items

**Finding 1 (REJECT, confidence 95, severity MEDIUM):**  
`BinanceSignatureService.BuildSignedQuery` at `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:27-32` is dead code — zero callers in the entire repo after the refactor. Remove the method. The correct implementation now lives as a private helper in `BinanceSigningHandler`.

---

## Final Verdict

**CHANGES_REQUESTED**

One blocking defect: `BinanceSignatureService.BuildSignedQuery` (lines 27-32) is unreachable dead code that duplicates and diverges from the handler's private helper. Fix is trivial (delete the method). All other invariants — layering, single DI registration, TryAdd override semantics, static types kept static, no interface additions, no public-surface regression, behavior byte-identical — are fully satisfied.
