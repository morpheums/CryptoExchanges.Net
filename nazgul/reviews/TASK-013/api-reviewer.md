# API Review — TASK-013: OkxSymbolFormat + value parsers + request validation

**Reviewer**: API Reviewer
**Date**: 2026-06-18
**Task**: TASK-013
**Files reviewed**:
- `src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs`
- `src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs`
- `src/CryptoExchanges.Net.Okx/Internal/OkxRequestValidation.cs`

---

## Findings

### Finding: ParseTimeInForce has no arm for "market" ordType
- **Severity**: LOW
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs:91-96`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `ParseTimeInForce("market")` throws `ArgumentOutOfRangeException` at runtime when called for a market order response. OKX market orders return `ordType = "market"`, and the switch has no arm for it, unlike `ParseOrderType` which handles `"market"` correctly. When the service layer is added and maps market order responses, calling `ParseTimeInForce` without guarding for the market case will cause an unhandled exception.
- **Fix**: Either add a `"market" => TimeInForce.Gtc` (or appropriate sentinel) arm, or add an explicit XML comment stating that callers must not invoke `ParseTimeInForce` for market ordType values and must branch on `ParseOrderType` first. Resolving before the service layer lands avoids a runtime bug.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:88-94` — Bybit's `ParseTimeInForce` does not face this issue because Bybit uses a separate TIF field decoupled from ordType.

---

## Summary

- PASS: Visibility — all three types are `internal static class`; no public surface is added to the NuGet package. `public` members inside internal types follow the established InternalsVisibleTo convention.
- PASS: InternalsVisibleTo — OKX csproj grants visibility only to `CryptoExchanges.Net.Okx.Tests.Unit`, `CryptoExchanges.Net.Okx.Tests.Integration`, and `DynamicProxyGenAssembly2` (NSubstitute proxy). Mirrors the Bybit pattern exactly; no consumer app assemblies included.
- PASS: NuGet conventions — `PackageId`, `Description`, `PackageLicenseExpression` (inherited from `Directory.Build.props`), and `GenerateDocumentationFile` (inherited) are all correct.
- PASS: Naming and signature consistency — all method names (`ParseDecimal`, `ParseOptionalDecimal`, `ParseAssetOrNone`, `ParseOrderSide`, `ParseOrderType`, `ParseOrderStatus`, `ParseTimeInForce`, `ValidateHistoryWindow`, `MaxHistoryLimit`) and signatures match the Bybit analogues exactly.
- PASS: `OkxSymbolFormat.Instance` pattern — `internal static class` with `public static readonly SymbolFormat Instance`, identical structure to `BybitSymbolFormat`. Delimiter `"-"`, `SymbolCasing.Upper`, and FallbackQuoteAssets list all match the stated OKX wire format.
- PASS: `ValidateHistoryWindow` deviation from Bybit — no `MaxHistorySpan` guard is intentional and documented in both code XML doc and task manifest. OKX paginates via `before`/`after` cursors rather than enforcing a fixed window span.
- PASS: XML doc coverage — all `public` members on internal types carry XML documentation, consistent with the Bybit pattern and the project's `GenerateDocumentationFile=true` setting.
- PASS: Backwards compatibility — purely additive change (three new files in the OKX assembly). No existing interfaces, models, records, or enums are touched. No breaking change.
- CONCERN: `ParseTimeInForce("market")` throws — latent runtime bug for market order responses, non-blocking at confidence 72.

---

## Final Verdict

APPROVED

VERDICT: APPROVED
CONFIDENCE: 97
