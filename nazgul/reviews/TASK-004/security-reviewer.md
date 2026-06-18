# Security Review: TASK-004
## BybitSymbolFormat + BybitValueParsers + BybitRequestValidation

**Reviewer**: Security Reviewer
**Date**: 2026-06-17
**Branch**: feat/m2-exchange-expansion
**Commit**: c1007cd

**Files reviewed:**
- `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs`
- `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs`
- `src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs`

---

## Findings

### Finding 1: Wire string echoed in exception messages for ParseOrderSide, ParseOrderType, ParseTimeInForce

- **Severity**: LOW
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:51, 64, 93`
- **Category**: Security (information disclosure in exception messages)
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: All three throwing parsers use the three-argument `ArgumentOutOfRangeException(paramName, actualValue, message)` overload, which embeds the raw wire string `s` as the `actualValue`. This means the Bybit API's wire value is included verbatim in the exception object's `ActualValue` property and in the default `Message` rendering. If a caller or upstream middleware logs unhandled exceptions, the raw wire string is surfaced in the log.

  The risk is bounded: (a) the wire strings are enum-like tokens (`"Buy"`, `"Limit"`, `"GTC"`) — not secrets, account data, or PII; (b) this is an `internal` class; (c) the Binance pattern uses the identical overload (`BinanceValueParsers.cs:51, 68, 98`), so this is a deliberate, established pattern in the codebase. The concern is limited to a novel or unexpectedly long exchange status string being echoed in a log, not credential leakage.

- **Fix**: If log hygiene for exchange-returned values is a concern, switch to the two-argument `ArgumentOutOfRangeException(paramName, message)` overload and include the value only in the message string: `throw new ArgumentOutOfRangeException(nameof(s), $"Unknown order side: '{s}'")`. This removes the value from the structured `ActualValue` slot where automated log serializers may capture it separately. Not blocking given parity with the Binance pattern and the non-sensitive nature of these values.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs:51` — identical pattern confirmed as codebase norm.

---

### Finding 2: `decimal.Parse` throws `FormatException` and `OverflowException` on hostile input — no wrapping

- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:18, 31`
- **Category**: Security (denial-of-service / unexpected-throw on untrusted input)
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `ParseDecimal` and `ParseOptionalDecimal` call `decimal.Parse(value, CultureInfo.InvariantCulture)` without catching `FormatException` or `OverflowException`. A malformed Bybit response field (e.g. `"NaN"`, `"Infinity"`, or a number outside the decimal range) will propagate an unhandled exception out of the mapping layer.

  This is identical to the established Binance pattern (`BinanceValueParsers.cs:18, 31`). The risk is accepted at the codebase level. The concern is architectural (the whole pattern), not Bybit-specific.

- **Fix**: No change required for this task given Binance parity. If hardening is desired in a future task, both Binance and Bybit parsers should adopt `decimal.TryParse` with a typed SDK exception on failure. This would be a cross-cutting change covering both exchange implementations.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs:18` — identical pattern confirmed.

---

### Finding 3: `ParseOrderType` strict-throw posture may cause hard failures on future Bybit API evolution

- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:60-65`
- **Category**: Security (denial-of-service / unexpected-throw on untrusted input)
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `ParseOrderType` maps only `"Limit"` and `"Market"` and throws `ArgumentOutOfRangeException` for anything else. If Bybit V5 introduces additional order type strings in a future API update, all history fetches will fail hard rather than map to a safe sentinel. By contrast, `ParseOrderStatus` uses the safer `Unknown` fallback for unrecognized values. The inconsistency is intentional per the task description and mirrors the Binance pattern exactly.

- **Fix**: No change required for security purposes. A future robustness improvement could add a `_ => OrderType.Unknown` arm (requires adding `OrderType.Unknown` to Core enums) to match `ParseOrderStatus` tolerance. This is a product decision, not a security requirement.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs:60-70` — same strict-throw posture on type.

---

## Checklist Results

### Credential Safety
- No reference to `ApiKey`, `SecretKey`, `BybitOptions`, or any credential type in any of the three files. PASS.
- No `[JsonInclude]` on credential fields (checked `BybitOptions.cs` for context). PASS.
- No `ToString()` override that could serialize secrets. PASS.
- `SecretKey` is not transmitted — it stays inside `BybitSignatureService`. PASS.

### Signing Integrity
- No HTTP pipeline code in any of the three files. Not applicable. PASS.

### Query String Safety
- No URL construction or query string building in any of the three files. PASS.

### Input Validation
- `ValidateHistoryWindow`: `limit` bounds check correct (`< 1 or > 50`). Start/end ordering check present. 7-day span check correct. Matches Binance pattern. PASS.
- `ParseAssetOrNone`: Delegates to `Asset.TryOf` (validates length ≤32 chars, charset A-Z/0-9, null/whitespace). Returns `Asset.None` on failure. No throw on bad input. PASS.
- `BybitSymbolFormat.Instance`: `FallbackQuoteAssets` contains only string literals; no user-supplied input at construction. PASS.

### Culture-Invariance
- `ParseDecimal` and `ParseOptionalDecimal` both use `CultureInfo.InvariantCulture` explicitly. Prevents locale-dependent parsing. PASS.

### Secret Management Expansion
- No new credential sources introduced. PASS.

### Rate Limiting / Error Translation
- Out of scope for TASK-004 (pure value parsing and validation helpers, no HTTP client behavior). PASS.

### JSON Deserialization Safety
- No `JsonDocument.Parse()` or `ReadFromJsonAsync<T>` calls in scope. Not applicable. PASS.

---

## Summary

| Check | Result | Notes |
|---|---|---|
| Credential safety | PASS | No ApiKey/SecretKey reference in any of the three files |
| Signing integrity | PASS | No HTTP pipeline code in scope |
| Query string safety | PASS | No URL construction |
| Culture-invariance | PASS | `CultureInfo.InvariantCulture` used throughout |
| Wire string in exception messages | CONCERN (72) | Non-sensitive enum tokens echoed via `actualValue` param; matches Binance pattern |
| `decimal.Parse` unhandled exceptions | CONCERN (60) | `FormatException`/`OverflowException` not caught; matches Binance pattern |
| `ParseOrderType` strict-throw posture | CONCERN (55) | Consistent with Binance; potential future break if Bybit expands type enum |
| `ParseAssetOrNone` null safety | PASS | Tolerant via `Asset.TryOf`; returns `Asset.None` |
| `ValidateHistoryWindow` bounds | PASS | Correct Bybit V5 constants (50/7d), correct comparison operators |
| `BybitOptions` no serialization of secrets | PASS | No `[JsonInclude]`, no `ToString()` override on secret fields |
| Secret management | PASS | No new credential sources introduced |

---

## Final Verdict

**APPROVED** — Confidence: 91/100

All three files are pure value-parsing and validation helpers with no credential handling, no HTTP pipeline involvement, and no signing code. They faithfully mirror the established Binance pattern. The three CONCERN items are all non-blocking: two are inherited from the Binance pattern (`decimal.Parse` surface, wire string in exception `actualValue` slot) and one is an accepted product trade-off (strict `ParseOrderType`). None meet the blocking threshold of severity HIGH/MEDIUM with confidence >= 80. There are no secrets, no credentials, no query string construction, and no serialization paths in any of the three files.
