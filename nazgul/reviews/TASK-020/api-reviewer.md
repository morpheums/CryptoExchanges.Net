# API Review — TASK-020: BitgetSymbolFormat + BitgetValueParsers + BitgetRequestValidation

**Reviewer**: API Reviewer
**Task**: TASK-020
**Branch**: feat/m4-bitget
**Files reviewed**:
- `src/CryptoExchanges.Net.Bitget/BitgetSymbolFormat.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetValueParsers.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetRequestValidation.cs`

---

## Scope

Three additive-only new files in `src/CryptoExchanges.Net.Bitget/`. No existing interface, model, enum, or method signatures are touched. Breaking-change surface: zero.

---

### Finding 1: Encapsulation — all three types correctly `internal`

- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `BitgetSymbolFormat.cs:7`, `Internal/BitgetValueParsers.cs:8`, `Internal/BitgetRequestValidation.cs:7`
- **Category**: API Design
- **Verdict**: PASS

All three types are declared `internal static`. `public` members inside `internal` classes are inaccessible to NuGet consumers (the C# compiler enforces this), matching the exact pattern in `BybitSymbolFormat`, `BybitValueParsers`, `BybitRequestValidation`, `OkxValueParsers`, `OkxRequestValidation`, and the Binance equivalents. The `InternalsVisibleTo` in `CryptoExchanges.Net.Bitget.csproj:19-21` grants access only to `CryptoExchanges.Net.Bitget.Tests.Unit`, `...Tests.Integration`, and `DynamicProxyGenAssembly2` (NSubstitute). No consumer app project is granted visibility. This is correct.

---

### Finding 2: Naming consistency with siblings

- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: all three files
- **Category**: API Design
- **Verdict**: PASS

Class names (`BitgetSymbolFormat`, `BitgetValueParsers`, `BitgetRequestValidation`) and all method names (`ParseDecimal`, `ParseOptionalDecimal`, `ParseAssetOrNone`, `ParseOrderSide`, `ParseOrderType`, `ParseOrderStatus`, `ParseMs`, `ParseTimeInForce`, `ValidateHistoryWindow`) are letter-for-letter identical to the Bybit and OKX siblings. `MaxHistoryLimit` const name matches. Namespace placement (`CryptoExchanges.Net.Bitget` and `CryptoExchanges.Net.Bitget.Internal`) mirrors the sibling pattern.

---

### Finding 3: `ParseAssetOrNone(string? ticker)` — nullable parameter intentional and consistent

- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `Internal/BitgetValueParsers.cs:39`
- **Category**: API Design
- **Verdict**: PASS

`ParseAssetOrNone` takes `string?` (nullable) while the other parse methods take `string` (non-nullable). This is intentional and consistent with every sibling: `BinanceValueParsers.ParseAssetOrNone(string? ticker)` (line 40), `BybitValueParsers.ParseAssetOrNone(string? ticker)` (line 40), `OkxValueParsers.ParseAssetOrNone(string? ticker)` (line 39) all carry the same nullable annotation. The asymmetry is justified because balance endpoints can omit or null-out asset tickers for long-tail assets.

---

### Finding 4: `BitgetSymbolFormat.Instance` — FallbackQuoteAssets identical to Bybit, not Binance-extended

- **Severity**: LOW
- **Confidence**: 70
- **File**: `BitgetSymbolFormat.cs:14-18`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking, confidence 70/100)

The `FallbackQuoteAssets` list is `["USDT", "USDC", "USDE", "DAI", "USD", "EUR", "BTC", "ETH"]`, verbatim identical to `BybitSymbolFormat`. The task manifest documents this as an intentional copy ("FallbackQuoteAssets copied from Bybit"). Binance's list is broader (`FDUSD`, `TUSD`, `BUSD`, `GBP`, `TRY`, `BNB` extras), which is Binance-specific. Bitget's actual quote universe on V2 spot is dominated by USDT, USDC, BTC, ETH, and USD stablecoins — the current list covers the major ones. Since `FallbackQuoteAssets` is only a cold-cache fallback (the warm table from `UpdateSymbols` is the primary path), omitting a minor quote is a degraded-cold-start experience, not a correctness failure. No fix required before merge; worth tracking as a follow-up once the full symbol table is wired.

---

### Finding 5: `ParseOptionalDecimal` zero-suppression — intentional, documented, consistent

- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `Internal/BitgetValueParsers.cs:26-32`
- **Category**: API Design
- **Verdict**: PASS

The `parsed == 0m ? null : parsed` branch is identical to `OkxValueParsers.ParseOptionalDecimal` and `BybitValueParsers.ParseOptionalDecimal`. The doc comment correctly explains that Bitget uses `"0"` for unset optional fields like `priceAvg`, making zero-suppression semantically appropriate. `ParseDecimal` (non-optional) returns `0m` for empty, which is correct for always-present numeric fields.

---

### Finding 6: `ParseOrderStatus` — `"live"` token shared with OKX, `"init"`/`"new"` Bitget-specific

- **Severity**: N/A (PASS)
- **Confidence**: 90
- **File**: `Internal/BitgetValueParsers.cs:73-80`
- **Category**: API Design
- **Verdict**: PASS

`"init" or "new" or "live" => OrderStatus.New` correctly handles Bitget V2's accepted-but-unfilled states. The task manifest explicitly justifies all three tokens. `"cancelled"` (British spelling) correctly maps to `OrderStatus.Canceled` (American spelling in the domain enum), and unknown tokens fall through to `OrderStatus.Unknown` without throwing — consistent with the Bybit/OKX/Binance posture for order status specifically. The asymmetry between throwing parsers (side, type, TIF) and non-throwing status is consistent across all sibling files.

---

### Finding 7: `ParseTimeInForce` — `"post_only"` mapped to `Gtc`, consistent with OKX/Bybit

- **Severity**: N/A (PASS)
- **Confidence**: 95
- **File**: `Internal/BitgetValueParsers.cs:95-101`
- **Category**: API Design
- **Verdict**: PASS

`"gtc" or "post_only" => TimeInForce.Gtc` mirrors OKX's `"limit" or "post_only" => TimeInForce.Gtc` and Bybit's `"GTC" or "PostOnly" => TimeInForce.Gtc`. Bitget V2 `force` tokens are lower-case (matching OKX's style, not Bybit's PascalCase). The `post_only` mapping to `Gtc` as closest domain equivalent is documented and consistent. The parser throws `ArgumentOutOfRangeException` for unknown TIF values, consistent with Bybit, Binance, and OKX.

---

### Finding 8: `BitgetRequestValidation` — no max-span check, consistent with OKX posture

- **Severity**: N/A (PASS)
- **Confidence**: 90
- **File**: `Internal/BitgetRequestValidation.cs:22-33`
- **Category**: API Design
- **Verdict**: PASS

`ValidateHistoryWindow` enforces `limit ∈ [1, 100]` and ordering of start/end but omits a fixed max-span check. This is correctly documented ("Bitget paginates via `idLessThan` cursors rather than enforcing a fixed maximum window span") and structurally matches `OkxRequestValidation.ValidateHistoryWindow`. Bybit and Binance add max-span enforcement because their APIs specifically reject requests exceeding 7d/24h respectively. The limit of 100 matches the documented Bitget V2 spot cap.

---

### Finding 9: `ParseMs` — matches OKX pattern, non-throwing

- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `Internal/BitgetValueParsers.cs:86-87`
- **Category**: API Design
- **Verdict**: PASS

`long.TryParse(...) ? ms : 0L` exactly matches `OkxValueParsers.ParseMs`. Bybit does not need `ParseMs` (Bybit V5 delivers timestamps as JSON numeric longs directly). Bitget V2 delivers timestamps as epoch-ms strings throughout, consistent with OKX. The non-throwing `0L` fallback is appropriate for timestamp fields where a missing/corrupt value should not abort response mapping.

---

### Finding 10: No breaking changes to any Core interface, model, or enum

- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: diff.patch (entire diff)
- **Category**: Compatibility
- **Verdict**: PASS

The diff adds only new files. No `IMarketDataService`, `ITradingService`, `IAccountService`, `IExchangeClient`, `IExchangeClientFactory`, or `ISymbolMapper` members are added or changed. No records in `Models/Models.cs` are touched. No enum values are added or reordered. Blast radius is confirmed LOW (new files only, within the Bitget package).

---

### Finding 11: NuGet conventions

- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `CryptoExchanges.Net.Bitget.csproj:1-31`
- **Category**: NuGet Conventions
- **Verdict**: PASS

`<PackageId>`, `<Description>`, and `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` (inherited from `Directory.Build.props`) are all present. `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is inherited and confirmed active (XML doc output exists in `obj/Debug/`). `InternalsVisibleTo` is scoped to test assemblies and NSubstitute's dynamic proxy only — no consumer app projects are granted visibility.

---

## Summary

- PASS: `BitgetSymbolFormat` encapsulation — `internal static` class, not accessible to NuGet consumers, consistent with all siblings.
- PASS: `BitgetValueParsers` encapsulation — `internal static` in `Internal` namespace, `InternalsVisibleTo` scoped to test/mock assemblies only.
- PASS: `BitgetRequestValidation` encapsulation — same as above.
- PASS: Naming consistency — all class and method names are letter-for-letter identical to Bybit/OKX siblings.
- PASS: `ParseAssetOrNone(string? ticker)` nullable parameter — intentional, consistent with all three siblings.
- PASS: Method signatures — `ParseDecimal(string)`, `ParseOptionalDecimal(string)`, `ParseMs(string)`, `ParseOrderSide(string)`, `ParseOrderType(string)`, `ParseOrderStatus(string)`, `ParseTimeInForce(string)`, `ValidateHistoryWindow(int, DateTimeOffset?, DateTimeOffset?)` are signature-identical to Bybit/OKX equivalents.
- PASS: Throwing vs non-throwing parser policy — throws for side/type/TIF, non-throwing for status/decimal/timestamp, consistent with all siblings.
- PASS: `MaxHistoryLimit = 100` — matches OKX; Bybit uses 50, Binance uses 1000 (exchange-specific, correct).
- PASS: No max-span enforcement in `ValidateHistoryWindow` — documented and consistent with OKX (Bitget uses cursor pagination).
- PASS: No breaking changes to any Core interface, model, or enum.
- PASS: NuGet project metadata complete; `InternalsVisibleTo` appropriately scoped.
- CONCERN: `FallbackQuoteAssets` list (copied from Bybit) may omit Bitget-specific quote assets as the integration matures (confidence: 70/100, non-blocking — warm-table path is primary; cold-cache degradation only).

## Final Verdict

APPROVED
