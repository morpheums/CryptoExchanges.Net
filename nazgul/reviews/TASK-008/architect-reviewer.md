# Architect Review — TASK-008
**Reviewer**: Architect Reviewer
**Commit**: f60bd18
**Branch**: feat/m2-exchange-expansion
**Date**: 2026-06-17

---

## Verdict

- **VERDICT**: APPROVE
- **CONFIDENCE**: 97

---

## Findings

### Finding: Layering / ProjectReference direction
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/CryptoExchanges.Net.DependencyInjection.csproj:13`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: New `<ProjectReference Include="..\CryptoExchanges.Net.Bybit\..." />` in the DI project.
- **Fix**: N/A
- **Pattern reference**: `CryptoExchanges.Net.DependencyInjection.csproj:12` (existing Binance reference)

DI is the top layer and is explicitly permitted to reference Exchange projects. The Bybit csproj itself references only Core and Http (`CryptoExchanges.Net.Bybit.csproj:12-13`), preserving the Core→Http→Exchange→DI chain. No layering violation.

---

### Finding: Named (not typed) HttpClient / captive-dependency
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:175,213`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:78,116` (Binance named-client pattern)

`AddBybitExchange` calls `services.AddHttpClient(BybitClientName, ...)` (a named client), then resolves it inside the keyed-singleton factory via `sp.GetRequiredService<IHttpClientFactory>().CreateClient(BybitClientName)`. Exactly mirrors the Binance path. No captive dependency.

---

### Finding: Offset-holder closure / clock-skew sharing
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:163,203,208`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:66,108,110` (Binance holder pattern)

`TryAddKeyedSingleton(ExchangeId.Bybit, (_, _) => new long[] { 0L })` creates the fresh mutable array. The signing handler factory captures it via `sp.GetRequiredKeyedService<long[]>(ExchangeId.Bybit)` and passes `() => Interlocked.Read(ref holder[0])`. `BybitClientComposer.ComposeForDi` (line 58-66) passes the same holder to `BybitExchangeClient`, which writes it in `SyncServerTimeAsync` via `Interlocked.Exchange`. Structurally identical to the Binance pattern.

---

### Finding: All four keyed singletons registered
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:163-217`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:66-120` (Binance keyed singletons)

`long[]` offset holder (line 163), `ISymbolMapper` keyed by `ExchangeId.Bybit` (line 165), `IMapper` keyed by `ExchangeId.Bybit` (line 167), `IExchangeClient` keyed by `ExchangeId.Bybit` (line 211). Mirrors Binance exactly.

---

### Finding: SocketsHttpHandler with PooledConnectionLifetime
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:182-183`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:87-88` (Binance handler)

`ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) })` matches Binance.

---

### Finding: ApplyResiliencePipeline arguments — UsageHeaderName and factory arguments
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:193-209`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:98-111` (Binance pipeline)

`UsageHeaderName = "X-Bapi-Limit-Status"` correctly set for Bybit (Binance uses `"X-MBX-USED-WEIGHT-1m"`). `translatorFactory`, `gateFactory`, and secret-gated `requestFinalizerFactory` are all present and correct.

---

### Finding: No X-BAPI-API-KEY in DefaultRequestHeaders — justified divergence from Binance
- **Severity**: N/A
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:175-183`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Binance places `X-MBX-APIKEY` in `DefaultRequestHeaders` (line 84-85); Bybit does not.
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:27-29`

Binance adds `X-MBX-APIKEY` as a default header because Binance V3 requires it even on non-signed requests. Bybit V5 carries `X-BAPI-API-KEY` exclusively through `BybitSigningHandler.SendAsync` per-request (lines 27-29), only when non-empty. The omission from `DefaultRequestHeaders` in the DI HttpClient config is intentional and correct per Bybit V5 API semantics.

---

### Finding: ReceiveWindow decimal-to-string formatting consistency
- **Severity**: N/A
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:207`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Internal/BybitClientComposer.cs:83`

`BybitOptions.ReceiveWindow` is `decimal = 5000m`. `(5000m).ToString(CultureInfo.InvariantCulture)` produces `"5000"`, matching the `recvWindow` value in all signing test vectors (e.g., `BybitSigningTests.cs:36`). Both the DI path and factory-free path use `options.ReceiveWindow.ToString(CultureInfo.InvariantCulture)`, so they are consistent.

---

### Finding: BybitHttpClient takes no BybitOptions — justified divergence from Binance
- **Severity**: N/A
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:214`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Binance uses `new BinanceHttpClient(httpClient, options)` (line 117); Bybit uses `new BybitHttpClient(httpClient)` (line 214).
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs:16`

Bybit's HTTP client is stateless beyond the injected `HttpClient`; all auth, signing, and recv-window are handled by `BybitSigningHandler` in the pipeline. The simpler constructor is correct.

---

### Finding: ComposeForDi ownsHttpClient: false invariant preserved
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitClientComposer.cs:65`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs:62`

`ComposeForDi` passes `ownsHttpClient: false`, preventing double-disposal of the `IHttpClientFactory`-owned handler chain, matching the Binance pattern.

---

### Finding: ValidateOnStart present
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:149-154`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:52-57` (Binance validation)

`AddOptions<BybitOptions>()` chain ends with `.ValidateOnStart()`. Validation rules for `TimeoutSeconds > 0` and non-empty `BaseUrl` mirror Binance's.

---

### Finding: AddCryptoExchanges extended correctly — no Binance regression
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:237-261`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:246-251` (Binance delegation)

`AddCryptoExchanges` calls both `AddBinanceExchange` and `AddBybitExchange`, passing through the Bybit-specific fields from `CryptoExchangesOptions`. Binance registration is untouched.

---

### Finding: TryAddSingleton<IExchangeClientFactory> idempotent
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:147`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:50` (Binance factory registration)

Both `AddBinanceExchange` and `AddBybitExchange` use `TryAddSingleton`, so calling both (e.g. via `AddCryptoExchanges`) does not double-register the factory.

---

### Finding: Internal type visibility — exchange internals stay internal
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:17-25`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj:17-21`

`BybitHttpClient`, `IBybitHttpClient`, `BybitSigningHandler`, `BybitClientComposer`, `BybitSymbolFormat` are `internal`. `BybitExchangeClient` and `BybitOptions` are `public`. `BybitSigningRequest` and `BybitErrorTranslator` are `public` — consistent with the Binance pattern. `InternalsVisibleTo` grants are scoped to the two test assemblies, `DynamicProxyGenAssembly2`, and `CryptoExchanges.Net.DependencyInjection`.

---

### Finding: No new public interface members
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Interfaces/IExchangeClient.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Interfaces/IExchangeClient.cs`

No additions to `IMarketDataService`, `ITradingService`, `IAccountService`, or `IExchangeClient`.

---

### Finding: DeltaMapper profiles used for DTO→model mapping
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitClientComposer.cs:19-24`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs:16-21`

`BybitClientComposer.CreateMapper` builds from `BybitResponseProfile(symbolMapper)`, a `DeltaMapper` `Profile` subclass, following the project mandate.

---

### Finding: GET-only retry enforced — no changes to resilience pipeline
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeResiliencePipeline.cs:38-39`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeResiliencePipeline.cs:38-39`

`ExchangeResiliencePipeline.Configure` is unchanged. GET-only retry invariant preserved.

---

### Finding: No global/static mutable state introduced
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:162-164`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A
- **Pattern reference**: `ServiceCollectionExtensions.cs:65-67` (Binance CA1861 pattern)

The `long[] { 0L }` is a fresh instance created inside a singleton factory (runs once), consistent with the CA1861 pragma comment. No static mutable fields.

---

### Finding: Build clean and all non-integration tests pass
- **Severity**: N/A
- **Confidence**: 99
- **File**: `CryptoExchanges.Net.sln`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Fix**: N/A

`dotnet build` → 0 warnings, 0 errors (TreatWarningsAsErrors). `dotnet test --filter 'Category!=Integration'` → 212 tests pass (Core 68, Http 12, Bybit.Unit 77, DI.Unit 10, Binance.Integration 45). No Binance regression.

---

### Finding: ApplyEnvDefaults overload naming — non-blocking concern
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:125,222`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: Two private static methods named `ApplyEnvDefaults` are distinguished solely by parameter type (`BinanceOptions` vs `BybitOptions`). C# type-inference resolves the correct overload at each call site today, but this pattern becomes harder to reason about as more exchanges are added.
- **Fix**: When adding a third exchange, rename to `ApplyBinanceEnvDefaults` / `ApplyBybitEnvDefaults` (and so on) to make call-site binding explicit.
- **Pattern reference**: N/A

---

## Summary

- PASS: Layering — DI → Bybit ProjectReference is the correct top-layer dependency; Bybit csproj references only Core + Http.
- PASS: Named HttpClient — `AddHttpClient(BybitClientName, ...)` + `IHttpClientFactory.CreateClient(BybitClientName)` in singleton factory; no captive dependency.
- PASS: Keyed singletons — `long[]` holder, `ISymbolMapper`, `IMapper`, `IExchangeClient` all keyed by `ExchangeId.Bybit`.
- PASS: Offset holder closure — `new long[] { 0L }` per-registration, `Interlocked.Read(ref holder[0])` in signing handler lambda, `Interlocked.Exchange` in `SyncServerTimeAsync`.
- PASS: No X-BAPI-API-KEY in DefaultRequestHeaders — justified; Bybit V5 api-key is a signing-handler concern, not a universal default header.
- PASS: ReceiveWindow formatting — `decimal.ToString(CultureInfo.InvariantCulture)` produces `"5000"` matching signing test vectors; consistent between DI and factory-free paths.
- PASS: ComposeForDi ownsHttpClient: false — IHttpClientFactory owns the handler chain; no double-dispose risk.
- PASS: ValidateOnStart — fail-fast options validation mirrors Binance.
- PASS: AddCryptoExchanges — correctly delegates to both AddBinanceExchange and AddBybitExchange; Binance unaffected.
- PASS: Internal visibility — BybitHttpClient, IBybitHttpClient, BybitSigningHandler, BybitClientComposer, BybitSymbolFormat all internal; InternalsVisibleTo scoped correctly.
- PASS: No new public interface members — IMarketDataService, ITradingService, IAccountService, IExchangeClient unchanged.
- PASS: DeltaMapper profiles — BybitResponseProfile extends DeltaMapper Profile; project mandate satisfied.
- PASS: GET-only retry — ExchangeResiliencePipeline unchanged.
- PASS: No static mutable state — offset holder is a per-registration factory instance.
- PASS: Build and tests — 0 warnings, 0 errors, 212 tests pass, no Binance regression.
- CONCERN: ApplyEnvDefaults overload naming — works correctly today via type inference, but naming will become ambiguous at 3+ exchanges (confidence: 55/100, non-blocking).

---

## Final Verdict

**APPROVED** — All architectural invariants hold. The `AddBybitExchange` registration mirrors `AddBinanceExchange` structurally with justified protocol-specific differences (no `DefaultRequestHeaders` api-key, simpler `BybitHttpClient` constructor, `X-Bapi-Limit-Status` usage header). No layering violations, no captive dependencies, no public interface changes, no regression. The single concern about `ApplyEnvDefaults` overload naming is stylistic and non-blocking at current exchange count.
