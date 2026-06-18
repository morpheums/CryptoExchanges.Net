---
id: TASK-REF-002
status: IMPLEMENTED
claimed_at: 2026-06-18
base_sha: 3eeb698fd117286b2a10c14e2150933655dc3534
---

> **Rework (gate round 1):** architect+code REJECT@95/97 — the inlined Binance `BuildSignedQuery` in the handler orphaned the service-side copy (zero callers). Deleted `BinanceSignatureService.BuildSignedQuery` (dead code). Build 0w/0e; 335 tests pass. security+api already APPROVED. Non-blocking carry: CHANGELOG note for the static→instance `ExchangeTimeSync` break (pre-v1.0, → PR body).

# TASK-REF-002: Interface seams for time-sync + signing (DIP, before Bitget)

**Status**: PLANNED (architect-recommended 2026-06-18; maintainer mandate — interfaces over static for swappable behavior)
**Depends on**: PR #13 (TASK-REF-001) MERGED to main — this builds on #13's `ExchangeTimeSync` + shared DI helper.
**Blast radius**: MEDIUM — Core + Http + all 3 exchanges; behavior-preserving. architect + api review.
**Own PR** (refactor, before M-BITGET / TASK-016) per the per-concern PR strategy.

## Scope (exactly this — architect ruling; do NOT interface-ify everything)
1. **`IExchangeTimeSync`** (Core.Resilience): 2 methods (`ComputeOffset`, `ApplyOffset`). `ExchangeTimeSync` becomes the instance impl (`: IExchangeTimeSync`). Register once, non-keyed: `TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>()` in `ExchangeServiceRegistration` (exchange-agnostic). Thread through the 3 `XxxExchangeClient` ctors + 3 composers (`ComposeWith`/`ComposeOver`/`ComposeForDi` resolve/pass it; factory-free `Create` passes `new ExchangeTimeSync()`). ~10 files, mechanical.
2. **`ISignatureService`** (Core.Auth): `string Sign(string payload)`. `BinanceSignatureService` / `BybitSignatureService` / `OkxSignatureService` implement it; each `XxxSigningHandler` ctor takes `ISignatureService` instead of the concrete type. No DI registration (composer-constructed). ~3 files/exchange.
3. **KEEP STATIC** (do NOT touch): `HmacSignature` + `SignatureEncoding` (pure crypto primitive), `XxxClientComposer`, `ExchangeServiceRegistration`, `ServiceCollectionExtensions`, pipeline builders.
4. Lean comments throughout: docs on the new interfaces; `<inheritdoc />` on impls; no noise (ADR-001 conv 7).

## Acceptance
1. `ExchangeTimeSync` is injected via `IExchangeTimeSync`; no static call sites remain in the clients. A consumer can override the DI registration to swap it.
2. The 3 signature services share `ISignatureService`; handlers depend on the interface.
3. Behavior byte-identical; full build clean (TreatWarningsAsErrors); ALL tests pass; no Binance/Bybit/OKX regression.
4. Bitget (TASK-016+) uses both interface seams from the start.

## Notes
- Gated on #13 merge → branch off main → TASK-REF-002 → PR/merge → then M-BITGET.
- Retro comment sweep of existing code tracked separately as GitHub issue #14 (going-forward enforcement is already live via the reviewer agents + ADR-001).

## Implementation Notes (2026-06-18)
Branch `refactor/interface-seams`, base SHA 3eeb698. Behavior-preserving (DIP) refactor.

### Two interface shapes
- `IExchangeTimeSync` (Core.Resilience): `long ComputeOffset(long serverTimeMs, long localNowMs)` + `long ApplyOffset(long serverTimeMs, long localNowMs, long[] offsetHolder)`.
- `ISignatureService` (Core.Auth): `string Sign(string payload)`.

### Seam 1 — IExchangeTimeSync
- `ExchangeTimeSync` converted from `static class` → `public sealed class ExchangeTimeSync : IExchangeTimeSync`; methods are now instance methods with `/// <inheritdoc />`. Logic byte-identical (same ComputeOffset subtraction + Interlocked.Exchange + null/length guards).
- Registered ONCE, non-keyed, inside `ExchangeServiceRegistration.AddExchange`: `services.TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>();` (exchange-agnostic; TryAdd → a consumer's prior registration wins → swappable).
- Threaded into all 3 clients: each `XxxExchangeClient` ctor gains an `IExchangeTimeSync timeSync` param (+ null guard + `_timeSync` field); `SyncServerTimeAsync` now calls `_timeSync.ApplyOffset(...)` instead of the static `ExchangeTimeSync.ApplyOffset(...)`.
- Threaded through all 3 composers, mirroring the existing offsetHolder threading: `ComposeOver`/`ComposeWith` take `IExchangeTimeSync`; factory-free `Create(options)` supplies `new ExchangeTimeSync()`; `ComposeForDi(sp, …)` resolves `sp.GetRequiredService<IExchangeTimeSync>()`. The per-exchange `ServiceCollectionExtensions.exchangeClientFactory` lambdas were UNCHANGED (they already delegate to `ComposeForDi(sp, …)`, which now resolves the seam).

### Seam 2 — ISignatureService
- `BinanceSignatureService` / `BybitSignatureService` / `OkxSignatureService` now `: ISignatureService`; their core `Sign(string)` got `/// <inheritdoc />` and the redundant doc blocks were removed. Extra members left as-is per scope: Binance `BuildSignedQuery` (kept), Bybit static `BuildGet/PostSignString`, OKX static `BuildPrehash`/`FormatTimestamp`.
- Each `XxxSigningHandler` ctor param changed from the concrete `XxxSignatureService` → `ISignatureService` (same concrete instance passed by the composer/finalizer, just typed as the interface at the handler boundary — no DI registration added).
- Binance handler used `signatureService.BuildSignedQuery(...)` (not on the interface); inlined as a private `BuildSignedQuery` helper that calls the interface `Sign` — wire output byte-identical (`q + "&signature=" + Sign(q)`).

### Kept static (architect ruling — NOT touched)
`HmacSignature` + `SignatureEncoding` (pure crypto primitive), all 3 `XxxClientComposer` (still `static`), `ExchangeServiceRegistration` (static, only added the one TryAddSingleton line), per-exchange `ServiceCollectionExtensions`, `HttpClientPipelineBuilder`, `ExchangeResiliencePipeline`.

### Behavior equivalence
Same instances, same call graph, same wire bytes. The only registration change is the added non-keyed `IExchangeTimeSync` singleton + handler ctor param TYPE change (concrete→interface, identical instance passed). No public surface regression: the two new interfaces + `ExchangeTimeSync` are public in Core; signature services remain `internal` (they just also implement the public interface).

### Tests
- `ExchangeTimeSyncTests` updated to use `new ExchangeTimeSync()` (concrete `_sut` field — CA1859), all original assertions kept.
- `DiRegistrationTests` +2: `Registers_ExchangeTimeSync_AsDefault` (resolves `ExchangeTimeSync`) and `Consumer_Can_Override_ExchangeTimeSync` (TryAdd override wins).

### Verification
- `dotnet build CryptoExchanges.Net.sln` → 0 warnings, 0 errors (TreatWarningsAsErrors).
- `dotnet test --filter 'Category!=Integration'` → 335 pass, 0 fail (baseline 333 + 2 new DI tests).
- `dotnet test --filter 'Category=Integration'` → 11 pass (Bybit 5 + OKX 6), 0 fail.
- Override confirmed via `Consumer_Can_Override_ExchangeTimeSync` (TryAdd semantics).

## Commits
- 83da9ed — feat(REF-002): interface seams IExchangeTimeSync + ISignatureService (DIP)
