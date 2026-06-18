# Architect Review — TASK-022 (Bitget final milestone M-BITGET closer)

**Verdict**: APPROVED
**Confidence**: 97/100

---

## Blocking Findings

None. All HIGH/MEDIUM-severity checks pass at or above the confidence threshold.

---

## Per-Invariant Conformance Checks

### INV-1: Core has no exchange knowledge — PASS
- `src/CryptoExchanges.Net.Core/CryptoExchanges.Net.Core.csproj` unchanged (confirmed: diff contains zero Core changes, only an Http `.csproj` addition of one `InternalsVisibleTo` line).

### INV-2: Http has no exchange knowledge — PASS
- `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj` adds exactly one line: `<InternalsVisibleTo Include="CryptoExchanges.Net.Bitget" />` (diff line 1513).
- Zero Http `.cs` file changes. The TASK-009 generalization held with no new abstraction/logic change. Confirmed against the diff.

### INV-3: Exchange internals stay internal — PASS
- Public surface: `BitgetExchangeClient`, `BitgetOptions`, `ServiceCollectionExtensions` (the last is public by necessity, exactly like OKX). All DTOs, `IBitgetHttpClient`, `BitgetErrorTranslator`, `BitgetClientComposer`, `BitgetSigningHandler`, `BitgetSignatureService`, `BitgetValueParsers`, `BitgetRequestValidation`, `BitgetMappingProfiles`, all service classes — all `internal sealed`. Confirmed by reading every new `.cs` file.
- `InternalsVisibleTo` grants: Tests.Unit, Tests.Integration, DynamicProxyGenAssembly2 (NSubstitute mock proxy). These match the Binance/OKX pattern exactly (`CryptoExchanges.Net.Bitget.csproj:19-22`).

### INV-4: Single composition root — PASS
- `BitgetClientComposer` is the sole wiring point. It has all six expected methods: `CreateMapper`, `Create`, `ComposeOver`, `ComposeWith`, `ComposeForDi`, `BuildResilientHttpClient`, plus the `NormalizeHostRoot` guard (`Internal/BitgetClientComposer.cs:1-362`).
- `BitgetExchangeClient.Create` delegates to `BitgetClientComposer.Create` (`BitgetExchangeClient.cs:167`). DI path calls `BitgetClientComposer.ComposeForDi` (`ServiceCollectionExtensions.cs:635`).

### INV-5: No new interface members — PASS
- Diff adds no properties or methods to `IMarketDataService`, `ITradingService`, `IAccountService`, or `IExchangeClient`.

### INV-6: DeltaMapper for all DTO→model mappings — PASS
- `BitgetResponseProfile` extends `Profile` from `DeltaMapper` (`BitgetMappingProfiles.cs:381`).
- Four maps: `BitgetOrder → Order`, `BitgetTicker → Ticker`, `BitgetSymbol → SymbolInfo`, `BitgetBalance → AssetBalance`.
- `AssertConfigurationIsValid()` called in `CreateMapper` (`BitgetClientComposer.cs:259`). Unit test `MapperConfiguration_IsValid` covers it.
- Direct-build exceptions (OrderBook, Candlestick, Trade, ExchangeInfo) follow the OKX precedent (not DeltaMapper-mapped) with the same rationale documented in comments.

### INV-7: Signing is a handler, not a client concern — PASS
- `BitgetSigningHandler` is a `DelegatingHandler` (`Resilience/BitgetSigningHandler.cs`). It strips and re-signs on retry (lines 70-77). The client holds `long[] _offsetHolder` and `IExchangeTimeSync _timeSync`; signing uses `() => Interlocked.Read(ref offsetHolder[0])` as the time-offset closure (`BitgetClientComposer.cs:332`).

### INV-8: Retry is GET-only — PASS
- No change to `ExchangeResiliencePipeline.Configure()`. POST endpoints (`PlaceOrderAsync`, `CancelOrderAsync`, `CancelOrderByClientIdAsync`, `CancelAllOrdersAsync`) use `http.PostAsync` which passes through the existing pipeline without enabling retry for mutating operations. The shared `HttpClientPipelineBuilder` is unchanged.

### INV-9: No captive dependency — PASS
- DI registration uses a named client (`"bitget"`) resolved via `IHttpClientFactory.CreateClient("bitget")` at singleton construction time (`ExchangeServiceRegistration.cs:124`). `IExchangeClient` is keyed singleton. No typed client used. `ownsHttpClient: false` in the DI path (`BitgetClientComposer.cs:305`).

### INV-10: Package-level coupling — CONDITIONAL PASS (see CONCERN-1)
- `AddBitgetExchange` lives in the Bitget assembly (`src/CryptoExchanges.Net.Bitget/ServiceCollectionExtensions.cs`) — correct per ADR-001. A consumer who wants only Bitget takes only the Bitget package.
- The aggregation `CryptoExchanges.Net.DependencyInjection` adds a compile-time `ProjectReference` to Bitget (`CryptoExchanges.Net.DependencyInjection.csproj:15`). This is the same pattern as for Binance/Bybit/OKX — expected for the opt-in aggregator. ADR-001 explicitly permits this.

### INV-11: Interfaces over static classes for swappable behavior — PASS
- `BitgetErrorTranslator` implements `IExchangeErrorTranslator` (interface).
- `BitgetSigningHandler` is a `DelegatingHandler` (injectable).
- Clock-skew reuses `IExchangeTimeSync` / `ExchangeTimeSync` from Core (injected via DI via `sp.GetRequiredService<IExchangeTimeSync>()`).
- `BitgetValueParsers` and `BitgetRequestValidation` are `internal static` pure-helper classes with no swappable behavior. This is correct and matches the Invariant-11 allowance for "genuinely fixed pure helpers".
- `BitgetClientComposer` is `internal static` — DI extension-method glue, correctly categorized.

### Signing handler conformance — PASS
- Strips all four `ACCESS-*` headers before re-adding them on each attempt (`BitgetSigningHandler.cs:70-77`), matching the OKX pattern exactly.
- Passphrase-empty guard throws `InvalidOperationException` (`BitgetSigningHandler.cs:42-44`), tested by `PassphraseMissing_SignedRequest_FastFails`.
- `BitgetSigningRequest.MarkSigned` / `IsSigned` pattern mirrors `OkxSigningRequest` per TASK-021.

### IExchangeTimeSync reuse (DEVIATION from file scope) — PASS
- Manifest documented the deviation: no `BitgetTimeSync.cs` created. Instead, `IExchangeTimeSync` is injected via `sp.GetRequiredService<IExchangeTimeSync>()` in `ComposeForDi` (`BitgetClientComposer.cs:302`). The container-free path uses `new ExchangeTimeSync()` directly (`BitgetClientComposer.cs:271`).
- `SyncServerTimeAsync` calls `_timeSync.ApplyOffset(serverTimeMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _offsetHolder)` (`BitgetExchangeClient.cs:187`). Correct: injected interface, not a static call.
- `ExchangeServiceRegistration.AddExchange` calls `services.TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>()` so all exchanges share one implementation.

### NormalizeHostRoot guard — PASS
- `NormalizeHostRoot` validates that `uri.AbsolutePath` is `"/"` or `""` (no path segment) and throws `ArgumentException` otherwise (`BitgetClientComposer.cs:354-360`).
- Called in BOTH paths: container-free (`BuildResilientHttpClient`, line 339) and DI (`baseUrlSelector`, `ServiceCollectionExtensions.cs:616`).
- Unit test `Di_AddBitgetExchange_BaseUrlWithPath_FailFast` covers it.

### DI: keyed singletons, named client, ValidateOnStart — PASS
- `AddBitgetExchange` delegates to `ExchangeServiceRegistration.AddExchange<BitgetOptions, IMapper>` (`ServiceCollectionExtensions.cs:607`). That method registers `IExchangeClient` as `AddKeyedSingleton`, the `IMapper` and `ISymbolMapper` as keyed singletons, and calls `ValidateOnStart()`. This is the same code path used by Binance/Bybit/OKX — no deviation.

### offsetHolder pattern — PASS
- `long[] _offsetHolder` held in `BitgetExchangeClient` (`BitgetExchangeClient.cs:121`), shared with the signing handler via `() => Interlocked.Read(ref offsetHolder[0])` closure. `SyncServerTimeAsync` writes to it via `_timeSync.ApplyOffset`. Matches the Binance/OKX pattern exactly.

### Error code map — PASS
- `"00000"` is never matched by any typed-exception branch (`BitgetErrorTranslator.cs:32-33` returns `ExchangeApiException` immediately when `code == SuccessCode`). This is verified by the test `ErrorTranslator_SuccessCode_IsNotAnError`.
- `ParseCode("00000")` returns `0` (numeric), not `null`. This is correct; `int.TryParse("00000", ...)` succeeds as integer 0.
- `ReadString` guards `JsonElement.ValueKind == JsonValueKind.String` before calling `GetString()` (`BitgetErrorTranslator.cs:549`), satisfying ADR-001 conv 3.

---

## Non-Blocking Concerns

### CONCERN-1: OKX does not have NormalizeHostRoot; Bitget does — minor asymmetry
- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Okx/Internal/OkxClientComposer.cs:100` vs `src/CryptoExchanges.Net.Bitget/Internal/BitgetClientComposer.cs:351-360`
- **Issue**: OKX's `BuildResilientHttpClient` simply does `options.BaseUrl.TrimEnd('/')` with no path-segment guard; `AddOkxExchange` passes `baseUrlSelector: o => o.BaseUrl` raw. Bitget adds `NormalizeHostRoot` for the same reason (signed prehash uses `RequestUri.AbsolutePath`). The asymmetry is intentional and documented (TASK-021 CONCERN#1), but a future OKX deploy with an unexpected path-suffix URL would silently produce bad signatures without a fast-fail. Not blocking because OKX's default URL is correct and the Bitget improvement is additive; however, backporting the guard to OKX would close the gap.
- **Suggestion**: As a separate task, add the same `NormalizeHostRoot`-style guard to `OkxClientComposer.BuildResilientHttpClient` and `AddOkxExchange.baseUrlSelector` for consistency and defense-in-depth.

### CONCERN-2: `CryptoExchangesOptions` grows linearly per exchange with no type-safe structure
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:99-114`
- **Issue**: With 4 exchanges, `CryptoExchangesOptions` now has 14 nullable flat string properties (`BinanceBaseUrl`, `BinanceApiKey`, `BinanceSecretKey`, `BybitBaseUrl`, ..., `BitgetPassphrase`). For 5 or 6 exchanges this becomes unwieldy; a per-exchange nested options type (e.g. `BinanceOptions BinanceOverrides { get; set; }`) would be more ergonomic for consumers. Pre-existing structural smell that TASK-022 clones faithfully; cloning it a 4th time doesn't materially worsen the issue but it has now crossed the threshold where it's worth noting.
- **Suggestion**: Consider a follow-up to restructure `CryptoExchangesOptions` into per-exchange nested objects before a fifth exchange is added.

### CONCERN-3: Simplifier-flagged shared debt across all 4 exchange assemblies
- **Severity**: LOW
- **Confidence**: 70
- **File**: All exchange assemblies (Binance/Bybit/OKX/Bitget)
- **Issue**: The simplifier previously flagged shared patterns across exchange assemblies: `volatile`/atomic swap in `SymbolMapper`, `DisposeAsync` boilerplate in each client, and redundant `using` directives. With 4 assemblies now all following the same pattern, a single cleanup pass would normalize all four simultaneously rather than requiring per-exchange tasks. Not blocking, but the debt compounds with each added exchange.
- **Suggestion**: Schedule a `nazgul-simplify` pass targeting all four exchange assemblies together as a milestone-close cleanup.

---

## Milestone-Close Macro Architecture Note (Binance + Bybit + OKX + Bitget)

### Did the abstraction strategy pay off?

Yes, clearly. The "generalize-after-Bybit-against-OKX" approach (TASK-009) produced a genuinely reusable `ExchangeServiceRegistration.AddExchange<TOptions, TMapper>` helper that Bitget consumed with no new Core or Http changes. The TASK-022 diff confirms this: the only Http change is the one-line `InternalsVisibleTo` grant; there are zero Core changes. The per-exchange variation points (symbol format, signing handler, error translator, host-root guard) are cleanly injected as lambdas into `AddExchange`, keeping each exchange assembly self-contained.

The OKX precedent for passphrase-gated signing (TASK-010/017 carry-in) was adopted cleanly in Bitget without reopening the Http layer. The `IExchangeTimeSync` Core reuse from REF-001/REF-002 eliminated the need for a per-exchange `BitgetTimeSync` class — the deviation is architecturally sound and reduces duplication across the 4 exchanges.

### Residual cross-exchange tech debt

**1. Static pure-helper code duplication (low criticality now, grows with exchange count).** Each exchange has its own `XxxValueParsers` and `XxxRequestValidation` as `internal static` classes. The parsers share a common pattern (`ParseDecimal`, `ParseMs`, `ParseAssetOrNone`) but are per-exchange to handle wire-format specifics. This is intentional and justified; the risk is if a parsing bug is fixed in one assembly and the others are not updated. Worth considering a `Core.Parsing` shared helper for the common subset (empty-string-to-zero, epoch-ms TryParse) that all parsers delegate to.

**2. `DisposeAsync` boilerplate in all 4 clients (very low).** Each client has the same `ValueTask DisposeAsync()` body: dispose the owned `HttpClient`, return `Task.CompletedTask`. This is idiomatic C# and the simplifier flagged it as a cleanup candidate; a shared `ExchangeClientBase` protected method is one option, but the boilerplate is trivial and the duplication is not load-bearing.

**3. The simplifier's `volatile` flag.** `SymbolMapper._wireToSymbol` uses `volatile` + atomic swap. This pattern is correct and consistent across all exchanges. The simplifier can remove redundant `using` directives and trim insignificant whitespace across all 4 assemblies in a single cleanup pass without architectural risk.

**4. `CryptoExchangesOptions` flat-string growth (noted in CONCERN-2 above).** This is the most structurally compound item. At 4 exchanges it is manageable; at 6 it is a consumer-usability issue.

**Overall milestone verdict**: The 4-exchange architecture is clean, dependency-direction compliant, and the generalization strategy proved sound. The code compiles at 0 warnings/errors with TreatWarningsAsErrors. All 92 Bitget unit tests pass; no regression in Binance, Bybit, OKX, Core, Http, or DI tests.

