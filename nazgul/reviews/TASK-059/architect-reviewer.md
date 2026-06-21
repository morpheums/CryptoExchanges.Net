---
reviewer: architect-reviewer
task: TASK-059
verdict: APPROVE
---

## Summary

TASK-059 delivers the KuCoin HTTP client, three service implementations, the composer, the entry-point `KucoinExchangeClient`, and a full unit test suite (149 passing). All eight hard constraints verified: the 4-layer dependency chain is clean, Http.csproj carries no DeltaMapper/Core pollution from the new `InternalsVisibleTo` node, DI extensions are deferred to TASK-060, retry is enforced GET-only by the shared `ExchangeResiliencePipeline`, and the composer faithfully implements both the factory (`Create`) and DI (`ComposeForDi`) paths. The build is green with zero warnings (`TreatWarningsAsErrors=true`).

## Hard Constraint Verification

**HC-1 — 4-layer chain**: `CryptoExchanges.Net.Kucoin.csproj` references only `CryptoExchanges.Net.Core` and `CryptoExchanges.Net.Http`. No DI or sibling-exchange project references present. PASS.

**HC-2 (K1) — Http.csproj purity**: The only change to `CryptoExchanges.Net.Http.csproj` is adding `<InternalsVisibleTo Include="CryptoExchanges.Net.Kucoin" />`. The file still carries exactly one `<ProjectReference>` (to Core) and one `<PackageReference>` (`Microsoft.Extensions.Http.Resilience`). No DeltaMapper or Core.Models type is referenced from Http. PASS.

**HC-3 — InternalsVisibleTo direction**: `CryptoExchanges.Net.Kucoin` is added to Http.csproj's `InternalsVisibleTo` list alongside the four existing peer exchanges (Binance, Bybit, OKX, Bitget). Dependency direction Kucoin→Http is unchanged; the `InternalsVisibleTo` attribute confers visibility in one direction only and does not invert it. Pattern is identical to those peers. PASS.

**HC-4 (ADR-001) — DI deferred**: `KucoinClientComposer` provides `ComposeForDi()` for use by TASK-060 but adds no `IServiceCollection` extension methods or DI wire-up of its own. PASS.

**HC-5 (K2/K3) — Retry GET-only**: `ExchangeResiliencePipeline.Configure()` (Http project) gates retry on `req.Method != HttpMethod.Get` and short-circuits to `ValueTask.FromResult(false)` for all other verbs. `KucoinHttpClient.PostAsync` and `DeleteAsync` do not bypass the pipeline; they flow through the same `HttpClient` instance and are therefore automatically excluded from retry by that predicate. No separate flag or method is required. PASS.

**HC-6 — One type per file**: Every new `.cs` file in the diff contains exactly one top-level type declaration. PASS.

**HC-7 — DeltaMapper mandate**: All DTO→`Core.Models` mappings (Order, Fill, SymbolInfo, Balance, Ticker) go through `KucoinResponseProfile : Profile`. The inline constructions in `KucoinMarketDataService` (`OrderBook`, `Candlestick`, `Trade`) are for types whose multi-source or array-indexed inputs make a DeltaMapper profile impractical — exactly the pattern documented in `BinanceMappingProfiles.cs:55-59` for value-parser-driven mappings. PASS.

**HC-8 — Composer pattern**: `KucoinClientComposer` is the single composition root. It provides `Create(KucoinOptions)` (factory path), `ComposeOver` (test/factory internal), `ComposeWith` (shared wiring), `ComposeForDi` (DI path), and `BuildResilientHttpClient`. Shape is byte-for-byte parallel to `OkxClientComposer`. PASS.

## Findings

### Finding: `KucoinRequestValidation` is a `static class` — but so is the established pattern
- **Severity**: LOW
- **Confidence**: 30
- **File**: `src/CryptoExchanges.Net.Kucoin/Internal/KucoinRequestValidation.cs:7`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: Invariant 11 flags `static class` for swappable behavior. `KucoinRequestValidation` holds only pure guard-clause helpers (`ValidateHistoryWindow`, `MaxHistoryLimit`). The behavior is not swappable — it encodes fixed KuCoin V1 server-side limits. The same `internal static class` pattern is used in `BinanceRequestValidation`, `OkxRequestValidation`, `BybitRequestValidation`, and `BitgetRequestValidation` across the codebase.
- **Fix**: No action required. `static class` for genuinely fixed pure helpers is explicitly allowed under Invariant 11.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceRequestValidation.cs:7`

### Finding: `KucoinClientComposer` is a `static class` — consistent with all peer composers
- **Severity**: LOW
- **Confidence**: 20
- **File**: `src/CryptoExchanges.Net.Kucoin/Internal/KucoinClientComposer.cs:16`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: Invariant 11 flags `static class` for swappable behavior. `KucoinClientComposer` is DI-glue: it is the composition root, not a swappable behavior type. The identical `internal static class` shape is used in `BinanceClientComposer` and `OkxClientComposer`.
- **Fix**: None.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Internal/OkxClientComposer.cs:16`

### CONCERN: `_supportedSymbols` lazy-cache in `KucoinMarketDataService` uses a bare `Lazy<Task<>>` — pattern carries a latent fire-and-forget risk shared across all exchange services
- **Severity**: LOW
- **Confidence**: 40
- **File**: `src/CryptoExchanges.Net.Kucoin/Services/KucoinMarketDataService.cs:502-521`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The `Lazy<Task<IReadOnlyDictionary<...>>>` with `ExecutionAndPublication` is now the third copy of this pattern (Binance, OKX, KuCoin). If the first awaiter throws, the exception is cached in the `Lazy` and all subsequent callers receive the faulted task — no retry path. This is a pre-existing pattern issue, not introduced by this diff, but cloning it a third time makes it marginally worse. If a transient network error occurs on first boot, callers must recreate the client to recover.
- **Fix**: Non-blocking. If this becomes a pain point, consider wrapping the `Lazy` in a reset-on-fault variant (e.g. reset `_supportedSymbols` to `null` when the task faults). Track as a separate refactor across all exchanges.
- **Pattern reference**: The same pattern exists in `src/CryptoExchanges.Net.Okx/Services/OkxMarketDataService.cs`.

## Summary Table

- PASS: 4-layer dependency chain — Kucoin.csproj references only Core + Http, no DI/sibling references.
- PASS: Http.csproj K1 purity — new `InternalsVisibleTo` node introduces no Core/DeltaMapper refs; file still has one ProjectReference (Core only).
- PASS: InternalsVisibleTo direction — identical to the four existing peer exchanges; no dependency inversion.
- PASS: ADR-001 DI deferred — `ComposeForDi()` stub present; no `IServiceCollection` extensions added yet.
- PASS: Retry GET-only (K2/K3) — `ExchangeResiliencePipeline` guards POST/DELETE at the pipeline level; `KucoinHttpClient` flows through the same `HttpClient`.
- PASS: One type per file — every new `.cs` file in the diff declares exactly one top-level type.
- PASS: DeltaMapper mandate — all structured DTO→model mappings use `KucoinResponseProfile`; array-indexed/value-parser constructions follow the documented BinanceMappingProfiles exemption pattern.
- PASS: Composer pattern — `Create` + `ComposeForDi` both present; single composition root; parallel to `OkxClientComposer`.
- PASS: `_offsetHolder` pattern — `long[] { 0L }` allocated in `Create`, threaded through `BuildResilientHttpClient` → signing handler closure → `KucoinExchangeClient._offsetHolder` → `SyncServerTimeAsync`; matches Binance/OKX precedent.
- PASS: Build — `dotnet build` succeeds, 0 warnings, 0 errors (`TreatWarningsAsErrors=true`).
- PASS: Tests — 149/149 passing, including signed/unsigned marking, limit clamping, error envelope propagation, and clock-sync verification.
- CONCERN: Lazy-cache fire-and-forget fault pattern (non-blocking, pre-existing across 3 exchanges) — confidence 40/100.
