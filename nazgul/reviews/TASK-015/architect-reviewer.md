# Architect Review — TASK-015
**Task**: OKX services + mapping + error + time + tests + AddOkxExchange DI (closes M-OKX)
**Date**: 2026-06-18
**Reviewer**: Architect Reviewer
**Branch**: feat/m3-okx (HEAD)
**Build**: 0 errors, 0 warnings (TreatWarningsAsErrors=true confirmed)

---

## Checklist walkthrough

### [1] Layer dependency chain — Core has no knowledge of OKX; Http has no knowledge of OKX

PASS. `CryptoExchanges.Net.Okx.csproj` references only `Core` and `Http`. No reverse reference added. The diff adds `CryptoExchanges.Net.Okx` to the DI aggregator's `ProjectReference` list, which is the correct direction (DI -> exchange assembly, per ADR-001 invariant #10).

### [2] AddOkxExchange lives in the OKX assembly (ADR-001 Decision 1)

PASS. `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs` contains `AddOkxExchange`. The DI aggregator `ServiceCollectionExtensions.cs` is a thin delegator: it calls `services.AddOkxExchange(opt => { ... })` passing `CryptoExchangesOptions` fields. No exchange-specific construction leaks into the aggregator — keyed singletons, named HttpClient, resilience pipeline, and signing gate all live in the OKX assembly. Confirmed.

### [3] OkxErrorTranslator + OkxTimeSync are INTERNAL (ADR-001 conv #2)

PASS.
- `OkxErrorTranslator`: `internal sealed class` at `src/CryptoExchanges.Net.Okx/Resilience/OkxErrorTranslator.cs:18`
- `OkxTimeSync`: `internal static class` at `src/CryptoExchanges.Net.Okx/Resilience/OkxTimeSync.cs:13`
- `OkxSigningHandler`: `internal sealed class` at `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:17`
- `OkxClientComposer`: `internal static class` at `src/CryptoExchanges.Net.Okx/Internal/OkxClientComposer.cs:15`
- `OkxSignatureService`: `internal sealed class` at `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs:16`

Integration tests access these types through the assembly's `InternalsVisibleTo` grants (`CryptoExchanges.Net.Okx.csproj:19-20`), which is the correct pattern.

### [4] DI registration mirrors Bybit pattern

PASS. Verified line-by-line against `src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs`:
- Keyed singletons: `long[]` offset holder, `ISymbolMapper`, `IMapper`, `IExchangeClient` — all keyed by `ExchangeId.Okx`
- Named HttpClient "okx" (not typed) — prevents captive dependency. `IHttpClientFactory.CreateClient(OkxClientName)` resolved inside the singleton factory.
- `ApplyResiliencePipeline` with `OkxErrorTranslator` + `ReactiveRateLimitGate`
- Secret+passphrase-gated `requestFinalizerFactory`: `PassThroughHandler` when `SecretKey` OR `Passphrase` is empty/null, else `OkxSigningHandler` (OKX's extra passphrase credential correctly added to the gate)
- `ValidateOnStart` + `TreatWarningsAsErrors` passes
- `TryAddSingleton<IExchangeClientFactory>` for factory registration

One OKX-specific difference from Bybit: `ResilienceOptions()` has no `UsageHeaderName` (Bybit uses `X-Bapi-Limit-Status`). This is correct — OKX does not expose a usage-header field, the reactive gate fires on Retry-After from 429 responses.

### [5] Public surface: only OkxExchangeClient + OkxOptions (+ AddOkxExchange host) public

PASS. Confirmed by grep across `src/CryptoExchanges.Net.Okx/`. Public types:
- `OkxExchangeClient` (intended)
- `OkxOptions` (intended)
- `ServiceCollectionExtensions` (intended — AddOkxExchange host)

All DTOs, services, mapper profiles, internal helpers, signing types, error translator, time sync, and composer are `internal`. Confirmed.

Note: `OkxResponseProfile.OkxResponseProfile(ISymbolMapper)` constructor is `public` within an `internal sealed class` — this is a no-op from an API surface standpoint (the containing class is internal, so the public constructor has the effective visibility of the class). Not a finding.

Similarly, `OkxValueParsers` methods are `public static` within `internal static class OkxValueParsers`. Same situation — internal container, effective visibility is internal. Pattern matches Binance/Bybit. Not a finding.

### [6] BinanceClientComposer pattern followed — single composition root

PASS. `OkxClientComposer` mirrors `BinanceClientComposer` exactly:
- `CreateMapper(ISymbolMapper)` — builds + validates DeltaMapper config
- `Create(OkxOptions)` — container-free path, calls `BuildResilientHttpClient` then `ComposeOver`
- `ComposeOver(...)` — creates `SymbolMapper`, calls `ComposeWith`
- `ComposeWith(...)` — constructs three services and `OkxExchangeClient`
- `ComposeForDi(IServiceProvider, ...)` — resolves keyed mapper + symbolMapper from DI, sets `ownsHttpClient: false`
- `BuildResilientHttpClient(OkxOptions, long[])` — builds the factory-less pipeline

The only addition over the reference is `BuildResilientHttpClient` being `public static` within an `internal` class (enabling test access through InternalsVisibleTo). This matches the Bybit pattern exactly.

### [7] DeltaMapper profiles for DTO->model mappings

PASS. `OkxResponseProfile : Profile` in `src/CryptoExchanges.Net.Okx/Mapping/OkxMappingProfiles.cs` covers:
- `OkxOrder -> Order` (all scalar fields, including CumulativeQuoteQuantity = accFillSz * avgPx)
- `OkxTicker -> Ticker` (PriceChange and PriceChangePercent computed from last/open24h with divide-by-zero guard)
- `OkxInstrument -> SymbolInfo` (symbol from components; filter fields Ignored pending a dedicated task — documented)
- `OkxBalanceDetail -> AssetBalance`

`AssertConfigurationIsValid()` invoked in `OkxClientComposer.CreateMapper`. Trade, OrderBook, and Candlestick are built directly (documented exception — matches Binance/Bybit precedent). No AutoMapper references.

### [8] POST/DELETE endpoints — retry correctly disabled

PASS. Retry is enforced GET-only in `ExchangeResiliencePipeline.Configure()` (`ShouldHandle` at `src/CryptoExchanges.Net.Http/ExchangeResiliencePipeline.cs:38`): `if (req?.Method != HttpMethod.Get) return ValueTask.FromResult(false)`. All OKX mutating endpoints (place order, cancel, cancel-batch) use POST and are not retried. Confirmed unchanged.

### [9] Signing is a DelegatingHandler — not a client concern

PASS. `OkxSigningHandler` extends `DelegatingHandler`. It reads the request's `RequestUri.PathAndQuery` + body to assemble the OKX prehash string, strips any prior OK-ACCESS-* headers on retry (fresh header set per attempt), and is inserted into the handler chain as `requestFinalizer` below the Polly retry layer so each attempt gets a fresh ISO-8601 timestamp. `OkxExchangeClient` holds no credentials.

### [10] clock-skew offsetHolder pattern

PASS. Single-element `long[] _offsetHolder` declared in `OkxExchangeClient` (line 407), passed into `OkxSigningHandler`'s constructor via closure (`() => Interlocked.Read(ref holder[0])`), and updated atomically via `Interlocked.Exchange` in `SyncServerTimeAsync`. Exactly mirrors Binance/Bybit.

### [11] No global/static mutable state

PASS. No static mutable fields introduced. `OkxSymbolFormat.Instance` is a static readonly value (not mutable). `OkxClientComposer` methods are pure static helpers. `_supportedSymbols` in `OkxMarketDataService` is instance-level (not static) and uses a double-check lock pattern — consistent with Bybit's approach.

### [12] No new members added to Core interfaces

PASS. Checked `src/CryptoExchanges.Net.Core/Interfaces/`. No additions to `IMarketDataService`, `ITradingService`, `IAccountService`, `IExchangeClient`, or `ISymbolMapper` in this diff.

### [13] No captive dependency

PASS. The `IExchangeClient` keyed singleton resolves `IHttpClientFactory.CreateClient(OkxClientName)` at construction time (a named client, not typed). `OkxHttpClient` wraps this `HttpClient` directly. DNS rotation is delegated to `SocketsHttpHandler.PooledConnectionLifetime` on the primary handler.

### [14] Package-level coupling — invariant #10

PASS for OKX specifically. `AddOkxExchange` lives in the OKX assembly. A consumer who `dotnet add package CryptoExchanges.Net.Okx` gets OKX only. The aggregation DI package's new `ProjectReference` to Okx is correct — it is the aggregator's job to pull all of them. No sibling exchange assembly forces itself into a consumer who only wants OKX.

---

## Findings

### Finding: `public const SpotInstType` / `MaxHistoryLimit` inside `internal` class — no real exposure but style inconsistency

- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Okx/Internal/OkxRequestValidation.cs:14,22,31`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: Constants and the validation method are declared `public` inside an `internal static class`. While the effective visibility is `internal` (C# access modifier of the containing type wins), this creates a misleading signal for future maintainers and produces analysis noise. The Binance and Bybit equivalents use `internal` on their helper members.
- **Fix**: Change `public const string SpotInstType`, `public const int MaxHistoryLimit`, and `public static void ValidateHistoryWindow` to `internal`. No downstream impact (they are only referenced within the assembly).
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceRequestValidation.cs` (uses `internal` on constants and helpers)

---

### Finding: `OkxResponseProfile` constructor declared `public` inside `internal sealed class`

- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Okx/Mapping/OkxMappingProfiles.cs:20`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: Same stylistic issue — `public OkxResponseProfile(ISymbolMapper symbolMapper)` inside `internal sealed class OkxResponseProfile`. The constructor is only callable from within this assembly (or InternalsVisibleTo targets). No functional harm, but it is inconsistent with Bybit's `BybitResponseProfile` which uses `public` for the same reason (DeltaMapper's `cfg.AddProfile` convention). This is actually a cross-pattern inconsistency worth noting: if DeltaMapper's `cfg.AddProfile` requires a public constructor, then keeping it public is correct. If it does not (and the call is internal-only), aligning to `internal` reduces noise.
- **Fix**: No immediate action required. If DeltaMapper's profile registration accepts internal constructors, change to `internal`. Otherwise leave as-is and add a comment explaining the DeltaMapper requirement.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Mapping/BybitMappingProfiles.cs` (same public constructor in internal class — pre-existing pattern)

---

### Finding: Stale duplicate comment in `OkxHttpClient.PostJsonAsync`

- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs:77`
- **Category**: Architecture (code hygiene)
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The private `PostJsonAsync` method retains the comment "OKX V5 POST is JSON-bodied; the signing handler signs the exact raw body string it reads back, so the serialized JSON must be the wire body verbatim." — which is a copy from the original single-overload method. The comment belongs at the overload level, not duplicated in the shared private helper. Harmless but adds noise.
- **Fix**: Remove the duplicated comment from `PostJsonAsync`; the calling overloads carry the relevant documentation.
- **Pattern reference**: N/A (style)

---

### Finding (MILESTONE-BOUNDARY, non-blocking): Three-exchange duplication now materially compounds before Bitget

- **Severity**: MEDIUM
- **Confidence**: 75
- **File**: `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs`, `src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs`, `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs`
- **Category**: Architecture (macro)
- **Verdict**: CONCERN (non-blocking — confidence 75 < 80, and this is the milestone macro note)
- **Issue**: Three `ServiceCollectionExtensions` files are now structurally near-identical (~120 lines each, ~375 total). After normalizing exchange names, the diff between Bybit and OKX is only 8 lines (passphrase gate, no ReceiveWindow, no UsageHeaderName, one extra env-var read). This pattern compounds to ~500 lines before Bitget reaches DONE. Concrete duplication visible today:
  1. The "keyed singleton registration block" (offset holder, ISymbolMapper, IMapper) is identical across all three — 15 lines × 3 = 45 lines with zero variation.
  2. The `AddHttpClient(...).ConfigurePrimaryHttpMessageHandler(SocketsHttpHandler)` + `ApplyResiliencePipeline` block structure is identical except for `ResilienceOptions` fields — ~30 lines × 3.
  3. `ApplyEnvDefaults` shape is identical (passphrase is the only OKX addition).
  4. Three `ClientComposer` files (~87-102 lines each) have identical `CreateMapper`, `ComposeOver`, `ComposeWith`, `ComposeForDi` skeletons — only `BuildResilientHttpClient` and type names differ.

  Similarly, `CryptoExchangesOptions` in the DI aggregator grows by 3-4 nullable string properties per exchange — it will have ~20+ properties at Bitget+.

- **Fix (recommended before Bitget)**: Two targeted extractions:
  1. **`ExchangeRegistrationHelper`** (or extension method on `IServiceCollection`) in `CryptoExchanges.Net.Core` or a new `CryptoExchanges.Net.Http` extension: extract the keyed-singleton block (offset holder + ISymbolMapper + IMapper) into a `AddExchangeKeyedSingletons(this IServiceCollection, ExchangeId, SymbolFormat, Func<ISymbolMapper,IMapper> mapperFactory)` helper. The three calls become 3 lines each with type-specific arguments only.
  2. **`HttpClientBuilderExtensions.ApplyStandardResiliencePipeline`**: the `ApplyResiliencePipeline` block is already an extension on `IHttpClientBuilder` (via the Http assembly's `ApplyResiliencePipeline`). Consider whether the `AddHttpClient + ConfigurePrimaryHttpMessageHandler + ApplyResiliencePipeline` sequence can be collapsed to a single `AddExchangeHttpClient(exchangeName, optionsFactory, resilienceOptions, translatorFactory, gateFactory, finalizerFactory)` that returns the keyed singleton factory.
  3. For the `ClientComposer` files: accept the current duplication if `BuildResilientHttpClient` diverges enough per exchange (it does: Binance adds `X-MBX-APIKEY` default header, OKX adds passphrase gate). Focus on the shared skeleton (CreateMapper/ComposeOver/ComposeWith/ComposeForDi) — a generic base or a source-generation T4 template is feasible; leave for a dedicated refactor task rather than blocking Bitget.
  
  Concrete action: create a `TASK-REF-001: Extract per-exchange DI helper` task before Bitget implementation begins. Not blocking this milestone.

- **Pattern reference**: All three `ServiceCollectionExtensions.cs` files (identical structure)

---

## Build and Test Evidence

- `dotnet build CryptoExchanges.Net.sln -c Debug --no-incremental` → **Build succeeded. 0 Warning(s). 0 Error(s).**
- Manifest reports: `dotnet test --filter 'Category!=Integration'` → 91 OKX unit + 93 Core + 12 Http + 80 Bybit + 11 DI + 45 Binance(unit) = ALL pass; `dotnet test --filter 'Category=Integration'` → 6 OKX + 5 Bybit = ALL pass.

---

## Summary

- PASS: Layer chain — Core, Http, Okx deps are correct; no upward references introduced
- PASS: AddOkxExchange in OKX assembly — ADR-001 Decision 1 honoured
- PASS: OkxErrorTranslator + OkxTimeSync + OkxSigningHandler — all `internal`
- PASS: Public surface — only `OkxExchangeClient` + `OkxOptions` + `ServiceCollectionExtensions` host; all other types internal
- PASS: DI registration mirrors Bybit — keyed singletons, named client, offset holder, secret+passphrase gate, ValidateOnStart
- PASS: OkxClientComposer follows BinanceClientComposer pattern — Create/ComposeOver/ComposeWith/ComposeForDi/BuildResilientHttpClient
- PASS: DeltaMapper Profile — OkxResponseProfile covers all four DTO→model maps; AssertConfigurationIsValid called
- PASS: Retry is GET-only — ExchangeResiliencePipeline unchanged; POST/DELETE endpoints not retried
- PASS: Signing is a DelegatingHandler — OkxSigningHandler, not client concern; re-signs per attempt
- PASS: offsetHolder pattern — single-element long[], Interlocked.Exchange in SyncServerTimeAsync, Interlocked.Read in handler closure
- PASS: No global mutable state introduced
- PASS: No Core interface members added
- PASS: No captive dependency — named client resolved via IHttpClientFactory
- PASS: Package coupling — OKX consumers pull OKX only; DI aggregator correctly references all exchanges
- CONCERN: `public` access modifiers inside `internal` classes in `OkxRequestValidation` (confidence: 60/100, non-blocking)
- CONCERN: `public` constructor on `OkxResponseProfile` (internal class) — pre-existing Bybit pattern, requires DeltaMapper API check (confidence: 55/100, non-blocking)
- CONCERN: Stale duplicate comment in `PostJsonAsync` (confidence: 70/100, non-blocking)
- CONCERN (MILESTONE MACRO): Three-exchange DI + Composer duplication now materially compounds; concrete refactor recommended before Bitget (confidence: 75/100, non-blocking)

---

## Milestone Architecture Note — M-OKX Aggregate (Binance + Bybit + OKX)

Three exchanges are now DONE. The aggregate diff shows the pattern is internally consistent and ADR-001 has been correctly applied. No blocking structural debt. However, four latent compounding issues are now material and will worsen with Bitget:

1. **`ServiceCollectionExtensions` × 3**: ~375 lines, ~90% structurally identical. The four-step registration block (keyed singletons / named HttpClient / ApplyResiliencePipeline / keyed IExchangeClient singleton) is copy-paste. Bitget adds ~120 more lines.
2. **`XxxClientComposer` × 3**: ~288 lines total. The five-method skeleton (CreateMapper/ComposeOver/ComposeWith/ComposeForDi/BuildResilientHttpClient) is near-identical; only `BuildResilientHttpClient` diverges meaningfully per exchange.
3. **`CryptoExchangesOptions`**: grows by 3-4 properties per exchange. Will have 16+ nullable strings at Bitget, all hand-edited in the same class.
4. **`XxxTimeSync` × 3**: `OkxTimeSync`, `BybitTimeSync`, `BinanceTimeSync` each have two identical static methods (`ComputeOffset` / `ApplyOffset`). Could be a single `TimeSync` utility in `Core`.

Recommended actions before Bitget (in priority order): (a) Extract `TimeSync` utility into `Core` — it has zero exchange-specific logic. (b) Create a shared DI helper for the keyed-singleton block. (c) Accept Composer duplication for now (BuildResilientHttpClient diverges enough). These are non-blocking here but will be significantly cheaper to do before a 4th exchange than after.

---

## Final Verdict

**APPROVED**

All architectural invariants pass. Four non-blocking CONCERNs raised (all confidence < 80). Builds clean. Tests pass. M-OKX is closed in good structural shape.
