# Architect Review — TASK-009B: Per-exchange DI re-homing (ADR-001)

**Commit**: 1a56835  
**Branch**: feat/m3-okx  
**Reviewer**: Architect  
**Date**: 2026-06-18  

---

## VERDICT: APPROVED
**Confidence**: 92/100

The diff correctly implements the dependency-direction fix mandated by ADR-001. All four mandate items pass. Two non-blocking concerns are raised: one pre-existing public type left untouched by the task (not a new regression), and one structural note about `ExchangeClientFactory` placement that is sound but worth documenting.

---

## Mandate Checklist

### (a) Layering Invariant #10 — Single-exchange consumer does not pull sibling assemblies

**PASS** — Confidence: 99/100

Verified via `dotnet list <proj> reference`:

```
CryptoExchanges.Net.Bybit    → Core, Http  (no Binance)
CryptoExchanges.Net.Binance  → Core, Http  (no Bybit)
CryptoExchanges.Net.DI       → Core, Http, Binance, Bybit  (aggregator — correct)
CryptoExchanges.Net.Http     → Core  (no exchange refs)
CryptoExchanges.Net.Core     → (no project refs)
```

A consumer referencing only `CryptoExchanges.Net.Bybit` gets `Core` + `Http` transitively; `Binance` is not in the closure. The pre-ADR-001 violation (DI package forced every exchange) is fully resolved.

The new `BybitOnly_Registration_ResolvesBybitClient` test in `DiRegistrationTests.cs` provides runtime proof: `AddBybitExchange` alone resolves a keyed Bybit `IExchangeClient` and leaves no unkeyed registration.

---

### (b) ExchangeClientFactory relocation to Http — Layering judgement

**PASS** — Confidence: 88/100

The factory (`src/CryptoExchanges.Net.Http/ExchangeClientFactory.cs`) is `internal sealed`, namespace `CryptoExchanges.Net.Http`, and depends only on `Core` types (`IExchangeClient`, `ExchangeId`, `ExchangeNotRegisteredException`, `IExchangeClientFactory`) plus `Microsoft.Extensions.DependencyInjection` abstractions. It introduces no exchange-specific knowledge.

The interface `IExchangeClientFactory` correctly stays `public` in `Core`. The concrete implementation living one layer up in `Http` is sound: `Http` is already the shared infrastructure layer that every exchange assembly references, and the factory needs `GetKeyedService` / `KeyedService.AnyKey` from DI abstractions — which `Http` already receives transitively through `Microsoft.Extensions.Http.Resilience` (confirmed via `dotnet list ... --include-transitive`). Placing it in `Core` would pull a DI-framework dependency into Core's zero-external-deps contract, which would be a harder violation.

`InternalsVisibleTo` grants added to `Http.csproj` for `CryptoExchanges.Net.Binance` and `CryptoExchanges.Net.Bybit` so each exchange's `AddXxxExchange` can register the internal type via `TryAddSingleton<IExchangeClientFactory, ExchangeClientFactory>()`. This is correct: the IVT grants are narrowly scoped (two named assemblies; no wildcards), and the factory remains invisible to consumers.

One structural note: `Http` now implicitly depends on the DI framework's keyed-service APIs at runtime (even though the package ref is transitive). This is non-blocking because (1) it was already true of the old DI-package copy, (2) every exchange assembly already brings in `Microsoft.Extensions.DependencyInjection.Abstractions` explicitly, and (3) the build is clean. Documented as a CONCERN below.

---

### (c) Registration behavior unchanged

**PASS** — Confidence: 97/100

Reviewed `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs` and `src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs` against the pre-commit `ServiceCollectionExtensions.cs` in the DI package (checked via `git show 4e5ba3f`). All of the following are present and structurally identical:

- `TryAddSingleton<IExchangeClientFactory, ExchangeClientFactory>()` — registration of the factory
- `AddOptions<XxxOptions>()` with `.Configure(ApplyEnvDefaults)`, `.Configure(configure?)`, two `.Validate(...)` predicates, and `.ValidateOnStart()`
- `TryAddSingleton(sp => sp.GetRequiredService<IOptions<XxxOptions>>().Value)` — options materialization
- `TryAddKeyedSingleton(ExchangeId.Xxx, (_, _) => new long[] { 0L })` — clock-skew offset holder (with `#pragma disable CA1861` comment preserved)
- `TryAddKeyedSingleton<ISymbolMapper>` and `TryAddKeyedSingleton<IMapper>`
- Named (not typed) `AddHttpClient(ClientName, ...)` with `SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }` — no captive dependency
- `ApplyResiliencePipeline(...)` with correct `UsageHeaderName`, `translatorFactory`, `gateFactory`, `requestFinalizerFactory` — PassThrough path when `SecretKey` is empty; signing path with `Interlocked.Read(ref holder[0])` closure
- `AddKeyedSingleton<IExchangeClient>(ExchangeId.Xxx, ...)` via `IHttpClientFactory.CreateClient(ClientName)` + `ComposeForDi`

No behavioral delta introduced.

---

### (d) InternalsVisibleTo(DependencyInjection) removal — Justified

**PASS** — Confidence: 99/100

Both exchange `.csproj` files previously carried `InternalsVisibleTo("CryptoExchanges.Net.DependencyInjection")`. That grant existed because the old monolithic `AddXxxExchange` bodies in the DI package needed to reach exchange-internal types (`BinanceClientComposer`, `BinanceSigningHandler`, etc.). Now that those bodies live inside the exchange assemblies themselves, the DI package only calls the public `AddBinanceExchange` / `AddBybitExchange` extension methods — it never touches exchange internals. The grants are correctly removed.

Retained IVT grants are appropriate:
- Binance: `CryptoExchanges.Net.Binance.Tests.Integration` (constructs internal signing types directly)
- Bybit: `CryptoExchanges.Net.Bybit.Tests.Unit`, `CryptoExchanges.Net.Bybit.Tests.Integration`, `DynamicProxyGenAssembly2` (NSubstitute mocking of `IBybitHttpClient`)

---

## Build Verification

`dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s)** (TreatWarningsAsErrors active).

---

## Findings

---

### Finding 1: BinanceErrorTranslator and BinanceTimeSync remain public — not hardened in this task

- **Severity**: LOW
- **Confidence**: 72/100 (pre-existing; not introduced by this diff)
- **File**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceErrorTranslator.cs:10`, `BinanceTimeSync.cs:5`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80; also pre-existing)
- **Issue**: ADR-001 Convention #2 states only `XxxExchangeClient` and `XxxOptions` are public per exchange. This task correctly internalized `BinanceSignatureService`, `BinanceSigningHandler`, and `BinanceSigningRequest`. However `BinanceErrorTranslator` and `BinanceTimeSync` remain `public`. Both were public before this commit (confirmed via `git show 4e5ba3f`), so this is not a regression introduced here. The task notes ("Binance signing types → internal") focused on the signing trio; the translator and time-sync were out of scope.
- **Fix**: Mark `BinanceErrorTranslator` and `BinanceTimeSync` as `internal` in a follow-up task. `BinanceErrorTranslator` is used only inside the resilience pipeline (also internal); `BinanceTimeSync` is called only from `BinanceSyncServerTimeService` (also internal). Neither type is referenced outside the assembly today. Verify with `InternalsVisibleTo` coverage for tests before landing.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:9` (same pattern applied this commit)

---

### Finding 2: Http layer's implicit DI-framework coupling not explicitly declared

- **Severity**: LOW
- **Confidence**: 65/100
- **File**: `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj:12`, `src/CryptoExchanges.Net.Http/ExchangeClientFactory.cs:5`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `ExchangeClientFactory` calls `GetKeyedService` / `KeyedService.AnyKey` from `Microsoft.Extensions.DependencyInjection`. The `Http.csproj` has no explicit `PackageReference` for `Microsoft.Extensions.DependencyInjection.Abstractions` — it arrives only transitively via `Microsoft.Extensions.Http.Resilience`. This compiles and works today, but it means Http's DI-framework dependency is implicit rather than declared. If `Microsoft.Extensions.Http.Resilience` ever stops pulling in the full DI stack (e.g., package split), the build would break without a clear signal.
- **Fix**: Add `<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.*" />` to `CryptoExchanges.Net.Http.csproj` to make the dependency explicit. This is a one-line addition with no behavior change.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj:23` (explicit `Microsoft.Extensions.DependencyInjection.Abstractions` reference)

---

## Summary

- PASS: Invariant #10 (package-level coupling) — dependency arrows are now correct; Bybit-only consumer does not pull Binance. Verified by `dotnet list reference` and the new `BybitOnly_Registration_ResolvesBybitClient` test.
- PASS: ExchangeClientFactory relocation — correctly placed in Http (shared infrastructure), kept `internal`, gated by narrowly-scoped `InternalsVisibleTo` grants. Exchange-agnostic. `IExchangeClientFactory` interface stays public in Core. Layering is sound.
- PASS: Registration behavior — verbatim move confirmed. Keyed singletons, named HttpClient, resilience pipeline, PassThrough finalizer, `ValidateOnStart`, clock-skew holder pattern all present and structurally identical.
- PASS: InternalsVisibleTo(DI) removal — justified and verified. DI package no longer touches exchange internals; only calls public extension methods.
- CONCERN: `BinanceErrorTranslator` and `BinanceTimeSync` still `public` (confidence: 72/100, non-blocking) — pre-existing, out of this task's stated scope; recommend a dedicated follow-up to complete the ADR-001 Convention #2 surface-hardening pass.
- CONCERN: Http project's `Microsoft.Extensions.DependencyInjection.Abstractions` dep is implicit/transitive rather than declared (confidence: 65/100, non-blocking) — one-line fix to future-proof the csproj.

## Final Verdict

**APPROVED** — Confidence 92/100. No blocking items. The ADR-001 fix is structurally correct and complete. Two non-blocking CONCERNs raised (both below the 80-confidence blocking threshold), suitable for follow-up tasks.
