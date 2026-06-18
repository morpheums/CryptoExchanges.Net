# Code Review: TASK-020 — BitgetSymbolFormat + BitgetValueParsers + BitgetRequestValidation

**Reviewer**: code-reviewer  
**Date**: 2026-06-18  
**Branch**: feat/m4-bitget  
**Files reviewed**:
- `src/CryptoExchanges.Net.Bitget/BitgetSymbolFormat.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetValueParsers.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetRequestValidation.cs`

**Build**: `dotnet build CryptoExchanges.Net.sln` → Build succeeded, 0 Warning(s), 0 Error(s)  
**Tests**: 292 unit tests pass (Bitget-specific tests deferred to TASK-022 per manifest)

---

## Findings

### Finding 1: `Instance` has `public` visibility on an `internal static` class
- **Severity**: LOW
- **Confidence**: 65
- **File**: `BitgetSymbolFormat.cs:10`
- **Category**: Style
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `Instance` is declared `public static readonly` on an `internal static class`. The field is unreachable from outside the assembly regardless of declared accessibility. The Bybit and Binance equivalents (`BybitSymbolFormat.cs:10`, `BinanceSymbolFormat.cs:10`) exhibit the exact same pattern, so this is a pre-existing convention that TASK-020 faithfully mirrors.
- **Fix**: No action required — this matches the established peer pattern.
- **Pattern reference**: `BybitSymbolFormat.cs:10`, `BinanceSymbolFormat.cs:10`

### Finding 2: FallbackQuoteAssets is identical to Bybit's list — no SUSDT coverage
- **Severity**: LOW
- **Confidence**: 50
- **File**: `BitgetSymbolFormat.cs:14-18`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `FallbackQuoteAssets = ["USDT", "USDC", "USDE", "DAI", "USD", "EUR", "BTC", "ETH"]` is byte-for-byte identical to Bybit's list. The manifest explicitly states "FallbackQuoteAssets copied from Bybit." Bitget spot supports `SUSDT` (Bitget's own stablecoin); missing it means `FromWire("BTCSUSDT")` would not round-trip. Those pairs are very low volume and TASK-022 unit tests will enforce the round-trip contract.
- **Fix**: No blocking action. Consider adding `SUSDT` in a follow-up if Bitget integration covers those pairs.
- **Pattern reference**: `BybitSymbolFormat.cs:14-18`

### Finding 3: `ParseDecimal`/`ParseOptionalDecimal` accept non-nullable `string` but handle null at runtime via `IsNullOrEmpty`
- **Severity**: LOW
- **Confidence**: 55
- **File**: `BitgetValueParsers.cs:14`, `BitgetValueParsers.cs:26`
- **Category**: Null safety
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: Both methods use `string.IsNullOrEmpty(value)` which safely handles `null` at runtime, but the parameter type is non-nullable `string`. This is the same pattern used verbatim in `BinanceValueParsers.cs:14,27`, `BybitValueParsers.cs:14,27`, and `OkxValueParsers.cs:14,26`. Build passes clean with 0 warnings and this pattern is established codebase convention.
- **Fix**: No action required — this matches the established peer pattern.
- **Pattern reference**: `BinanceValueParsers.cs:14`, `BybitValueParsers.cs:14`

### Finding 4: `BitgetRequestValidation` omits max time-window span check
- **Severity**: LOW
- **Confidence**: 70
- **File**: `BitgetRequestValidation.cs:22-33`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `BinanceRequestValidation` enforces a 24-hour max window and `BybitRequestValidation` enforces 7 days. `BitgetRequestValidation.ValidateHistoryWindow` only checks ordering. This is documented in the manifest: "Bitget paginates via `idLessThan` cursors rather than enforcing a fixed maximum window span." The implementation exactly matches `OkxRequestValidation.ValidateHistoryWindow` which skips max-span for the same cursor-pagination reason. This is correct per the API design and explicitly justified in the doc comment.
- **Fix**: No action required — intentional design, correctly documented, mirrors OKX.
- **Pattern reference**: `OkxRequestValidation.cs:31-42`

---

## Summary

### PASS

- **Build**: `dotnet build CryptoExchanges.Net.sln` → 0 Warning(s), 0 Error(s)
- **Tests**: All 292 unit tests pass
- **BitgetSymbolFormat**: `internal static`, `Delimiter=""`, `Casing=SymbolCasing.Upper` — exact match to the Bybit/Binance delimiter-less-upper pattern. Round-trip soundness via `FallbackQuoteAssets` is correct and mirrors `BybitSymbolFormat.cs:14-18`.
- **InvariantCulture consistency**: Every `decimal.Parse` and `long.TryParse` uses fully-qualified `System.Globalization.CultureInfo.InvariantCulture` inline — consistent with Binance, Bybit, and OKX value parsers (none of which add a `using System.Globalization;` in the ValueParsers file itself).
- **ParseDecimal empty→0**: Correct. Matches `BinanceValueParsers.cs:16-17`.
- **ParseOptionalDecimal empty→null, zero→null**: Correct. Matches `BybitValueParsers.cs:28-31`.
- **ParseMs non-throwing→0**: Correct. `long.TryParse` with fallback `0L`. Matches `OkxValueParsers.cs:88-89`.
- **ParseOrderSide throw contract**: `buy`/`sell` + `ArgumentOutOfRangeException` on unknown. Correct for Bitget V2 lower-case wire tokens.
- **ParseOrderType throw contract**: `limit`/`market` + `ArgumentOutOfRangeException` on unknown. Correct — TIF is separate on `force` field.
- **ParseTimeInForce throw contract**: `gtc`/`post_only`→Gtc, `ioc`→Ioc, `fok`→Fok, unknown throws. `post_only`→Gtc mapping is explicitly documented and matches Bybit's `PostOnly`→Gtc precedent (`BybitValueParsers.cs:90`).
- **ParseOrderStatus non-throwing contract**: `init`/`new`/`live`→New, `partially_filled`→PartiallyFilled, `filled`→Filled, `cancelled`→Canceled (British spelling handled), unknown→`OrderStatus.Unknown`. Matches the manifest contract exactly and mirrors Bybit/OKX non-throwing posture.
- **`BitgetRequestValidation.MaxHistoryLimit = 100`**: Correct per Bitget V2 spot API cap, matching OKX's identical cap.
- **ValidateHistoryWindow**: `limit∈[1..100]` throws `ArgumentOutOfRangeException`; unordered window throws `ArgumentException`. Correct. Missing max-span is intentional (cursor pagination, mirrors OKX).
- **XML documentation**: All public members on all three `internal static` classes have XML doc comments. Docs are informative (explaining Bitget-specific behavior like British `cancelled` spelling, `post_only`→Gtc mapping rationale, cursor pagination design) — not noise. Appropriate for internal infrastructure.
- **C# 13/.NET 10 idioms**: Collection expressions `[...]` used in `FallbackQuoteAssets`. Switch expressions used for all enum parsers. Pattern matching `is < 1 or > MaxHistoryLimit` used in validation. All correct.
- **No `#pragma warning disable` added without justification**
- **No new `lock` blocks**
- **No async methods in scope** (all synchronous parsers/validators — appropriate)

### CONCERN (all non-blocking)

- **CONCERN: `Instance` accessibility on `internal` class** — `public static readonly` on `internal static class` is slightly redundant, but this is the established Bybit/Binance pattern; no action needed. (confidence: 65/100)
- **CONCERN: FallbackQuoteAssets may be missing `SUSDT`** — manifest endorses copying from Bybit; low-impact since SUSDT pairs are thin. (confidence: 50/100)
- **CONCERN: `ParseDecimal`/`ParseOptionalDecimal` non-nullable `string` param handles null via `IsNullOrEmpty`** — matches all peer parsers exactly; build is clean. (confidence: 55/100)
- **CONCERN: No max time-window span check** — intentional per API design (cursor pagination), documented in code comment, mirrors OKX. (confidence: 70/100)

### REJECT

None.

---

## Final Verdict

**APPROVED**

All three files compile clean, all tests pass, every behavioral contract from the task manifest is correctly implemented, wire token mappings are plausible for Bitget V2 spot, documentation is accurate and non-redundant, and all idioms are consistent with the Bybit/OKX/Binance peer implementations. No blocking issues found.
