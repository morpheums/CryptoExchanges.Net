# Security Review — TASK-013
## OKX Symbol Format + Value Parsers + Request Validation

**Reviewer**: Security Reviewer
**Date**: 2026-06-18
**Files Reviewed**:
- `src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs`
- `src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs`
- `src/CryptoExchanges.Net.Okx/Internal/OkxRequestValidation.cs`

---

## Checklist Results

### Credential Safety
- PASS: No `SecretKey`, `ApiKey`, or credential references in any of the three files.
- PASS: No `[JsonInclude]` or serialization attributes present.
- PASS: No `ToString()` override that could expose secrets.
- PASS: No HMAC/signing code in scope (signing lives in TASK-011 files, confirmed absent here).

### Signing Integrity
- PASS: No signed request path in these files. No `MarkSigned()`, no timestamp/signature construction. Not applicable.

### Query String Safety
- PASS: No HTTP/query string construction in these files. Pure parsers and validators only.

### Input Validation
- PASS: `OkxRequestValidation.ValidateHistoryWindow` uses C# pattern `limit is < 1 or > MaxHistoryLimit` — correctly rejects negatives (including `int.MinValue`) and values above 100. No bypass possible via negative or overflow input since `limit` is a typed `int`.
- PASS: `DateTimeOffset?` window check guards `.HasValue` before `.Value` — no null ref.
- PASS: `ParseAssetOrNone` uses `Asset.TryOf` — handles null input, never throws.
- PASS: `ParseOrderStatus` falls through to `OrderStatus.Unknown` for unrecognized values — no unhandled throw on new/unknown exchange states.

### Secret Management
- PASS: No new credential source introduced. Not applicable to these files.

### Rate Limiting
- PASS: No rate-limit gate responsibility in these files. Not applicable.

### JSON Deserialization Safety
- PASS: No `JsonDocument.Parse` or `ReadFromJsonAsync` in these files.

---

## Findings

### Finding: decimal.Parse without TryParse on exchange response data
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs:18,30`
- **Category**: Robustness (not security)
- **Verdict**: PASS (confidence below 80; non-blocking)
- **Issue**: `decimal.Parse()` throws `FormatException`/`OverflowException` on malformed or out-of-range numeric strings from the exchange response. This is unhandled in the parser itself.
- **Fix**: Not required — this is identical to the Binance reference pattern (`BinanceValueParsers.cs:18,31`) and is the project's established "deterministic reject" posture per the task manifest acceptance criteria. If callers ever need fault isolation (e.g., mapping a partial batch response), they can wrap at the call site with `decimal.TryParse`. No action needed at this layer.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs:18,31`

---

## Summary

- PASS: Credential safety — no secrets, no ApiKey/SecretKey, no serialization exposure in any of the three files.
- PASS: Signing integrity — no HMAC/signing code present (correctly scoped to TASK-011).
- PASS: Query string safety — no HTTP or URL construction; pure parsers/validators.
- PASS: Input validation — `ValidateHistoryWindow` correctly bounds limit (1..100), handles negative and overflow inputs via typed `int` pattern match; window ordering check is null-safe.
- PASS: `ParseAssetOrNone` handles `Asset.None` gracefully via `TryOf`.
- PASS: `ParseOrderStatus` uses non-throwing fallback `OrderStatus.Unknown` for unknown exchange states.
- CONCERN: `decimal.Parse` on untrusted response data — throws `FormatException`/`OverflowException` on malformed input, but this matches the Binance reference pattern and is the project's documented "deterministic reject" posture (confidence: 55/100, non-blocking).

---

## Final Verdict

APPROVED

VERDICT: APPROVED
CONFIDENCE: 97

No blocking issues found. All security checklist items pass. One low-severity, low-confidence robustness note on `decimal.Parse` is non-blocking and matches the established codebase pattern.
