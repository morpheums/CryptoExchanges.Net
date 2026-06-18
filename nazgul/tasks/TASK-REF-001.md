---
id: TASK-REF-001
status: IN_PROGRESS
claimed_at: 2026-06-18
base_sha: 3960d4a2271d519e72c81dee147977510233d531
---

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
