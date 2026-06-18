# Security Review — TASK-020: BitgetSymbolFormat + Value Parsers + Request Validation

**Reviewer**: Security Reviewer
**Task**: TASK-020
**Branch**: feat/m4-bitget
**Date**: 2026-06-18

**Reviewed files:**
- `src/CryptoExchanges.Net.Bitget/BitgetSymbolFormat.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetValueParsers.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetRequestValidation.cs`

---

## Findings

### Finding 1: ParseDecimal / ParseOptionalDecimal throw on malformed exchange-controlled data — no try/catch at call site

- **Severity**: LOW
- **Confidence**: 55
- **File**: `Internal/BitgetValueParsers.cs:18`, `Internal/BitgetValueParsers.cs:30`
- **Category**: Security (DoS / availability)
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `decimal.Parse(value, InvariantCulture)` will throw `FormatException` for non-numeric strings (e.g., `"N/A"`, `"--"`, or any exchange-side anomaly) and `OverflowException` for values outside `decimal` range. Neither exception is caught inside the parser, and there are no call-site wrappers in scope yet (TASK-020 adds no callers). This matches the Binance pattern exactly (`BinanceValueParsers.cs:18,31`), which also throws, but Binance has a caller-level `catch (FormatException)` guard in `TryMapTicker` (`BinanceMarketDataService.cs:344`) to prevent batch projection abort. Whether Bitget callers (arriving in later tasks) will follow the same protective wrapper pattern is not enforced by this file alone. The risk is a malformed-but-validly-structured Bitget response field could propagate an unhandled `FormatException` to the SDK consumer, degrading a full fetch rather than just the bad record.
- **Fix**: Either add a `try { ... } catch (FormatException) { return 0m; }` fallback inside `ParseDecimal` (making it fully graceful like `ParseMs`), or — more consistently with the Binance pattern — ensure every future call site that iterates a collection wraps the projection in a `TryMap` guard. Document which posture is chosen in the XML doc. Since callers do not yet exist, this is non-blocking for TASK-020 but should be addressed before TASK-020's parsers are wired into any batch-mapping profile.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Services/BinanceMarketDataService.cs:337-349` (TryMapTicker catch pattern)

---

### Finding 2: ParseMs accepts negative epoch-ms values via NumberStyles.Integer

- **Severity**: LOW
- **Confidence**: 35
- **File**: `Internal/BitgetValueParsers.cs:87`
- **Category**: Security (data integrity / edge case)
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `NumberStyles.Integer` permits a leading minus sign, so a Bitget response containing `"-1"` or `"-9999"` would parse successfully to a negative `long` and be returned as-is instead of falling back to `0L`. A negative epoch-ms is semantically invalid and would produce a `DateTimeOffset` in the distant past (year ~1969) if converted downstream. The probability of Bitget emitting a negative timestamp is near zero and this is not exploitable by the SDK caller (exchange-controlled only), but it is a subtle semantic gap.
- **Fix**: Add a `<= 0` check: `long.TryParse(..., out var ms) && ms > 0 ? ms : 0L`. This makes the fallback behavior consistent with the "return 0 for unset/malformed" contract stated in the XML doc.
- **Pattern reference**: Consistent with `ParseMs` docstring: "returning 0 for null/empty/malformed input" — negative values are logically malformed.

---

## Checklist Results

### Credential safety — PASS
No `ApiKey`, `SecretKey`, or `Passphrase` fields are referenced, stored, logged, or transmitted in any of the three files. These are purely format/parse/validate utilities.

### Signing integrity — PASS
No signing handler code, no HTTP pipeline, no request construction. These files have zero contact with the HMAC/credential flow. `BitgetSignatureService` (TASK-018, concurrent) is entirely separate.

### Query string safety — PASS
No URL construction or query string building in any of the three files.

### Input validation correctness — PASS
`ValidateHistoryWindow` limit check (`limit is < 1 or > MaxHistoryLimit`) correctly handles all `int` edge cases:
- `int.MinValue` (-2,147,483,648): satisfies `< 1`, throws correctly.
- `int.MaxValue` (2,147,483,647): satisfies `> 100`, throws correctly.
- No arithmetic is performed on `limit`, so no overflow is possible.

`DateTimeOffset` comparison (`startTime.Value > endTime.Value`) is semantically correct: `DateTimeOffset` comparison is always normalized to UTC offset, so local-timezone vs. UTC inputs compare correctly. The "equal" case (`startTime == endTime`) is intentionally allowed (zero-width window, valid per Bitget cursor-based pagination).

The `MaxHistoryLimit = 100` constant correctly reflects the Bitget V2 spot cap documented for `/api/v2/spot/trade/fills` and `/api/v2/spot/trade/history-orders`.

### InvariantCulture usage — PASS
All three decimal parsers (`ParseDecimal`, `ParseOptionalDecimal`) and `ParseMs` explicitly pass `CultureInfo.InvariantCulture`. No locale-dependent parsing paths exist.

### ParseOrderStatus graceful degradation — PASS
Unknown status strings map to `OrderStatus.Unknown` rather than throwing, matching the Binance and Bybit/OKX posture. British spelling `"cancelled"` is correctly mapped to `OrderStatus.Canceled`. The inclusion of `"init"`, `"new"`, and `"live"` as aliases for `OrderStatus.New` is consistent with the implementation notes and conservative coverage of Bitget V2 state machine variants.

### ParseOrderSide / ParseOrderType / ParseTimeInForce throw posture — PASS
These three parsers throw `ArgumentOutOfRangeException` on unknown values, identical to `BinanceValueParsers` for the same fields. This is the correct posture for structurally-typed enumerated fields where an unknown value would indicate either an API schema change or a corrupted response. The raw wire value is included in the exception message, but these values are exchange-controlled enum tokens (not user credentials or sensitive data), so this is not an information-disclosure concern.

### Symbol format — PASS
`BitgetSymbolFormat.Instance` correctly mirrors the Bybit/Binance delimiter-less upper-case pattern (`Delimiter=""`, `Casing=SymbolCasing.Upper`). The `FallbackQuoteAssets` list covers the standard stablecoin and major-asset quote denominations. No `Asset.None` handling concern: `SymbolMapper.FromWire` calls `Asset.TryOf` for the actual parsing, and the format's `FallbackQuoteAssets` is defensively copied at `SymbolMapper` construction time (confirmed in `SymbolMapper.cs:39`).

### Secret management — PASS
No new credentials source. `BitgetOptions` exists in the project but is not modified by this diff. No `ToString()` override needed on any of these three utility files (they contain no instance state and hold no secrets).

### No logging or serialization of sensitive values — PASS
Confirmed: zero references to `ApiKey`, `SecretKey`, `Passphrase`, `log`, `Log`, `JsonSerializer`, `JsonInclude`, or `ToString` in any of the three reviewed files.

---

## Summary

- PASS: Credential safety — no credential fields touched, referenced, or transmitted in any file.
- PASS: Signing integrity — zero contact with HMAC or request-signing pipeline.
- PASS: Input validation (ValidateHistoryWindow) — limit range check handles all int edge cases including MinValue/MaxValue; DateTimeOffset comparison is UTC-normalized and correct; equal boundary correctly allowed.
- PASS: InvariantCulture — all numeric parsers use explicit InvariantCulture.
- PASS: ParseOrderStatus graceful degradation — unknown → OrderStatus.Unknown, matching established pattern.
- PASS: ParseOrderSide/Type/TimeInForce throw posture — matches BinanceValueParsers, appropriate for enum fields.
- PASS: SymbolFormat — correct delimiter-less upper-case, defensive FallbackQuoteAssets copy at SymbolMapper level.
- PASS: No secret logging or serialization exposure.
- CONCERN: ParseDecimal/ParseOptionalDecimal throw on malformed data — no in-parser try/catch; future batch call sites must follow Binance's TryMapTicker guard pattern (confidence: 55/100, non-blocking).
- CONCERN: ParseMs accepts negative epoch-ms via NumberStyles.Integer — semantically invalid but not exploitable; fix with `ms > 0` guard (confidence: 35/100, non-blocking).

---

## Final Verdict

**APPROVED**

No finding meets the blocking threshold (confidence >= 80 AND severity HIGH/MEDIUM). Both concerns are low-severity and low-confidence, representing design-consistency gaps to address in later tasks (callers, mapping profiles) rather than exploitable defects in these files. The three new files correctly implement their intended roles as pure format/parse/validate utilities, faithfully mirror the established Binance and Bybit patterns, and have zero contact with credential, signing, or HTTP pipeline code.
