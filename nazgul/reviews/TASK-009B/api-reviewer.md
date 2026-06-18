# API Review — TASK-009B: Per-exchange DI re-homing (ADR-001)

**Commit**: 1a56835  
**Reviewer**: API Reviewer  
**Date**: 2026-06-18  
**Branch**: feat/m3-okx  

---

## Mandate Coverage

### 1. Breaking namespace move of AddBinanceExchange / AddBybitExchange

CONFIRMED ACCEPTABLE.

- `AddBinanceExchange` is now in `CryptoExchanges.Net.Binance` namespace (`src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs:14`). Previous location was `CryptoExchanges.Net.DependencyInjection`.
- `AddBybitExchange` is now in `CryptoExchanges.Net.Bybit` namespace (`src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs:15`). Previous location was `CryptoExchanges.Net.DependencyInjection`.
- No `[Obsolete]` forwarders exist, consistent with TASK-009B notes (TreatWarningsAsErrors makes [Obsolete(error:true)] a build error; plain [Obsolete] would emit a warning treated as error — correct to skip).
- Breaking change is acceptable at version 0.1.0-preview.1, pre-v1.0, as confirmed by ADR-001.
- `AddCryptoExchanges` is UNCHANGED: still in `CryptoExchanges.Net.DependencyInjection` namespace (`src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:5`), still takes `Action<CryptoExchangesOptions>? configure = null`. No signature change. PASS.
- Consumer migration cost: add `using CryptoExchanges.Net.Binance;` and/or `using CryptoExchanges.Net.Bybit;`. Confirmed in DiRegistrationTests.cs:4-5.

### 2. Public surface of each exchange package

**Binance intended surface**: `BinanceExchangeClient`, `BinanceOptions`, `ServiceCollectionExtensions` (AddBinanceExchange).
**Bybit intended surface**: `BybitExchangeClient`, `BybitOptions`, `ServiceCollectionExtensions` (AddBybitExchange).

Confirmed public types per assembly (grep on class-level declarations):

**Binance**:
- `BinanceOptions` (BinanceExchangeClient.cs:12) — CORRECT
- `BinanceExchangeClient` (BinanceExchangeClient.cs:34) — CORRECT
- `ServiceCollectionExtensions` (ServiceCollectionExtensions.cs:21) — CORRECT
- `BinanceErrorTranslator` (Resilience/BinanceErrorTranslator.cs:10) — EXCESS PUBLIC (see Finding 1)
- `BinanceTimeSync` (Resilience/BinanceTimeSync.cs:5) — EXCESS PUBLIC (see Finding 2)

**Bybit**:
- `BybitOptions` (BybitOptions.cs:6) — CORRECT
- `BybitExchangeClient` (BybitExchangeClient.cs:14) — CORRECT
- `ServiceCollectionExtensions` (ServiceCollectionExtensions.cs:22) — CORRECT
- `BybitErrorTranslator` (Resilience/BybitErrorTranslator.cs:8) — EXCESS PUBLIC (see Finding 3)
- `BybitSigningRequest` (Resilience/BybitSigningRequest.cs:5) — EXCESS PUBLIC (see Finding 4)
- `BybitTimeSync` (Resilience/BybitTimeSync.cs:7) — EXCESS PUBLIC (see Finding 5)

**Binance signing types (this task's explicit fold-in)**:
- `BinanceSignatureService` → `internal` (Auth/BinanceSignatureService.cs:9) — DONE. PASS.
- `BinanceSigningHandler` → `internal` (Resilience/BinanceSigningHandler.cs:12) — DONE. PASS.
- `BinanceSigningRequest` → `internal` (Resilience/BinanceSigningRequest.cs:5) — DONE. PASS.

### 3. Naming consistency and method signatures

- Both new classes are named `ServiceCollectionExtensions` in their respective exchange namespaces. PASS.
- `AddBinanceExchange(this IServiceCollection services, Action<BinanceOptions>? configure = null)` — matches original signature, optional parameter preserved. PASS.
- `AddBybitExchange(this IServiceCollection services, Action<BybitOptions>? configure = null)` — matches original signature, optional parameter preserved. PASS.
- Consumer migration confirmed: a single `using CryptoExchanges.Net.Binance;` covers both `BinanceOptions` and `AddBinanceExchange` (same namespace). Same for Bybit. PASS.

### 4. XML doc coverage on new public AddXxxExchange methods

- `ServiceCollectionExtensions` class (Binance): `<summary>` present (lines 16-20). PASS.
- `AddBinanceExchange` method: full `<summary>`, `<param name="services">`, `<param name="configure">`, `<returns>` present (lines 27-34). PASS.
- `ServiceCollectionExtensions` class (Bybit): `<summary>` present (lines 21-25). PASS.
- `AddBybitExchange` method: full `<summary>`, `<param name="services">`, `<param name="configure">`, `<returns>` present (lines 29-35). PASS.
- Note: CS1591 is suppressed in both csprojs (`<NoWarn>CS1591</NoWarn>`), so the build would not have caught missing docs. Docs are present regardless. PASS.

---

## Findings

### Finding 1: BinanceErrorTranslator is public — violates ADR-001 convention #2
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceErrorTranslator.cs:10`
- **Category**: API Design / Compatibility
- **Verdict**: CONCERN (non-blocking — pre-existing, out-of-scope for this commit's stated fold-in)
- **Issue**: `BinanceErrorTranslator` is `public sealed class`. ADR-001 convention #2 states only `XxxExchangeClient` + `XxxOptions` are public per exchange. This type is an implementation detail of the resilience pipeline. It is directly instantiated in `BinancePipelineEndToEndTests.cs:33` and `BinanceErrorTranslatorTests.cs:14` — those tests depend on it being public (no IVT for the test project on ErrorTranslator; the Binance integration test project has IVT, so making it internal would be compile-safe for tests). This type was public before this commit; TASK-009B's stated fold-in was limited to the 3 signing types. A follow-up task should address it.
- **Fix**: Change `public sealed class BinanceErrorTranslator` → `internal sealed class BinanceErrorTranslator`. Tests compile because `CryptoExchanges.Net.Binance.Tests.Integration` already has `InternalsVisibleTo` in the Binance csproj.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:9` — `internal sealed class BinanceSignatureService` (made internal in this commit)

### Finding 2: BinanceTimeSync is public — violates ADR-001 convention #2
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceTimeSync.cs:5`
- **Category**: API Design / Compatibility
- **Verdict**: CONCERN (non-blocking — pre-existing, out-of-scope for this commit's stated fold-in)
- **Issue**: `BinanceTimeSync` is `public static class`. ADR-001 convention #2 requires this to be `internal`. Directly referenced in `BinanceTimeSyncTests.cs:12`. Test project has IVT, so making it internal is safe.
- **Fix**: Change `public static class BinanceTimeSync` → `internal static class BinanceTimeSync`. The integration test project has IVT and will continue to compile.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:12` — `internal sealed class BinanceSigningHandler` (made internal in this commit)

### Finding 3: BybitErrorTranslator is public — violates ADR-001 convention #2
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitErrorTranslator.cs:8`
- **Category**: API Design / Compatibility
- **Verdict**: CONCERN (non-blocking — pre-existing, Bybit shipped before ADR-001 was written)
- **Issue**: `BybitErrorTranslator` is `public sealed class`. ADR-001 convention #2 requires `internal`. Directly referenced in `BybitPipelineEndToEndTests.cs:119,156` and `BybitSigningTests.cs:280,300`. The Bybit assembly has IVT for both `CryptoExchanges.Net.Bybit.Tests.Unit` and `CryptoExchanges.Net.Bybit.Tests.Integration`, so making it internal is compile-safe.
- **Fix**: Change `public sealed class BybitErrorTranslator` → `internal sealed class BybitErrorTranslator`.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:13` — `internal sealed class BybitSignatureService`

### Finding 4: BybitSigningRequest is public — violates ADR-001 convention #2
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:5`
- **Category**: API Design / Compatibility
- **Verdict**: CONCERN (non-blocking — pre-existing, out-of-scope for this commit's stated fold-in)
- **Issue**: `BybitSigningRequest` is `public static class`. The analogous Binance type (`BinanceSigningRequest`) was correctly made `internal` in this commit. `BybitSigningRequest` was not in this commit's touch list. Directly referenced in `BybitPipelineEndToEndTests.cs:65,86,125,162`. The Bybit assembly has IVT for the integration test project, so making it internal is compile-safe.
- **Fix**: Change `public static class BybitSigningRequest` → `internal static class BybitSigningRequest`. This completes the Binance/Bybit signing-type symmetry already established in this commit for the 3 Binance types.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs:5` — `internal static class BinanceSigningRequest` (made internal in this commit)

### Finding 5: BybitTimeSync is public — violates ADR-001 convention #2
- **Severity**: MEDIUM
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs:7`
- **Category**: API Design / Compatibility
- **Verdict**: CONCERN (non-blocking — pre-existing, lower urgency as BinanceTimeSync has same issue)
- **Issue**: `BybitTimeSync` is `public static class`. Directly referenced in `BybitSigningTests.cs:254,255,262,271`. The Bybit unit test project has IVT, so making it internal is compile-safe.
- **Fix**: Change `public static class BybitTimeSync` → `internal static class BybitTimeSync`.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceTimeSync.cs` (same pattern — both should be internal)

### Finding 6: ExchangeClientFactory relocation introduces new InternalsVisibleTo grants on Http project
- **Severity**: LOW
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj:8-9`
- **Category**: API Design
- **Verdict**: PASS (justified, consistent with mandate)
- **Issue / Rationale**: Two `InternalsVisibleTo` entries were added to `CryptoExchanges.Net.Http` to grant Binance and Bybit assemblies access to the relocated `internal ExchangeClientFactory`. The mandate says "any new InternalsVisibleTo in a source project must be justified." These are granted to OTHER SOURCE assemblies (Binance, Bybit), not consumer applications — they permit each `AddXxxExchange` to `TryAddSingleton<IExchangeClientFactory, ExchangeClientFactory>()` referencing the internal concrete type. This is architecturally justified: the factory must live in a shared lower layer (Http) to avoid the DI package being a required dependency for single-exchange consumers, and granting visibility to Binance/Bybit is the minimal access required. PASS.

---

## Structural Checks

### AddCryptoExchanges unchanged
- Signature: `public static IServiceCollection AddCryptoExchanges(this IServiceCollection services, Action<CryptoExchangesOptions>? configure = null)` — unchanged. PASS.
- Delegates to `AddBinanceExchange` + `AddBybitExchange` (now calling the public extensions from each assembly). Behavior preserved. PASS.
- `CryptoExchangesOptions` properties: `BinanceBaseUrl`, `BinanceApiKey`, `BinanceSecretKey`, `BybitBaseUrl`, `BybitApiKey`, `BybitSecretKey` — all present, no additions or removals. PASS.

### Dependency graph (ADR-001 acceptance criterion 3)
- Bybit csproj references only `Core` + `Http`. No Binance reference. A Bybit-only consumer does NOT transitively pull in Binance. PASS.
- DI csproj retains references to Binance + Bybit (required for aggregator). PASS.
- Http csproj references only Core. PASS.

### csproj metadata
- Binance csproj: `<PackageId>CryptoExchanges.Net.Binance</PackageId>`, `<Description>`, Apache-2.0 inherited from Directory.Build.props. PASS.
- Bybit csproj: `<PackageId>CryptoExchanges.Net.Bybit</PackageId>`, `<Description>`. PASS.
- InternalsVisibleTo for `CryptoExchanges.Net.DependencyInjection` correctly REMOVED from both Binance and Bybit csprojs (DI package no longer accesses exchange internals). PASS.

### BinanceHttpClient endpoint guard
- `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` confirmed present as first statement in `GetAsync<T>` (`BinanceHttpClient.cs:30`). Consistent with ADR-001 convention #4. PASS.

### Test coverage
- `DiRegistrationTests.BybitOnly_Registration_ResolvesBybitClient` added — verifies Bybit-only registration path resolves correctly without Binance dependency. PASS.
- All existing DI tests updated with `using CryptoExchanges.Net.Binance;` and `using CryptoExchanges.Net.Bybit;` for the relocated extensions. PASS.

---

## Summary

- PASS: Namespace move of AddBinanceExchange/AddBybitExchange — clean break, correct namespace, pre-v1.0 acceptable
- PASS: AddCryptoExchanges unchanged in CryptoExchanges.Net.DependencyInjection, delegates to per-exchange extensions
- PASS: Binance signing types (BinanceSignatureService, BinanceSigningHandler, BinanceSigningRequest) correctly made internal
- PASS: Method signatures AddBinanceExchange/AddBybitExchange match original, optional parameter preserved
- PASS: XML doc coverage on both new AddXxxExchange methods — class + method + all params documented
- PASS: ExchangeClientFactory moved to Http, InternalsVisibleTo on Http for Binance/Bybit justified
- PASS: Dependency graph — Bybit-only consumer does not pull Binance
- PASS: BinanceHttpClient endpoint guard added (ADR-001 convention #4)
- PASS: csproj metadata correct; InternalsVisibleTo for DI package removed from exchange csprojs
- CONCERN: BinanceErrorTranslator is public (should be internal per ADR-001 #2) — confidence 95/100, non-blocking (pre-existing, file not in commit scope)
- CONCERN: BinanceTimeSync is public (should be internal per ADR-001 #2) — confidence 95/100, non-blocking (pre-existing)
- CONCERN: BybitErrorTranslator is public (should be internal per ADR-001 #2) — confidence 95/100, non-blocking (pre-existing)
- CONCERN: BybitSigningRequest is public (should be internal per ADR-001 #2) — confidence 95/100, non-blocking (pre-existing; Binance analog was addressed in this commit)
- CONCERN: BybitTimeSync is public (should be internal per ADR-001 #2) — confidence 90/100, non-blocking (pre-existing)

---

## Final Verdict

**APPROVED**

All items in the TASK-009B mandate are correctly implemented. The five CONCERNs are pre-existing public types that ADR-001 convention #2 requires be made internal, but none were in this commit's stated scope (the task explicitly scoped the fold-in to the 3 Binance signing types). All five can be made internal without a public-API impact because the relevant test assemblies already have InternalsVisibleTo grants. Recommend filing a follow-up task to complete the ADR-001 #2 harmonization pass across all five remaining public resilience/infrastructure types in Binance and Bybit.
