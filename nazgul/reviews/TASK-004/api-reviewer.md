# API Review — TASK-004
**Reviewer**: API Reviewer
**Task**: BybitSymbolFormat + value parsers + request validation
**Files reviewed**:
- `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs`
- `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs`
- `src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs`

**Pattern references (Binance equivalents)**:
- `src/CryptoExchanges.Net.Binance/BinanceSymbolFormat.cs`
- `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs`
- `src/CryptoExchanges.Net.Binance/Internal/BinanceRequestValidation.cs`

---

### Finding 1: Visibility posture is correct — all three types are `internal static`
- **Severity**: N/A (confirmation)
- **Confidence**: 100
- **Files**:
  - `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs:7` — `internal static class BybitSymbolFormat`
  - `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:8` — `internal static class BybitValueParsers`
  - `src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs:7` — `internal static class BybitRequestValidation`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. All three types have `internal` visibility and will not appear in the public NuGet API surface. The `public static readonly` / `public static` / `public const` members on these types are internal-assembly-public only, consistent with the Binance equivalents at `BinanceSymbolFormat.cs:10`, `BinanceValueParsers.cs:14`, `BinanceRequestValidation.cs:10`.

---

### Finding 2: Namespace placement is consistent with Binance pattern
- **Severity**: N/A (confirmation)
- **Confidence**: 100
- **Files**:
  - `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs:4` — `namespace CryptoExchanges.Net.Bybit`
  - `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:1` — `namespace CryptoExchanges.Net.Bybit.Internal`
  - `src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs:1` — `namespace CryptoExchanges.Net.Bybit.Internal`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. `BybitSymbolFormat` sits in the root assembly namespace (matching `BinanceSymbolFormat.cs:4`). Both internal helpers sit in the `.Internal` sub-namespace (matching `BinanceValueParsers.cs:1` and `BinanceRequestValidation.cs:1`). Physical file layout matches namespace hierarchy.

---

### Finding 3: Member naming and signature parity with Binance counterparts
- **Severity**: N/A (confirmation)
- **Confidence**: 100
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. Every expected member is present and signature-identical to the Binance counterpart.

| Method | Bybit file:line | Binance file:line |
|--------|-----------------|-------------------|
| `ParseDecimal(string)` | `BybitValueParsers.cs:14` | `BinanceValueParsers.cs:14` |
| `ParseOptionalDecimal(string)` | `BybitValueParsers.cs:27` | `BinanceValueParsers.cs:27` |
| `ParseAssetOrNone(string?)` | `BybitValueParsers.cs:40` | `BinanceValueParsers.cs:40` |
| `ParseOrderSide(string)` | `BybitValueParsers.cs:47` | `BinanceValueParsers.cs:47` |
| `ParseOrderType(string)` | `BybitValueParsers.cs:60` | `BinanceValueParsers.cs:60` |
| `ParseOrderStatus(string)` | `BybitValueParsers.cs:71` | `BinanceValueParsers.cs:76` |
| `ParseTimeInForce(string)` | `BybitValueParsers.cs:88` | `BinanceValueParsers.cs:93` |
| `ValidateHistoryWindow(int, DateTimeOffset?, DateTimeOffset?)` | `BybitRequestValidation.cs:20` | `BinanceRequestValidation.cs:20` |
| `MaxHistoryLimit` (const int) | `BybitRequestValidation.cs:10` | `BinanceRequestValidation.cs:10` |
| `SymbolFormat.Instance` | `BybitSymbolFormat.cs:10` | `BinanceSymbolFormat.cs:10` |

---

### Finding 4: XML doc coverage matches Binance posture exactly
- **Severity**: N/A (confirmation)
- **Confidence**: 100
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. Every `public` member on all three `internal` types carries a `<summary>` block. Throwing members include `<exception>` tags. The `ParseOptionalDecimal` doc correctly describes Bybit's zero-for-unset convention (`triggerPrice`/`avgPrice`) rather than copying Binance's `stopPrice`/`icebergQty` wording — a meaningful doc deviation that accurately describes the Bybit wire behavior.

---

### Finding 5: Wire encoding deviations from Binance are correctly documented and justified
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:47-94`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: Bybit V5 uses mixed-case side/type strings (`"Buy"`, `"Sell"`, `"Limit"`, `"Market"`) vs Binance's ALL_CAPS (`"BUY"`, `"SELL"`, `"LIMIT"`, `"MARKET"`). These are correct for the Bybit V5 REST API. The implementation notes in `TASK-004.md` (lines 62-66) explicitly justify each difference. `ParseOrderStatus` maps Bybit's `"Triggered"` to `OrderStatus.PendingNew` (`BybitValueParsers.cs:78`) — semantically correct since a triggered conditional order has been submitted to the matching engine but not yet filled. `PendingNew` exists in Core (`Enums.cs:49`) and is used by Binance for `PENDING_NEW`.

---

### Finding 6: `PostOnly` TIF silently collapsed to `Gtc`
- **Severity**: MEDIUM
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:89-90`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 72, below threshold of 80)
- **Issue**: `"PostOnly"` maps to `TimeInForce.Gtc` with the XML doc note "closest domain equivalent for a resting maker-only order." `PostOnly` and `GTC` are semantically distinct: `PostOnly` rejects if it would immediately match (maker-only enforcement), while `GTC` rests indefinitely without that constraint. Collapsing them loses information callers could act on. Root cause is a gap in the Core `TimeInForce` enum, not a defect in this implementation.
- **Fix**: Track as follow-up: add `TimeInForce.PostOnly` to Core enum when a subsequent exchange also supports it, then update both Bybit and future exchange parsers. Current mapping is the pragmatic floor given the Core model.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Enums/Enums.cs:54-63` (TimeInForce enum — no PostOnly value exists)

---

### Finding 7: `ValidateHistoryWindow` error message hard-codes "7 days" separately from the constant
- **Severity**: LOW
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs:35`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The error message `"The startTime/endTime window must not exceed 7 days."` hard-codes `"7 days"` as a string literal, while `MaxHistorySpan` is defined as `TimeSpan.FromDays(7)` at line 12. If the limit changes, two edits are required. This mirrors the identical pattern in `BinanceRequestValidation.cs:35` so it is at least consistent.
- **Fix**: Minor: use `$"The startTime/endTime window must not exceed {MaxHistorySpan.TotalDays:0} days."` to keep the string derived from the constant. Not blocking.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceRequestValidation.cs:35` (same pattern — consistent across both)

---

### Finding 8: No public API surface impact — no breaking changes
- **Severity**: N/A
- **Confidence**: 100
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. No interface in `src/CryptoExchanges.Net.Core/Interfaces/` was modified. No model records or enums were changed. No public method signatures on any public type were altered. All three new types are `internal` and contribute zero symbols to the NuGet public API surface.

---

### Finding 9: NuGet project conventions are fully satisfied
- **Severity**: N/A
- **Confidence**: 100
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: None. `PackageId`, `Description`, `AssemblyName`, `RootNamespace` set in `CryptoExchanges.Net.Bybit.csproj:3-6`. `PackageLicenseExpression Apache-2.0`, `Authors`, `Version`, `GenerateDocumentationFile` inherited from `Directory.Build.props:8-18`. `InternalsVisibleTo` scoped to test assembly (`CryptoExchanges.Net.Bybit.Tests.Integration`) and DI package (`CryptoExchanges.Net.DependencyInjection`) only — no consumer application projects granted visibility. Identical justification to Binance project (`CryptoExchanges.Net.Binance.csproj:17-22`).

---

## Summary

- PASS: Visibility — all three types are `internal static`; zero impact on public NuGet API surface.
- PASS: Namespace placement — root namespace for SymbolFormat, `.Internal` sub-namespace for helpers; matches Binance layout exactly.
- PASS: Member naming/signatures — all ten named members present with identical signatures to Binance counterparts.
- PASS: XML doc coverage — every public member documented; throwing members include `<exception>` tags; Bybit-specific wire context described accurately.
- PASS: Wire encoding deviations — mixed-case Bybit V5 tokens correctly differ from Binance ALL_CAPS; documented and justified.
- PASS: NuGet conventions — PackageId, Description, License (inherited), GenerateDocumentationFile (inherited), InternalsVisibleTo scoped correctly.
- PASS: No breaking changes to Core interfaces, models, or enums.
- CONCERN: `PostOnly` collapsed to `Gtc` (confidence: 72/100, non-blocking) — semantic gap acknowledged; root cause is missing `TimeInForce.PostOnly` in Core. Track as follow-up.
- CONCERN: Error message string in `ValidateHistoryWindow` hard-codes `"7 days"` separately from `MaxHistorySpan` (confidence: 85/100, non-blocking) — mirrors existing Binance pattern; cosmetic.

## Final Verdict

APPROVED
