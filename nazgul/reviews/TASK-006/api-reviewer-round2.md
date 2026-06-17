# API Review Round 2: TASK-006 — LSP fix for GetOrderHistoryAsync / GetTradeHistoryAsync

**Reviewer**: api-reviewer
**Fix commit**: 48fb17b
**Date**: 2026-06-17
**Scope**: Verify only the round-1 blocking REJECT was correctly addressed. Non-blocking concerns from round 1 are deferred per gate disposition and are not re-raised.

---

## Verification Checklist

### 1. Clamp applied to both methods and passed to both ValidateHistoryWindow and the query parameter

**BybitTradingService.GetOrderHistoryAsync** (`src/CryptoExchanges.Net.Bybit/Services/BybitTradingService.cs:207-214`):

```csharp
var effectiveLimit = Math.Min(limit, BybitRequestValidation.MaxHistoryLimit);
BybitRequestValidation.ValidateHistoryWindow(effectiveLimit, startTime, endTime);
// ...
["limit"] = effectiveLimit.ToString()
```

PASS — `effectiveLimit` is passed to `ValidateHistoryWindow` (not the raw `limit`) and is also the value set in the `["limit"]` query parameter. The raw `limit` default of 500 clamps to 50 before validation, so the default-parameter call path succeeds.

**BybitAccountService.GetTradeHistoryAsync** (`src/CryptoExchanges.Net.Bybit/Services/BybitAccountService.cs:116-124`):

```csharp
var effectiveLimit = Math.Min(limit, BybitRequestValidation.MaxHistoryLimit);
BybitRequestValidation.ValidateHistoryWindow(effectiveLimit, startTime, endTime);
// ...
["limit"] = effectiveLimit.ToString()
```

PASS — identical clamping pattern applied correctly in the account service.

**BybitRequestValidation.ValidateHistoryWindow** (`src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs:25-27`):

```csharp
if (limit is < 1 or > MaxHistoryLimit)
    throw new ArgumentOutOfRangeException(...)
```

`MaxHistoryLimit = 50`. With `effectiveLimit = Math.Min(500, 50) = 50`, the guard `limit is < 1 or > 50` is false. No exception thrown. PASS.

The only remaining way to trigger the guard is `limit < 1` (explicit caller error), which is the correct behaviour — a caller explicitly passing 0 or a negative limit should see an exception.

### 2. Build: 0 warnings / 0 errors

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:04.17
```

PASS.

---

## Summary

- PASS: Clamping applied in `BybitTradingService.GetOrderHistoryAsync` — `effectiveLimit` passed to both `ValidateHistoryWindow` and `["limit"]` query parameter.
- PASS: Clamping applied in `BybitAccountService.GetTradeHistoryAsync` — identical pattern, same correctness.
- PASS: Default call path (`limit=500`) now resolves to `effectiveLimit=50`, passes validation, and sends `limit=50` to Bybit. No `ArgumentOutOfRangeException`.
- PASS: Build clean — 0 warnings, 0 errors.

---

## Final Verdict

**APPROVED** (confidence: 98)

The one blocking issue from round 1 is fully resolved. Both methods clamp via `Math.Min(limit, BybitRequestValidation.MaxHistoryLimit)` before passing to validation and to the wire parameter. The LSP violation is eliminated. Build is clean.
