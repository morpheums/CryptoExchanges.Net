---
id: TASK-REF-001
status: DONE
claimed_at: 2026-06-18
base_sha: 3960d4a2271d519e72c81dee147977510233d531
---

> **Post-gate follow-on (2026-06-18):** maintainer flagged excessive comments + static-over-interface. Trimmed `ExchangeTimeSync` (44→18 lines) + the DI helper's class-doc essay on this branch; established the lean-comment + interface-over-static conventions in code-reviewer/architect agents + ADR-001 (commit 3991cef). The interface conversions themselves (IExchangeTimeSync + ISignatureService) are split out as **TASK-REF-002** (architect-scoped) to run before Bitget. Retro comment sweep → GitHub issue #14.

> **Review Gate: PASSED (round 1)** — all 4 APPROVE (architect, code, security, api); zero blocking. Behavior verified byte-identical (registration order, both wrinkles, finalizer gating, SyncServerTime equivalence; TimeSync test consolidation is a strict superset). Commits 93ea257 (Phase 1) + 80a5d5a (Phase 2). Non-blocking carry-forwards: CHANGELOG note for the public BinanceTimeSync/BybitTimeSync deletion (done in PR body); null-guards on helper delegates + gateFactory-hardcoded + 13-param signature → re-evaluate at the Bitget boundary.

# TASK-REF-001: Extract shared per-exchange DI/TimeSync (DRY before Bitget)

**Milestone**: M-OKX→M-BITGET bridge (refactor)
**Wave**: between Wave 10 (M-OKX) and Wave 11 (M-BITGET)
**Status**: PLANNED (decided 2026-06-18 — architect milestone-macro note + user approval)
**Depends on**: PR #12 (M-OKX) MERGED to main — this refactor touches OKX code, so it must branch off a main that contains OKX.
**Blast radius**: HIGH — touches all three exchange assemblies + Core/Http shared code. REQUIRES architect + api review. Must not change behavior.

## Why
After 3 exchanges (Binance, Bybit, OKX) the per-exchange pattern shows ~90%-identical duplication that will compound when Bitget repeats it a 4th time:
- `ServiceCollectionExtensions` ×3 (~375 lines, ~90% identical; Bybit↔OKX differ ~8 lines)
- `XxxClientComposer` ×3 (only `BuildResilientHttpClient` diverges)
- `XxxTimeSync` ×3 (identical `ComputeOffset`/`ApplyOffset`, zero exchange-specific logic)
- `CryptoExchangesOptions` (+3–4 nullable strings per exchange)

## Scope (architect-recommended order)
1. **Move TimeSync to Core** — replace `BinanceTimeSync`/`BybitTimeSync`/`OkxTimeSync` with a single Core `ExchangeTimeSync` (`ComputeOffset` + `ApplyOffset` with the zero-length guard). Update the 3 exchanges to use it; delete the 3 copies.
2. **Shared keyed-singleton DI helper** — extract the common `AddXxxExchange` body (keyed long[] holder / ISymbolMapper / IMapper / IExchangeClient registration, named HttpClient, ApplyResiliencePipeline, ValidateOnStart) into a shared helper (likely in Http or a small DI-support type), parameterized by the per-exchange variation points (options type, ExchangeId, client name, usage header, symbol format, mapper factory, error translator, finalizer factory, composer ComposeForDi). Each AddXxxExchange becomes a thin call into it.
3. **Leave Composer duplication** as-is (the per-exchange composer is mostly justified; `BuildResilientHttpClient` is the only real divergence and is small). Re-evaluate if it becomes painful.
- Consider whether `CryptoExchangesOptions` should become a small per-exchange-options dictionary/sub-objects (optional; only if clean).

## Acceptance Criteria
1. TimeSync lives once in Core; the 3 per-exchange TimeSync types are gone; all signing/SyncServerTime paths use the Core type.
2. The 3 `AddXxxExchange` methods delegate to the shared helper; registration behavior is byte-identical (keyed singletons, named client, resilience pipeline, secret[/passphrase]-gated finalizer, ValidateOnStart).
3. Full solution builds clean (TreatWarningsAsErrors); ALL tests pass (current: 336 non-integration + 11 integration); zero behavior regression for Binance/Bybit/OKX.
4. Bitget (TASK-017+) then uses the shared helper from the start — net new code per future exchange drops substantially.

## Notes
- This is its OWN PR (refactor/di-timesync-dry → main), separate from the Bitget feature PR, per the per-exchange/per-concern PR strategy.
- Sequence: PR #12 merges → branch off main → TASK-REF-001 → PR/merge → then M-BITGET (TASK-016+).

## Polish candidates folded in from the /simplify pass (2026-06-18)
A `/simplify` review of the OKX milestone found the OKX code clean on reuse/efficiency/altitude. The only items were cross-exchange harmonizations that belong HERE (applying them OKX-only would diverge from the Binance/Bybit idioms). Fold these in while harmonizing — apply consistently across ALL exchanges, not one:
- **Shared HttpClient configuration**: BaseAddress(TrimEnd '/') + Timeout + User-Agent "CryptoExchanges.Net/0.1.0" is duplicated between each `XxxClientComposer.BuildResilientHttpClient` and each `AddXxxExchange` AddHttpClient lambda (×3 exchanges, 2 sites each). A shared `ConfigureExchangeHttpClient(HttpClient, baseUrl, timeoutSeconds, apiKeyHeader?)` would remove it.
- **OK-ACCESS-* / X-BAPI-* / X-MBX-* header names** are inline string literals repeated (Remove+Add) in each signing handler — could be per-handler `const`s (low value; only do if touching those files).
- **Auth/rate-limit/balance error-code sets** use long `code is "x" or "y" or …` chains in each XxxErrorTranslator — a `static readonly HashSet<string>` (or frozen set) per category reads better for the longer OKX/Bitget lists. Apply per-translator only if it improves readability; keep consistent across exchanges.
- **JSON options block** + **BuildQueryString** are identical across the 3 HttpClients — candidates for a shared Http helper (consider alongside the DI helper).

## Implementation Notes

### Phase 1 — TimeSync → Core
- New Core primitive: `CryptoExchanges.Net.Core.Resilience.ExchangeTimeSync` (public, full XML docs) at
  `src/CryptoExchanges.Net.Core/Resilience/ExchangeTimeSync.cs`. Methods:
  - `static long ComputeOffset(long serverTimeMs, long localNowMs) => serverTimeMs - localNowMs;`
  - `static long ApplyOffset(long serverTimeMs, long localNowMs, long[] offsetHolder)` — null-guard +
    zero-length guard (`ArgumentException`) + `Interlocked.Exchange(ref offsetHolder[0], offset)` + return offset.
    This is exactly the previous `OkxTimeSync.ApplyOffset`.
- Public because it is a Core shared primitive consumed cross-assembly by all three exchanges.
- DELETED the three per-exchange types: `BinanceTimeSync`, `BybitTimeSync`, `OkxTimeSync`.
- Standardized ALL three `XxxExchangeClient.SyncServerTimeAsync` on
  `Core.Resilience.ExchangeTimeSync.ApplyOffset(serverMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _offsetHolder)`.
  Binance/Bybit previously inlined the `Interlocked.Exchange` after a separate `ComputeOffset`; they now use the
  same atomic `ApplyOffset` (still computes server−local then writes index 0 — behavior-identical). Referenced via the
  fully-qualified `Core.Resilience.ExchangeTimeSync` to keep the using-block diff minimal.
- Tests repointed: consolidated the per-exchange TimeSync unit tests into one
  `tests/CryptoExchanges.Net.Core.Tests.Unit/ExchangeTimeSyncTests.cs` (ComputeOffset sign/magnitude, ApplyOffset
  write+return, zero-length-holder guard, plus a new null-holder guard). Removed the `OkxTimeSync`/`BybitTimeSync`
  test blocks from the two signing-test files (kept their `Resilience` usings — still needed for ErrorTranslator/
  SignatureService) and deleted `BinanceTimeSyncTests.cs` (its sole ComputeOffset test moved to Core).

### Phase 2 — Shared keyed-singleton DI helper
- Helper: `internal static class ExchangeServiceRegistration` with
  `public static IServiceCollection AddExchange<TOptions, TMapper>(...)` at
  `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs`.
- **Location/visibility**: Http (it already hosts `ExchangeClientFactory` + the resilience pipeline seam). `internal`
  + the pre-existing `InternalsVisibleTo` to the three exchange assemblies in `CryptoExchanges.Net.Http.csproj` —
  matches `ExchangeClientFactory`'s posture exactly (no new IVT entries needed). No public-surface change.
- **No DeltaMapper leak into Http**: the `IMapper` keyed singleton is registered via the generic type parameter
  `TMapper` (caller supplies `IMapper`), so Http needs no DeltaMapper reference.
- **Variation points (all delegate-parameterized)**: `ExchangeId` key, client name, options type-name (for identical
  validation messages), env-defaults applier (`Action<TOptions>`), user `configure`, `timeoutSecondsSelector` +
  `baseUrlSelector` (`Func<TOptions,int>` / `Func<TOptions,string>` — chosen over an `IExchangeOptions` interface
  precisely to avoid touching the public options types), symbol-mapper factory (`Func<ISymbolMapper>`), IMapper
  factory (`Func<ISymbolMapper,TMapper>` = `XxxClientComposer.CreateMapper`), optional HttpClient configurer
  (`Action<HttpClient,TOptions>?`), `ResilienceOptions` (carries the usage-header name), translator factory, request-
  finalizer factory, and the IExchangeClient factory (`Func<IServiceProvider,HttpClient,long[],IExchangeClient>`).
  The rate-limit gate is `new ReactiveRateLimitGate()` hardcoded in the helper (identical across all three).
- **Wrinkle 1 — Binance api-key header**: Binance sets a default `X-MBX-APIKEY` header on the HttpClient; Bybit/OKX
  do not. Handled via the optional `configureHttpClient` delegate (Binance passes the header-adding action; Bybit/OKX
  pass `null`). The shared base-config (BaseAddress `TrimEnd('/')` + Timeout + User-Agent "CryptoExchanges.Net/0.1.0")
  runs first inside the helper, then the per-exchange configurer (this folds the /simplify shared-HttpClient-config
  candidate for the AddXxx path).
- **Wrinkle 2 — BinanceHttpClient ctor takes options**: `BinanceHttpClient(httpClient, options)` vs
  `Bybit/OkxHttpClient(httpClient)`. Handled via the `exchangeClientFactory` delegate, which builds the typed http
  client + calls `XxxClientComposer.ComposeForDi(sp, http, holder)` per-exchange.
- Each `AddXxxExchange` is now a thin expression-bodied call into the helper. Behavior is byte-identical: same
  `TryAddSingleton<IExchangeClientFactory>`, same `AddOptions().Configure().Validate().ValidateOnStart()` with the
  same messages, same keyed `long[]`/`ISymbolMapper`/`IMapper`/`IExchangeClient` registrations (TryAdd vs Add as
  before), same named client + `ConfigurePrimaryHttpMessageHandler`, same `ApplyResiliencePipeline` args, same
  secret[/passphrase]-gated `PassThroughHandler` finalizer.
- **Lines**: the three `ServiceCollectionExtensions` went from 124/125/129 (=378) to 81/76/81 (=238) — −140 lines of
  duplication, replaced by one 138-line helper (~75 of which are XML docs/comments; ~63 lines of actual shared logic
  that now exists once instead of ×3, and will be reused by Bitget for free).

### Kept duplicated (deliberately, to avoid behavior risk / out of scope)
- **`XxxClientComposer` (incl. `BuildResilientHttpClient`)** — left as-is per the manifest ("LEAVE the per-exchange
  XxxClientComposer duplication as-is"). The base-HttpClient-config duplication on the container-free `Create()` path
  therefore still lives in each composer; only the DI/`AddXxx` side was de-duplicated. Re-evaluate when it becomes painful.
- **Signing-handler header-name literals**, **error-code `or`-chains**, and the **HttpClient JSON-options/BuildQueryString**
  blocks (other /simplify candidates) — NOT touched. They live in files outside the DI/TimeSync scope of this task and
  removing them carries no behavior benefit relevant to this refactor; deferred to keep the blast radius contained.

## Verification

Baseline (before changes, HEAD 3960d4a): build 0W/0E; non-integration 336 pass; integration 11 pass.

### Phase 1 (commit 93ea257)
- `dotnet build CryptoExchanges.Net.sln` → Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test --filter 'Category!=Integration'` → 333 passed, 0 failed
  (336 − 7 removed per-exchange TimeSync tests + 4 consolidated Core tests).
- `dotnet test --filter 'Category=Integration'` → 11 passed, 0 failed (Bybit 5 + OKX 6).

### Phase 2 (commit 80a5d5a)
- `dotnet build CryptoExchanges.Net.sln` → Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test --filter 'Category!=Integration'` → 333 passed, 0 failed.
- `dotnet test --filter 'Category=Integration'` → 11 passed, 0 failed (Bybit 5 + OKX 6).
- DI behavioral equivalence covered by `DiRegistrationTests` (keyed `IExchangeClient` resolution, IMapper singleton,
  no-unkeyed registration, `ValidateOnStart` fail-fast on `TimeoutSeconds=0`, Bybit-only registration, and a
  `ValidateScopes=true, ValidateOnBuild=true` scope-clean graph check that would catch any captive dependency) — all pass.

## Commits
- `93ea257` — refactor(REF-001): move TimeSync to Core (Phase 1)
- `80a5d5a` — refactor(REF-001): shared keyed-singleton DI helper (Phase 2)
