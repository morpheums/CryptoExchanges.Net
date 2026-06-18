# Code Review: TASK-REF-002 — Interface seams (IExchangeTimeSync + ISignatureService)

**Branch**: refactor/interface-seams  
**Reviewer**: Code Reviewer  
**Date**: 2026-06-18  
**Build**: PASS (0 warnings, 0 errors — TreatWarningsAsErrors)  
**Tests**: PASS (335 unit tests pass, 0 fail)

---

## Behavior Equivalence Audit

### BinanceSigningHandler.BuildSignedQuery inlining
**CONFIRMED BYTE-IDENTICAL.**

Pre-refactor (service method, `BinanceSignatureService.cs:27-32`):
```csharp
var signature = Sign(queryString);
var separator = string.IsNullOrEmpty(queryString) ? string.Empty : "&";
return $"{queryString}{separator}signature={signature}";
```

Post-refactor (handler private method, `BinanceSigningHandler.cs:84-88`):
```csharp
var signature = signatureService.Sign(queryString);
var separator = string.IsNullOrEmpty(queryString) ? string.Empty : "&";
return $"{queryString}{separator}signature={signature}";
```

Empty-query separator guard: PRESENT in both. Logic is byte-identical.

### SyncServerTimeAsync — IExchangeTimeSync injection
**CONFIRMED SAME EFFECT.**

All 3 clients (`BinanceExchangeClient.cs:108`, `BybitExchangeClient.cs:91`, `OkxExchangeClient.cs:536`) now call `_timeSync.ApplyOffset(serverTimeMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _offsetHolder)`.

`ExchangeTimeSync.ApplyOffset` is identical to the former static version — same `Interlocked.Exchange(ref offsetHolder[0], offset)` call, same null/length guards. The concrete `ExchangeTimeSync` instance is:
- **Factory-free path**: `new ExchangeTimeSync()` allocated in each composer's `Create()` method; the same instance passed through `ComposeOver` → `ComposeWith` → client ctor. One instance per client. CORRECT.
- **DI path**: `sp.GetRequiredService<IExchangeTimeSync>()` in each `ComposeForDi`; TryAddSingleton means one shared instance across all DI-resolved clients. CORRECT (offset holder is still per-exchange via keyed singleton `long[]`).

### Sign bodies — unchanged
**CONFIRMED UNTOUCHED.**

Diffs for all 3 signature services show only: (a) `using CryptoExchanges.Net.Core.Auth;` added, (b) `: ISignatureService` on class, (c) `/// <summary>/<param>/<returns>` replaced with `/// <inheritdoc />` on `Sign`. The computation bodies are bit-for-bit identical to base SHA 3eeb698.

### offsetHolder threading — preserved
The `long[]` offsetHolder is created once per composition path and the same array reference reaches both the signing handler's `Func<long>` closure and `SyncServerTimeAsync`. The `timeSync` parameter flows through the same composition chain but is orthogonal to the offset holder — no regression here.

---

## Findings

### Finding: BinanceSignatureService.BuildSignedQuery is dead code after this refactor
- **Severity**: MEDIUM
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:22-32`
- **Category**: Code Quality
- **Verdict**: REJECT (confidence 97, severity MEDIUM — blocking)
- **Issue**: `BuildSignedQuery(string queryString)` was the only pre-refactor call site and the handler now calls its own private copy. `grep -rn "\.BuildSignedQuery" src/ tests/` returns zero results. The class is `internal sealed` with no external callers. The method retains a full `/// <summary>/<param>/<returns>` doc block (lines 22-27), which also violates the lean-comment mandate (ADR-001, conv 7): no verbose docs on dead code. This is not a regression — the method was not added by this diff — but the refactor created the orphan and the task's Implementation Notes explicitly acknowledge it ("Extra members left as-is per scope: Binance `BuildSignedQuery` (kept)"). The task scope note is a deferral, not a justification; dead code with a public-doc block is a defect per the lean-comment convention and maintainability standard.
- **Fix**: Delete `BinanceSignatureService.cs` lines 22-32 (the `BuildSignedQuery` method and its doc block). No callers; no behavior change.
- **Pattern reference**: ADR-001 conv 7 (lean comments), implementation notes line 45 ("kept" — deferred but acknowledged).

### Finding: ExchangeTimeSync class-level doc uses `/// <inheritdoc cref="IExchangeTimeSync" />` — non-standard pattern
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Core/Resilience/ExchangeTimeSync.cs:3`
- **Category**: Style
- **Verdict**: CONCERN (non-blocking — confidence 70)
- **Issue**: Every other `/// <inheritdoc />` in the codebase is on member overrides/implementations, not class-level declarations. The `cref` form on a class doc is not wrong (it copies the interface summary into the impl's XML output) but it is the only such usage in the codebase and slightly non-idiomatic. A plain `/// <summary>Concrete <see cref="IExchangeTimeSync"/> implementation.</summary>` would be more consistent.
- **Fix** (non-blocking): Replace line 3 with `/// <summary>Concrete <see cref="IExchangeTimeSync"/> implementation.</summary>` or drop the class-level doc entirely (class is `public sealed` and the interface carries the canonical description).
- **Pattern reference**: All other `/// <inheritdoc />` usages are member-level only (e.g. `BinanceExchangeClient.cs:77`, `ISignatureService.cs` impls).

---

## All Other Checks

### Correctness
- PASS: All async methods use `.ConfigureAwait(false)` on every await — unchanged in this refactor; verified in `BinanceSigningHandler.cs`, `BybitSigningHandler.cs`, `OkxSigningHandler.cs`, all 3 client `SyncServerTimeAsync` and `PingAsync`.
- PASS: `CancellationToken` threaded and re-thrown on `ct.IsCancellationRequested` — unchanged.
- PASS: `ArgumentNullException.ThrowIfNull(timeSync)` added in all 3 client ctors.
- PASS: No new disposables introduced without `using`.

### Null safety
- PASS: `IExchangeTimeSync timeSync` parameter guarded with `ArgumentNullException.ThrowIfNull(timeSync)` in all 3 clients.
- PASS: `ISignatureService` parameter in handlers is a primary-ctor parameter; handlers are internal sealed, created only by composers who pass non-null concrete instances.
- PASS: No nullable property dereferences introduced.

### Interface docs (lean-comment rule)
- PASS: `IExchangeTimeSync` — two concise `<summary>` entries with `<exception>` where it adds value; no noise.
- PASS: `ISignatureService` — single concise `<summary>` on interface and member.
- PASS: `ExchangeTimeSync` members use `/// <inheritdoc />`.
- PASS: `BinanceSignatureService.Sign`, `BybitSignatureService.Sign`, `OkxSignatureService.Sign` all use `/// <inheritdoc />`.
- PASS: `<remarks>` blocks removed from `BybitSignatureService` and `OkxSignatureService` class docs; content folded into the `<summary>` (concise, non-redundant).
- CONCERN (logged above): `ExchangeTimeSync` class-level `/// <inheritdoc cref="..." />`.

### Registration
- PASS: `TryAddSingleton<IExchangeTimeSync, ExchangeTimeSync>()` in `ExchangeServiceRegistration.AddExchange` — exchange-agnostic, first registration wins (consumer can override). Verified by `Consumer_Can_Override_ExchangeTimeSync` test.
- PASS: No DI registration for `ISignatureService` — correct per scope (composer-constructed, not DI-resolved).

### Thread safety
- PASS: No new mutable shared state. `_timeSync` is `readonly` field, `IExchangeTimeSync` instance is stateless (its logic uses `Interlocked.Exchange` on the caller-supplied `long[]`, not instance state).

### Warning suppressions
- PASS: No new `#pragma warning disable` added. No new `<NoWarn>` entries.

### Records and value types
- PASS: No new records or value types introduced.

### Tests
- PASS: `ExchangeTimeSyncTests` updated to `new ExchangeTimeSync()` instance; all 4 original assertions preserved.
- PASS: `DiRegistrationTests` +2 tests: default registration resolves `ExchangeTimeSync`, and `TryAdd` override semantics confirmed.
- PASS: 335 unit tests pass, 0 fail.

---

## Summary

| # | Item | Verdict | Confidence |
|---|------|---------|------------|
| 1 | `BinanceSignatureService.BuildSignedQuery` dead code (lines 22-32) | REJECT (MEDIUM, blocking) | 97/100 |
| 2 | `ExchangeTimeSync` class-level `/// <inheritdoc cref="..."/>` style | CONCERN (LOW, non-blocking) | 70/100 |
| 3 | `IExchangeTimeSync` seam — behavior equivalence | PASS | — |
| 4 | `ISignatureService` seam — Sign bodies unchanged | PASS | — |
| 5 | BuildSignedQuery inlining — empty-query guard present | PASS | — |
| 6 | offsetHolder/timeSync threading through composers | PASS | — |
| 7 | Null guards on new parameters | PASS | — |
| 8 | Lean comments on new interfaces and impls | PASS | — |
| 9 | Build clean (0 warnings, 0 errors) | PASS | — |
| 10 | 335 unit tests pass | PASS | — |

---

## Final Verdict

**CHANGES_REQUESTED**

One blocking finding: `BinanceSignatureService.BuildSignedQuery` (lines 22-32) is dead code created by this refactor. The method has no callers in `src/` or `tests/` post-refactor, retains a verbose doc block that violates the lean-comment convention, and the task's own implementation notes deferred its deletion with "kept" — acknowledging the problem. Delete lines 22-32 from `BinanceSignatureService.cs`, confirm build remains clean (it will), and the PR is ready to merge.
