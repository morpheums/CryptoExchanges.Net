# Architect Review — TASK-023 (PR #16 Self-Review Remediation, Group A + Group B)

**Diff scope**: commits `524e017` and `0bee170` on top of `c5027e4`

---

### Finding 1: Strict layering preserved — Core is clean
- **Severity**: LOW (confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/CryptoExchanges.Net.Core.csproj`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `ExchangeUrl` lives in `CryptoExchanges.Net.Http`, not Core. The `ExchangeTimeSync.ApplyOffset` throw on `serverTimeMs <= 0` is retained as defense-in-depth in Core; the new `if (serverTimeMs > 0)` guards live in each exchange client. Core has no new deps.

---

### Finding 2: Http has no exchange-specific knowledge
- **Severity**: LOW (confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/ExchangeUrl.cs:1-49`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `ExchangeUrl` is exchange-agnostic. The XML doc explains the prehash invariant as a general principle that applies to any exchange whose signing reads `RequestUri.AbsolutePath/Query` — it does not name Bitget or OKX in the type definition. The word "OKX/Bitget" appears only in the summary comment as explanatory context, not as a code dependency. No exchange namespace is imported.

---

### Finding 3: InternalsVisibleTo — correct and consistent
- **Severity**: LOW (confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj:12`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Adding `CryptoExchanges.Net.Http.Tests.Unit` to `InternalsVisibleTo` exactly mirrors the pattern for `ExchangeServiceRegistration` (all exchange assemblies are already listed). The test project is unit-only (no `ProjectReference` to any exchange assembly), so this does not violate ADR-001 package coupling. The four exchange assemblies were already present; the test assembly addition is the only new entry.

---

### Finding 4: `ExchangeUrl` is `internal static` — invariant 11 (DIP / interfaces over statics) check
- **Severity**: MEDIUM
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Http/ExchangeUrl.cs:12`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: Invariant 11 flags `static class`es for swappable behavior. `ExchangeUrl` contains two functions: `BuildQueryString` (pure, deterministic URL encoding — no swappable behavior) and `NormalizeHostRoot` (pure validation — also not swappable behavior; no exchange would need to replace "reject non-host-root URLs" with a different policy). These are genuinely fixed pure helpers, exactly the category Invariant 11 explicitly exempts. However the rule says "when unsure, prefer the interface" — a future maintainer who wants to inject mock URL logic in tests cannot do so here without `InternalsVisibleTo`. The current tests call the static directly (which works via `InternalsVisibleTo`), so testability is not impaired. Given the purely deterministic nature of both methods and the `internal` visibility, this falls squarely in the exempted category; no interface is required. Flagging at LOW confidence for documentation only.
- **Fix**: No action needed. If a future exchange needs custom query encoding (e.g., non-`Uri.EscapeDataString` percent encoding), extract an `IQueryStringBuilder` at that point.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:1` (correctly `internal static` for DI glue)

---

### Finding 5: `NormalizeHostRoot` asymmetry — Bybit skips it, Binance skips it
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitClientComposer.cs:97`, `src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs:48`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: Bybit's container-free path uses `options.BaseUrl.TrimEnd('/')` (not `NormalizeHostRoot`) and its DI `baseUrlSelector` is `o => o.BaseUrl` (raw, not validated). Same for Binance. This is intentional and correct: Bybit signs only the query string (`request.RequestUri?.Query`), not `AbsolutePath`, so a path segment in BaseUrl would not silently corrupt the signature. Binance signs via query parameter too. The `NormalizeHostRoot` guard is strictly necessary only for OKX and Bitget, whose signing handlers read `RequestUri.PathAndQuery` or `RequestUri.AbsolutePath` to build the prehash. The diff correctly adds `NormalizeHostRoot` to OKX (previously was bare `.TrimEnd('/')`) and keeps it on Bitget. The asymmetry is therefore architecturally sound. The only risk is a future developer who copies a Bybit-style composer for a signing-sensitive exchange and misses the guard. A code comment in `BybitClientComposer.cs` noting "NormalizeHostRoot not needed here — Bybit signs query only, not AbsolutePath" would prevent that mistake.
- **Fix**: No functional change needed. Optionally add a one-line comment to `BybitClientComposer.cs:97` explaining the intentional raw TrimEnd.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeUrl.cs:31-48` (NormalizeHostRoot doc explains when it is needed)

---

### Finding 6: Double `.TrimEnd('/')` in DI path for OKX/Bitget
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:100`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `ExchangeServiceRegistration.AddExchange` calls `baseUrlSelector(o).TrimEnd('/')` unconditionally at line 100. OKX and Bitget `baseUrlSelector` already calls `ExchangeUrl.NormalizeHostRoot`, which itself calls `.TrimEnd('/')`. The result is a double-trim — functionally harmless (idempotent), but reveals a layering tension: the shared helper and the registration both apply the same transformation. The `ExchangeServiceRegistration` `.TrimEnd('/')` predates `NormalizeHostRoot` and exists for exchanges (Binance, Bybit) that pass the raw URL directly. It is not a defect; just noted in case a future cleanup removes the redundancy.
- **Fix**: No action required now. When/if all four exchanges use `NormalizeHostRoot` in `baseUrlSelector`, the `.TrimEnd('/')` in `ExchangeServiceRegistration` can be removed.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs:100`

---

### Finding 7: `BuildQueryString` parameter type widening (`IReadOnlyDictionary` vs `Dictionary`)
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Http/ExchangeUrl.cs:19`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: `ExchangeUrl.BuildQueryString` accepts `IReadOnlyDictionary<string, string>?` while all four callers pass `Dictionary<string, string>?`. `Dictionary<TKey,TValue>` implements `IReadOnlyDictionary<TKey,TValue>`, so implicit covariant assignment applies — the compiler accepts all call sites. The widened signature is strictly better: it signals "this method does not need to mutate the collection" and would accept `ReadOnlyDictionary<>`, `ImmutableDictionary<>`, or any future source. Build confirms zero warnings.

---

### Finding 8: `serverTimeMs > 0` guard — correctness and defense-in-depth intact
- **Severity**: LOW (confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:108-112`, `src/CryptoExchanges.Net.Bybit/BybitExchangeClient.cs:650-651`, `src/CryptoExchanges.Net.Okx/OkxExchangeClient.cs:782-783`, `src/CryptoExchanges.Net.Bitget/BitgetExchangeClient.cs:450-451`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The guard is applied identically and consistently across all four exchange clients. The `ExchangeTimeSync.ApplyOffset` throw for `serverTimeMs <= 0` in Core is retained, so the defense-in-depth model is correct: the outer guard prevents the public API from surfacing a throw on degraded but non-fatal network conditions; the Core-level throw ensures any future code path that bypasses the outer guard still fails safely.

---

### Finding 9: No public API changes; no interface additions
- **Severity**: LOW (confirmation)
- **Confidence**: 100
- **File**: multiple
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `IMarketDataService`, `ITradingService`, `IAccountService`, and `IExchangeClient` are all unchanged. `ExchangeUrl` is `internal`. `BitgetValueParsers.ParseOptionalDecimal` removal is internal-only. `NormalizeHostRoot` was previously `public static` on `BitgetClientComposer` — it is now removed from there and added as `internal static` on `ExchangeUrl`. This is a net reduction of public surface.

---

### Finding 10: Package coupling — no aggregation-package regression
- **Severity**: LOW (confirmation)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.DependencyInjection/` (not touched by this diff)
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The DI aggregation package is untouched. All new code lives in `CryptoExchanges.Net.Http` (shared layer, already a transitive dep of every exchange) or in per-exchange assemblies. ADR-001 is not disturbed.

---

### Finding 11: Test coverage for new shared helper and Bitget orderbook guard
- **Severity**: LOW (confirmation)
- **Confidence**: 100
- **File**: `tests/CryptoExchanges.Net.Http.Tests.Unit/ExchangeUrlTests.cs`, `tests/CryptoExchanges.Net.Bitget.Tests.Unit/BitgetMappingAndServiceTests.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `ExchangeUrl` has a dedicated test class covering: null/empty input, escaped ordering, host-only normalization (trailing slash trim), path-segment rejection, and blank-input rejection. The Bitget orderbook short-row guard has a test confirming rows with fewer than 2 elements are skipped. The OKX test `Di_AddOkxExchange_BaseUrlWithPath_FailFast` validates the DI path rejects a bad base URL. All unit tests pass (0 failures, 477 total tests across all unit suites).

---

### Build and test gate
- **Build**: `dotnet build CryptoExchanges.Net.sln` — succeeded, 0 warnings, 0 errors (`TreatWarningsAsErrors=true` enforced)
- **Tests**: All unit tests pass — 477 total across Http, Bitget, OKX, Bybit, Binance, DI, Core suites

---

## Summary

- PASS: Strict layering (Core->Http->Exchange->DI) — `ExchangeUrl` is in Http, not Core; no exchange namespace imported
- PASS: `ExchangeUrl` is exchange-agnostic — no Binance/Bybit/OKX/Bitget type references
- PASS: `internal static` classification correct — both methods are pure fixed helpers, exempt from Invariant 11
- PASS: `InternalsVisibleTo` addition for `Http.Tests.Unit` — follows existing pattern for `ExchangeServiceRegistration`
- PASS: No public API changes to any interface or exchange client public surface
- PASS: Defense-in-depth model intact — outer `> 0` guard + Core throw both in place across all 4 clients
- PASS: No package coupling regression — DI aggregation untouched; new code in Http (already transitive) and per-exchange
- PASS: Test coverage — `ExchangeUrlTests` and `MarketData_GetOrderBook_SkipsShortLevels` are present and passing
- PASS: `IReadOnlyDictionary` widening — compiles correctly, strictly more general
- CONCERN: `NormalizeHostRoot` asymmetry for Bybit/Binance (confidence: 90, non-blocking) — intentionally correct since those exchanges do not sign `AbsolutePath`; a brief explanatory comment in `BybitClientComposer.cs:97` would prevent future misapplication
- CONCERN: Double `.TrimEnd('/')` in DI path when `baseUrlSelector` already calls `NormalizeHostRoot` (confidence: 95, non-blocking) — harmless, can be cleaned up when all exchanges adopt `NormalizeHostRoot` in their selectors
- CONCERN: `ExchangeUrl` as `internal static` vs interface (confidence: 55, non-blocking) — methods are pure fixed helpers matching the explicit exemption in Invariant 11; no action needed unless future exchanges require pluggable encoding

## Final Verdict

APPROVED

No blocking findings. The extraction of `BuildQueryString` and `NormalizeHostRoot` into the shared Http layer is architecturally sound: it eliminates four byte-identical copies, enforces the host-root signing invariant on both the OKX container-free path (previously unguarded — was `TrimEnd('/')` only) and the Bitget path, and sits correctly in the Http layer where it belongs. Layering, public surface, package coupling, and test coverage all check out cleanly. The three non-blocking concerns are pre-existing pattern tensions surfaced for awareness, not defects introduced by this diff.
