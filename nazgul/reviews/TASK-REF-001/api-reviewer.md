# API Review — TASK-REF-001: Extract shared per-exchange DI/TimeSync (DRY before Bitget)

**Branch**: refactor/di-timesync-dry
**Commits**: 93ea257 (Phase 1 — TimeSync to Core), 80a5d5a (Phase 2 — shared DI helper)
**Reviewer**: API Reviewer
**Date**: 2026-06-18

---

## Overall Verdict: APPROVED

All checks pass. No blocking issues found.

---

## Finding 1: BinanceTimeSync and BybitTimeSync were public — their deletion is a breaking change

- **Severity**: HIGH
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceTimeSync.cs` (deleted), `src/CryptoExchanges.Net.Bybit/Resilience/BybitTimeSync.cs` (deleted)
- **Category**: Compatibility
- **Verdict**: PASS (pre-v1.0 preview, intentional, documented)

At base SHA 3960d4a, `BinanceTimeSync` (namespace `CryptoExchanges.Net.Binance.Resilience`) was `public static class` with one public method `ComputeOffset`. `BybitTimeSync` (namespace `CryptoExchanges.Net.Bybit.Resilience`) was likewise `public static class` with public `ComputeOffset` and `ApplyOffset`. Both are now deleted.

`OkxTimeSync` was `internal static class` — its deletion is safe with zero visibility concern.

The removals of `BinanceTimeSync` and `BybitTimeSync` are technically breaking for any consumer who referenced them directly. However: (a) this is `0.1.0-preview.1` where breaking changes are explicitly acceptable with notice; (b) the task manifest documents this as intentional, behavior-preserving; (c) the replacement `Core.Resilience.ExchangeTimeSync` is a strict superset (adds `ApplyOffset`, keeps `ComputeOffset` with identical semantics); (d) no samples or integration tests in the active codebase reference the deleted types directly. The change is correctly classified as intentional and pre-v1.0 acceptable.

**Fix**: No code change needed. Recommend a CHANGELOG or release notes entry noting the removal of `BinanceTimeSync` and `BybitTimeSync` and the migration path (`ExchangeTimeSync.ComputeOffset` / `ExchangeTimeSync.ApplyOffset` in `CryptoExchanges.Net.Core.Resilience`).

---

## Finding 2: ExchangeTimeSync is added to the Core public surface

- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Resilience/ExchangeTimeSync.cs:17`
- **Category**: API Design
- **Verdict**: PASS

`ExchangeTimeSync` is `public static class` in `CryptoExchanges.Net.Core.Resilience`. Naming is consistent with the existing `ResilienceOptions` in the same namespace. All public members carry full XML `<summary>`, `<param>`, `<returns>`, and `<exception>` docs. `ComputeOffset` and `ApplyOffset` are pure/static with no exchange-specific logic. The `ArgumentNullException.ThrowIfNull` + length guard on `ApplyOffset` match the defensive patterns in the existing codebase. `GenerateDocumentationFile` is inherited from `Directory.Build.props:8`. No issues.

---

## Finding 3: ExchangeServiceRegistration is internal — public surface of AddXxxExchange methods is unchanged

- **Severity**: HIGH
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:22`, `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs:32`, `src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs:33`, `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs:32`
- **Category**: Compatibility
- **Verdict**: PASS

`ExchangeServiceRegistration` is `internal static class` in the Http assembly. It is reachable from the three exchange assemblies via the pre-existing `InternalsVisibleTo` entries in `CryptoExchanges.Net.Http.csproj` (`CryptoExchanges.Net.Binance`, `CryptoExchanges.Net.Bybit`, `CryptoExchanges.Net.Okx`). No new IVT entries were added. The helper is not exposed to consumer application projects.

The three public signatures — `AddBinanceExchange(IServiceCollection, Action<BinanceOptions>?)`, `AddBybitExchange(IServiceCollection, Action<BybitOptions>?)`, `AddOkxExchange(IServiceCollection, Action<OkxOptions>?)` — are unchanged. Each is still `public static IServiceCollection` and expression-bodies correctly return the `IServiceCollection` from the helper's `return services` at line 136 of the helper.

---

## Finding 4: IMapper keyed singleton registration key type is preserved

- **Severity**: MEDIUM
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:96`
- **Category**: Compatibility
- **Verdict**: PASS

The original per-exchange registrations used `TryAddKeyedSingleton<IMapper>(exchangeId, ...)`. The helper uses `TryAddKeyedSingleton<TMapper>(exchangeId, ...)` where `TMapper` is a generic type parameter constrained to `class`. All three callers pass `IMapper` as `TMapper` (`AddExchange<BinanceOptions, IMapper>`, `AddExchange<BybitOptions, IMapper>`, `AddExchange<OkxOptions, IMapper>`), so the effective call is `TryAddKeyedSingleton<IMapper>` — behavior-identical to the original. The composers resolve via `GetRequiredKeyedService<IMapper>(exchangeId)` which remains valid.

---

## Finding 5: gateFactory hardcoded in helper — no per-exchange variation lost

- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:126`
- **Category**: API Design
- **Verdict**: PASS

`gateFactory: _ => new ReactiveRateLimitGate()` is hardcoded in the helper. This is correct: the original per-exchange `ServiceCollectionExtensions` all passed the same `_ => new ReactiveRateLimitGate()`. No variation existed, so hardcoding it in the helper is safe. If a future exchange needs a different gate, the parameter can be added then. The manifest explicitly notes this decision.

---

## Finding 6: Behavior equivalence — Binance SyncServerTimeAsync uses ApplyOffset atomically

- **Severity**: MEDIUM
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:103-105`
- **Category**: Compatibility
- **Verdict**: PASS

Before: `var offset = BinanceTimeSync.ComputeOffset(resp.ServerTime, localNowMs); Interlocked.Exchange(ref _offsetHolder[0], offset)` — two lines, both inside the async method, effectively atomic for the write.

After: `ExchangeTimeSync.ApplyOffset(resp.ServerTime, localNowMs, _offsetHolder)` — single call that does `Interlocked.Exchange(ref offsetHolder[0], offset)` internally. Behavior-identical. The return value of `ApplyOffset` (the written offset) is discarded, which is correct — callers only need the side-effect on the holder.

---

## Finding 7: NuGet and build conventions

- **Severity**: LOW
- **Confidence**: 99
- **File**: `Directory.Build.props:8`, `src/CryptoExchanges.Net.Core/CryptoExchanges.Net.Core.csproj`
- **Category**: NuGet Conventions
- **Verdict**: PASS

No new `src/` project was added. The new files are added to existing projects (`Core` and `Http`). `ExchangeTimeSync.cs` is added to `CryptoExchanges.Net.Core` which already has `PackageId`, `Description`, `PackageLicenseExpression` in csproj, and `GenerateDocumentationFile` via `Directory.Build.props`. `ExchangeServiceRegistration.cs` is added to `CryptoExchanges.Net.Http` which is not packaged standalone (it is an internal dependency). No `IsPackable` issues introduced.

---

## Summary

- PASS: Deletion of `BinanceTimeSync` (public) and `BybitTimeSync` (public) — intentional breaking change at preview version; no active consumer references; migration path exists via `ExchangeTimeSync` in Core. Recommend CHANGELOG entry.
- PASS: Deletion of `OkxTimeSync` (internal) — no visibility concern, safe removal.
- PASS: New `ExchangeTimeSync` in `Core.Resilience` — correct namespace, complete XML docs, sensible public surface (two statics, no exchange-specific logic), behavior-identical to the three deleted types combined.
- PASS: `ExchangeServiceRegistration` is correctly `internal` + uses pre-existing IVT; no new IVT entries; `AddBinanceExchange`/`AddBybitExchange`/`AddOkxExchange` public signatures are unchanged.
- PASS: `IMapper` keyed singleton key type preserved — all three callers pass `IMapper` as `TMapper`.
- PASS: `gateFactory` hardcoded correctly; `ReactiveRateLimitGate` was identical across all three original registrations.
- PASS: All three `SyncServerTimeAsync` implementations are behavior-identical after replacing two-line pattern with `ApplyOffset`.
- PASS: NuGet/build conventions — no new projects, `GenerateDocumentationFile` inherited, `TreatWarningsAsErrors` satisfied (build 0W/0E verified in manifest).
- PASS: No interface members added or removed in any Core interface. No model record properties removed. No enum values reordered.
