# Architect Review — TASK-013
# OkxSymbolFormat + OkxValueParsers + OkxRequestValidation

**Date**: 2026-06-18
**Reviewer**: Architect Reviewer
**Final Verdict**: APPROVED

---

## Checklist

- [x] No `using` or `ProjectReference` in Core pointing to Http, Binance, or DI
- [x] No `using` or `ProjectReference` in Http pointing to Binance or DI
- [x] No previously-internal types made public without justification
- [x] No new properties added to existing public interfaces
- [x] No new exchange client composition in this diff (infrastructure-only task)
- [x] No DTO→model mappings in this diff (no DeltaMapper profile needed)
- [x] No new HTTP operations (no retry concern)
- [x] No new signing path
- [x] No DI registration in this diff
- [x] No clock-skew / `_offsetHolder` concern (no signing handler)
- [x] No global state or static mutable fields introduced
- [x] No shared/aggregation package given a compile-time reference to a new sibling integration

---

## Findings

### Finding: `ParseTimeInForce` has no "market" arm while `ParseOrderType` does
- **Severity**: LOW
- **Confidence**: 75
- **File**: `src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs:91-97`
- **Category**: Architecture (correctness / API contract)
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `ParseOrderType` handles `"market"` and maps it to `OrderType.Market`. `ParseTimeInForce` has no `"market"` arm — it throws `ArgumentOutOfRangeException` for any unrecognized token, which includes `"market"`. Both parsers are keyed off the same `ordType` field. When a market order is returned from OKX and a mapping profile calls both parsers on the same field, `ParseOrderType` will succeed while `ParseTimeInForce` will throw, aborting the mapping. Market orders on OKX have no meaningful TIF (they execute immediately), so the domain model should likely receive `TimeInForce.Ioc` or a sentinel value rather than an exception. The implementation note documents `ParseTimeInForce` for `ordType` but does not address the market case, suggesting it was omitted rather than deliberately left to the caller to guard.
- **Fix**: Add a `"market"` arm to `ParseTimeInForce` returning a sensible sentinel — either `TimeInForce.Ioc` (market orders consume liquidity immediately, making IOC the closest semantic match) or introduce an explicit `TimeInForce.None`/`TimeInForce.Na` if Core supports it. Alternatively, callers in the mapping profile must null-guard the TIF call for market orders and document that contract. Whichever path is chosen, add a corresponding unit test in TASK-015.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:88-94` — Bybit keeps TIF and OrderType on separate wire fields so the gap does not arise; OKX's single-field design requires the extra arm.

---

### Finding: `OkxSymbolFormat.Instance` field accessibility (`public` on `internal` class)
- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs:10`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The static field is declared `public` on an `internal static` class. The effective visibility is `internal` (C# compiler enforces container visibility), so there is no actual leak. It is nonetheless inconsistent with the class's accessibility modifier and could mislead a reader into thinking the field is part of a public surface. The Bybit reference uses the exact same pattern (`BybitSymbolFormat.cs:10`), so this is a pre-existing pattern issue, not a regression introduced by this diff.
- **Fix**: Change `public static readonly` to `internal static readonly` for consistency and clarity. Non-blocking — defer to the next pass over `BybitSymbolFormat` for consistency.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs:10` (same pattern — fix both together).

---

### Finding: No max-window-span guard in `OkxRequestValidation` (documented deviation)
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Okx/Internal/OkxRequestValidation.cs:24-35`
- **Category**: Architecture
- **Verdict**: PASS (deviation is documented and architecturally sound)
- **Issue**: None. The absence of a `MaxHistorySpan` check is explicitly justified in the implementation notes (OKX V5 paginates via `before`/`after` cursors and does not enforce a fixed window cap on these endpoints). The inline XML doc on `ValidateHistoryWindow` states this clearly. The deviation from the Bybit pattern is deliberate and correctly scoped to this exchange's API contract.

---

## Pattern Conformance Summary

| Item | Result |
|---|---|
| `OkxSymbolFormat` mirrors `BybitSymbolFormat` exactly (delimiter, casing, fallback assets) | PASS |
| `OkxValueParsers` mirrors `BybitValueParsers` structure (InvariantCulture, empty/zero handling, throwing vs non-throwing posture) | PASS |
| `OkxRequestValidation` mirrors `BybitRequestValidation` structure with documented deviations (MaxHistoryLimit=100, no span guard) | PASS |
| All three files are `internal static` — no cross-layer visibility leak | PASS |
| No Core modifications — uses existing `SymbolFormat`/`SymbolCasing` types | PASS |
| No `ProjectReference` added to Core or Http | PASS |
| `OkxSymbolFormat` namespace is `CryptoExchanges.Net.Okx` (root, not Internal) — correct per Bybit pattern | PASS |
| `OkxValueParsers` and `OkxRequestValidation` in `CryptoExchanges.Net.Okx.Internal` namespace | PASS |
| ParseTimeInForce missing "market" arm — correctness gap when both parsers called on same field | CONCERN (LOW, non-blocking) |
| `public` field on `internal` class — pre-existing pattern inconsistency cloned from Bybit | CONCERN (LOW, non-blocking) |

---

## Final Verdict: APPROVED

No blocking findings. One low-confidence correctness concern (`ParseTimeInForce` missing the `"market"` arm) is flagged for the implementer's attention before TASK-015 test coverage is written — if tests exercise a market-order round-trip through both parsers, the gap will surface as a test failure. The other concern (field accessibility) is a pre-existing pattern issue not introduced by this diff.
