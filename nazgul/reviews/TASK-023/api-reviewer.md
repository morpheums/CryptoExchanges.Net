# API Review — TASK-023 (PR #16 self-review remediation)

**Diff reviewed**: `git diff c5027e4..HEAD` (commits `524e017` and `0bee170`)
**Reviewer**: api-reviewer
**Date**: 2026-06-18

---

## Findings

### Finding 1: ExchangeUrl is internal — not leaking into public API
- **Severity**: LOW (verification only)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeUrl.cs:12`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: `ExchangeUrl` is declared `internal static class` in the `CryptoExchanges.Net.Http` namespace. The class is consumed only by the 4 exchange assemblies plus `CryptoExchanges.Net.Http.Tests.Unit`, all of which are granted access via `InternalsVisibleTo` in the `.csproj`. None of these assemblies re-export `ExchangeUrl` through their own public API surfaces. `BinanceExchangeClient`, `BybitExchangeClient`, `OkxExchangeClient`, and `BitgetExchangeClient` only call into `ExchangeUrl` from their own `internal`-scoped HTTP client classes. No public type in any exchange assembly has a member whose signature names `ExchangeUrl`. ADR-001 rule (only `XxxExchangeClient` and `XxxOptions` are public per exchange) is upheld.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj:8-12`

---

### Finding 2: BitgetValueParsers.ParseOptionalDecimal removal — not a breaking change
- **Severity**: LOW (verification only)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Internal/BitgetValueParsers.cs`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: `BitgetValueParsers` is `internal static class` and `ParseOptionalDecimal` was a `public static` method on it, accessible only within the Bitget assembly (the `internal` class modifier caps visibility regardless of the member modifier). The method was dead — no remaining callers exist in `src/` after its removal, confirmed by grep. The accompanying unit test for it in `BitgetSigningTests.cs` has been removed in the same diff. Binance, Bybit, and OKX each have their own `ParseOptionalDecimal` on their own internal parsers; those are untouched. No public contract was changed.
- **Pattern reference**: `internal static class BitgetValueParsers` — `src/CryptoExchanges.Net.Bitget/Internal/BitgetValueParsers.cs:8`

---

### Finding 3: Per-exchange private BuildQueryString removal — not a breaking change
- **Severity**: LOW (verification only)
- **Confidence**: 99
- **File**: `BinanceHttpClient.cs`, `BybitHttpClient.cs`, `OkxHttpClient.cs`, `BitgetHttpClient.cs`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: All four removed `BuildQueryString` methods were `private static`, scoped inside `internal sealed class` HTTP client types. They were never accessible outside their declaring class. The replacement `ExchangeUrl.BuildQueryString` is byte-identical in behavior (same `Uri.EscapeDataString` escaping, same `&` joining, same empty-dict guard). The only signature difference is the parameter type: the new shared method accepts `IReadOnlyDictionary<string, string>?` while the call sites pass `Dictionary<string, string>?`, which is a valid implicit covariant assignment — no cast is needed and it compiles unchanged. No public consumer exists.

---

### Finding 4: BitgetClientComposer.NormalizeHostRoot deletion — not a breaking change
- **Severity**: LOW (verification only)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Internal/BitgetClientComposer.cs`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: `BitgetClientComposer` is `internal static class`. Although `NormalizeHostRoot` on the old version was `public static`, the `internal` class modifier means the method was only reachable within the Bitget assembly (or via InternalsVisibleTo, but no test project is granted that for Bitget internals). No external consumer existed. The existing call sites (`BitgetClientComposer.cs:101` and `ServiceCollectionExtensions.cs:53`) are already updated to `ExchangeUrl.NormalizeHostRoot` in the same diff.
- **Pattern reference**: `internal static class BitgetClientComposer` — `src/CryptoExchanges.Net.Bitget/Internal/BitgetClientComposer.cs:16`

---

### Finding 5: SyncServerTimeAsync behavior change — silent skip on non-positive server time
- **Severity**: MEDIUM
- **Confidence**: 92
- **File**: All four exchange clients — e.g., `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:105-113`
- **Category**: API Design / Compatibility
- **Verdict**: PASS (with note)
- **Issue**: `SyncServerTimeAsync` is not declared on any Core interface (`IExchangeClient` has no such member). It is a concrete method on each exchange client class with no interface contract governing its exception behavior. What changed is purely at the call site: the four exchange clients now guard with `if (serverTimeMs > 0)` before calling `ApplyOffset`, so the method returns cleanly on a zero/negative server time rather than propagating `ArgumentOutOfRangeException` to the caller.

  The `IExchangeTimeSync.ApplyOffset` documented contract (throws `ArgumentOutOfRangeException` on non-positive `serverTimeMs`) is fully preserved — `ExchangeTimeSync.ApplyOffset` still throws at line 18. The guard is applied one level above, at the exchange client, so `ApplyOffset` is never called with invalid input. Defense-in-depth is retained.

  The behavior change is reasonable: a degraded `/time` endpoint returning a zero payload should not crash the caller's startup path. The tradeoff — silent degradation means subsequent signed requests run with the un-synced local clock, potentially producing `-1021` timestamp errors — is the less surprising failure mode in production. The change is acceptable as a deliberate contract refinement for a non-interfaced method on a `0.1.0-preview.1` library.

  Non-blocking note: the XML doc comments on the four `SyncServerTimeAsync` methods do not yet document the degraded-skip behavior. The inline code comment is clear but the doc comment does not reflect it. Not a blocker.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Resilience/ExchangeTimeSync.cs:17-18` (retained throw on ApplyOffset), `src/CryptoExchanges.Net.Core/Resilience/IExchangeTimeSync.cs:12` (interface contract unchanged)

---

### Finding 6: New InternalsVisibleTo for Http.Tests.Unit — justified
- **Severity**: LOW
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj:12`
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: The new `InternalsVisibleTo` grant is for a test-only project (`<IsPackable>false</IsPackable>`, `<IsTestProject>true</IsTestProject>` confirmed). Its sole purpose is to allow `ExchangeUrlTests` to call the `internal ExchangeUrl` members directly. This follows the established pattern. No consumer application project is given InternalsVisibleTo.
- **Pattern reference**: `tests/CryptoExchanges.Net.Http.Tests.Unit/CryptoExchanges.Net.Http.Tests.Unit.csproj:4`

---

### Finding 7: OKX DI BaseUrl previously had no host-root validation — now hardened
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs:52`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: Prior to this diff, `OkxServiceCollectionExtensions` passed `o.BaseUrl` raw, meaning a misconfigured `BaseUrl` with a path segment would produce subtly broken signatures at runtime rather than failing fast at container build. The diff upgrades this to `ExchangeUrl.NormalizeHostRoot(o.BaseUrl)`, consistent with what Bitget already did. This is an additive defensive tightening; no caller passing a valid host-root URL is affected. The new OKX unit test `Di_AddOkxExchange_BaseUrlWithPath_FailFast` confirms the exception propagates correctly.

---

## Summary

- PASS: `ExchangeUrl` is `internal static` — not leaking into any public API surface. InternalsVisibleTo granted only to the 4 exchange assemblies (as implementers) and `Http.Tests.Unit` (test project). No consumer app project listed.
- PASS: `BitgetValueParsers.ParseOptionalDecimal` was internal-only (internal class caps member visibility), had zero remaining callers in src, and its unit test was removed in the same diff. Clean deletion.
- PASS: Per-exchange `BuildQueryString` (private static on internal sealed classes) was never externally visible. Replacement via `ExchangeUrl.BuildQueryString` is behavior-identical; `Dictionary<T,T>` satisfies `IReadOnlyDictionary<T,T>` at all call sites.
- PASS: `BitgetClientComposer.NormalizeHostRoot` (public static on an internal class) had no public consumer. Class is `internal` so the method was already package-internal. Call sites updated in the same diff.
- PASS: `SyncServerTimeAsync` behavior refinement — silent skip on non-positive server time is an acceptable contract refinement for a non-interfaced method on a preview library. `ApplyOffset`'s documented `ArgumentOutOfRangeException` is preserved at the `IExchangeTimeSync`/`ExchangeTimeSync` layer. Minor non-blocking doc gap: the four `SyncServerTimeAsync` XML comments do not document the degraded-skip behavior. (Confidence 92, non-blocking)
- PASS: New `InternalsVisibleTo` for `Http.Tests.Unit` is the correct pattern for a test-only project accessing internals.
- PASS: OKX DI BaseUrl guard is an additive hardening; no valid caller is affected.

---

## Final Verdict

**APPROVED** — All four specific questions resolve cleanly. `ExchangeUrl` is internal and unexposed. The removed members (`ParseOptionalDecimal`, per-exchange `BuildQueryString`, `BitgetClientComposer.NormalizeHostRoot`) were all internal/private with no public consumers. The `SyncServerTimeAsync` behavior change is a deliberate, acceptable contract refinement for a non-interfaced method on a preview library; the defensive throw inside `ExchangeTimeSync.ApplyOffset` is preserved. No Core interface was modified. No public model, enum, or DI signature was changed.
