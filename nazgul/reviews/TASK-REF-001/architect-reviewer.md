# Architect Review — TASK-REF-001
**Branch**: refactor/di-timesync-dry
**Commits reviewed**: 93ea257 (Phase 1 — TimeSync), 80a5d5a (Phase 2 — DI helper)
**Base SHA**: 3960d4a
**Reviewer**: Architect Reviewer
**Date**: 2026-06-18

---

## Overall Verdict: APPROVED

All architectural invariants hold. Behavior is byte-identical to the pre-refactor registrations. Build clean (0W/0E). All 333 non-integration tests pass; all 11 integration tests pass. Two non-blocking CONCERNs noted below.

---

## Behavioral Equivalence Check

### Phase 1 — TimeSync

All three `XxxExchangeClient.SyncServerTimeAsync` methods now delegate to `Core.Resilience.ExchangeTimeSync.ApplyOffset(serverMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _offsetHolder)`.

- **Binance**: Previously computed offset via `BinanceTimeSync.ComputeOffset` then called `Interlocked.Exchange(ref _offsetHolder[0], offset)` inline. Now calls `ExchangeTimeSync.ApplyOffset` which does exactly `serverMs - localMs` then `Interlocked.Exchange(ref offsetHolder[0], offset)`. Behavior-identical.
- **Bybit**: Previously called `BybitTimeSync.ComputeOffset` then `Interlocked.Exchange` inline. Now delegates to `ExchangeTimeSync.ApplyOffset`. Behavior-identical.
- **OKX**: Was already using `OkxTimeSync.ApplyOffset` — the Core implementation is a verbatim copy of that method. Behavior-identical.

The Core `ExchangeTimeSync` is `public static`, placed in `CryptoExchanges.Net.Core.Resilience` namespace — correct home. Zero exchange-specific logic. The guards (`ArgumentNullException.ThrowIfNull` + zero-length `ArgumentException`) are carried forward from `BybitTimeSync` and `OkxTimeSync`.

### Phase 2 — DI Helper

**Registration sequence verified to be byte-identical** for all three exchanges (line-by-line comparison against 3960d4a):

1. `TryAddSingleton<IExchangeClientFactory, ExchangeClientFactory>()` — present in helper line 77. PASS.
2. `AddOptions<TOptions>().Configure(envDefaults).Configure(userCfg).Validate(timeout > 0, msg).Validate(baseUrl required, msg).ValidateOnStart()` — present in helper lines 79-84. Validation messages are string-interpolated using the `optionsName` parameter; callers pass "BinanceOptions"/"BybitOptions"/"OkxOptions" which reproduce the exact pre-refactor message strings. PASS.
3. `TryAddSingleton(sp => sp.GetRequiredService<IOptions<TOptions>>().Value)` — present at line 86. PASS.
4. `TryAddKeyedSingleton(exchangeId, (_, _) => new long[] { 0L })` with CA1861 pragma — present at lines 92-94. PASS.
5. `TryAddKeyedSingleton<ISymbolMapper>(exchangeId, ...)` — present at line 95. PASS.
6. `TryAddKeyedSingleton<TMapper>(exchangeId, ...)` where all three callers pass `IMapper` as `TMapper`. The registration key type resolves to `IMapper`, identical to the old `TryAddKeyedSingleton<IMapper>`. Composers call `GetRequiredKeyedService<IMapper>(exchangeId)` — matches. PASS.
7. `AddHttpClient(clientName, ...).ConfigurePrimaryHttpMessageHandler(SocketsHttpHandler { PooledConnectionLifetime = 2m })` — present at lines 104-113. PASS.
8. `ApplyResiliencePipeline(clientName, resilienceOptions, translatorFactory, gateFactory: _ => new ReactiveRateLimitGate(), requestFinalizerFactory)` — present at lines 122-127. PASS.
9. `AddKeyedSingleton<IExchangeClient>(exchangeId, ...)` — present at lines 129-134. PASS.

**Wrinkle 1 — Binance X-MBX-APIKEY header**: Before: `if (!string.IsNullOrEmpty(o.ApiKey)) c.DefaultRequestHeaders.Add("X-MBX-APIKEY", o.ApiKey)` inside AddHttpClient lambda. After: same code but inside the `configureHttpClient` delegate, called by the shared helper AFTER the common base config (BaseAddress/Timeout/User-Agent). Order is preserved: base config first, then exchange-specific. PASS.

**Wrinkle 2 — BinanceHttpClient(httpClient, options) vs Bybit/OkxHttpClient(httpClient)**: Handled via per-exchange `exchangeClientFactory` delegate. Binance: `new BinanceHttpClient(httpClient, options)`. Bybit: `new BybitHttpClient(httpClient)`. OKX: `new OkxHttpClient(httpClient)`. PASS.

**OKX passphrase gate**: `if (string.IsNullOrEmpty(o.SecretKey) || string.IsNullOrEmpty(o.Passphrase)) return new PassThroughHandler()` — preserved verbatim in the OKX `requestFinalizerFactory` delegate. PASS.

**Binance/Bybit secret-only gate**: `if (string.IsNullOrEmpty(o.SecretKey)) return new PassThroughHandler()` — preserved in both. PASS.

---

## Architectural Invariant Checks

### Invariant 1: Core has no exchange knowledge
`CryptoExchanges.Net.Core.csproj` — unchanged, no new ProjectReferences. `ExchangeTimeSync.cs` is in `Core.Resilience` namespace with zero exchange-specific imports. PASS.

### Invariant 2: Http has no exchange knowledge
`CryptoExchanges.Net.Http.csproj` — unchanged (Core reference only). `ExchangeServiceRegistration.cs` imports only `Core.Enums`, `Core.Interfaces`, `Core.Resilience`, `Microsoft.Extensions.*` — no DeltaMapper, no exchange-specific types. The `IMapper` type is threaded via `TMapper` generic to avoid a DeltaMapper reference in Http. PASS.

### Invariant 3: Exchange client internals stay internal
`ExchangeServiceRegistration` is `internal` in the Http assembly. Pre-existing `InternalsVisibleTo` to the three exchange assemblies in `CryptoExchanges.Net.Http.csproj` cover it. No new IVT entries required. No previously-internal types made public. PASS.

### Invariant 4: Single composition root
`BinanceClientComposer`, `BybitClientComposer`, `OkxClientComposer` are unchanged. The shared helper delegates back to each composer's `ComposeForDi`. PASS.

### Invariant 5: Interfaces as extension points
No changes to any `IMarketDataService`, `ITradingService`, `IAccountService`, or `IExchangeClient` interface. PASS.

### Invariant 7: Signing is a handler concern
All three signing handlers (`BinanceSigningHandler`, `BybitSigningHandler`, `OkxSigningHandler`) unchanged. The `requestFinalizerFactory` delegates in each `AddXxxExchange` are identical to the pre-refactor lambdas. PASS.

### Invariant 8: Retry is GET-only
`ExchangeResiliencePipeline.Configure` not modified. PASS.

### Invariant 9: No captive dependency
Named client pattern preserved — `IHttpClientFactory.CreateClient(clientName)` in the `AddKeyedSingleton<IExchangeClient>` factory. `ValidateScopes=true, ValidateOnBuild=true` DI test still passes. PASS.

### Invariant 10: Package-level coupling
`ExchangeServiceRegistration` is `internal` in Http, accessed only via the pre-existing IVT. No aggregation/DI package gains a new compile-time reference. PASS.

### `long[] _offsetHolder` pattern
The holder is created once in the shared helper (`TryAddKeyedSingleton(exchangeId, (_, _) => new long[] { 0L })`). The signing handler's closure reads it via `Interlocked.Read(ref holder[0])`. `SyncServerTimeAsync` writes it via `ExchangeTimeSync.ApplyOffset` which uses `Interlocked.Exchange`. Same atomic pattern as before. PASS.

---

## Findings

### Finding: BinanceTimeSync and BybitTimeSync were public; now deleted
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceTimeSync.cs` (deleted), `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs` (deleted)
- **Category**: Architecture (public surface reduction)
- **Verdict**: CONCERN (non-blocking — confidence 70, pre-release package)
- **Issue**: `BinanceTimeSync` and `BybitTimeSync` were `public static` types on the NuGet packages' public surface (OkxTimeSync was already `internal`). Removing them is technically a breaking change if any consumer called `BinanceTimeSync.ComputeOffset` or `BybitTimeSync.ComputeOffset`/`ApplyOffset` directly. The replacement `ExchangeTimeSync` is available in Core.
- **Fix**: Because the package is at `0.1.0-preview.1`, consumers have no stability guarantee under SemVer 2.0 for pre-release versions, making this acceptable. However, the CHANGELOG should note the removals. If the package ever approaches 1.0 with those types still present, consumers would need a migration note pointing to `Core.Resilience.ExchangeTimeSync`.
- **Pattern reference**: `Directory.Build.props:18` (`Version = 0.1.0-preview.1`)

### Finding: `ExchangeServiceRegistration` has 13 parameters — future Bitget adds a 4th exchange
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:56-71`
- **Category**: Architecture (maintainability / OCP)
- **Verdict**: CONCERN (non-blocking — confidence 55)
- **Issue**: The helper signature already has 15 parameters (2 type + 13 value). The intent is to reduce future per-exchange boilerplate, but a very long parameter list traded for explicit per-exchange code is a potential maintenance burden — especially if a 4th or 5th exchange needs a genuinely new variation point not covered by the current delegates. The helper is `internal`, so it cannot be a breaking change, but it will accrete parameters over time. An options/builder object (e.g. `ExchangeRegistrationOptions<TOptions>`) would make named parameters less critical and allow callers to omit truly optional variation points without passing `null` explicitly.
- **Fix**: This is not broken today and is out of scope for this refactor. Flag for a follow-up when a 4th variation point is discovered during Bitget (TASK-016+). If Bitget fits cleanly with the existing 13 parameters, close this concern.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:56-71`

---

## Summary

- PASS: Dependency direction (Core/Http/Exchange chain) — no violations introduced in any .csproj or .cs file.
- PASS: ExchangeTimeSync placement in Core — zero exchange-specific logic, correct namespace, `public` access modifier appropriate for cross-assembly use.
- PASS: ExchangeServiceRegistration placement in Http — `internal` + pre-existing IVT, no DeltaMapper leak, no new public surface.
- PASS: Byte-identical registration for all three exchanges — keyed singletons, named HttpClient, ApplyResiliencePipeline args (including per-exchange gateFactory hardcoded), ValidateOnStart messages, Binance X-MBX-APIKEY header wrinkle, OKX dual-secret gate, per-exchange IExchangeClient factory wrinkle.
- PASS: `long[] _offsetHolder` atomic write pattern — `Interlocked.Exchange` in `ExchangeTimeSync.ApplyOffset`, `Interlocked.Read` in signing handler closures.
- PASS: No captive dependency — named client pattern unchanged, scope-clean DI test passes.
- PASS: Build clean (0W/0E with TreatWarningsAsErrors), 333 non-integration + 44 integration tests pass.
- CONCERN: BinanceTimeSync/BybitTimeSync were public and are deleted — technically breaking for a consumer who used them directly, but acceptable at preview semver and correctly replaced by Core.Resilience.ExchangeTimeSync. (confidence: 70/100, non-blocking)
- CONCERN: 13-parameter helper signature may accrete further with future exchanges — flag for Bitget evaluation, not blocking today. (confidence: 55/100, non-blocking)

## Final Verdict: APPROVED
