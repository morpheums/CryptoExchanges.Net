# TASK-013 Review Gate — Consolidated Feedback

**Aggregate verdict:** ⚠ CHANGES_REQUESTED (require_all_approve=true; 1 of 4 reviewers blocking)

| Reviewer | Verdict | Confidence |
|---|---|---|
| architect-reviewer | ✦ APPROVED | 88 |
| code-reviewer | ✗ CHANGES_REQUESTED | 90 |
| security-reviewer | ✦ APPROVED | 97 |
| api-reviewer | ✦ APPROVED | 97 |

Pre-checks: build 0W/0E; tests 241 non-integration pass (OKX tests land in TASK-015).

---

## BLOCKING (must fix — confidence >= 80, severity MEDIUM)

### B1. `ParseTimeInForce` has no `"market"` arm — latent runtime crash
- **File:** `src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs:91-97`
- **Raised by:** code-reviewer (MEDIUM @ 92, blocking). Independently corroborated by
  api-reviewer (LOW @ 72), architect-reviewer (LOW @ 75) — all four converged on this gap.
- **Issue:** `ParseOrderType` and `ParseTimeInForce` both key off the same OKX V5 `ordType`
  wire field. `ParseOrderType` accepts `"market"` → `OrderType.Market`, but
  `ParseTimeInForce` has no `"market"` arm and falls through to
  `throw new ArgumentOutOfRangeException`. The XML doc on `ParseOrderType` (line 58) explicitly
  says "the fill nuance is carried by `ParseTimeInForce`", signalling callers invoke both on the
  same value. A market-order response (`ordType = "market"`) will therefore crash when the
  TASK-015 mapping profile maps it. No callers exist yet, so the build/tests pass today — the
  defect is latent until TASK-014/TASK-015 wire the order-response mapping.
- **Fix (choose one, match the Bybit precedent):**
  1. Add a `"market"` arm to `ParseTimeInForce`. Market orders have no meaningful TIF in OKX V5
     spot — map to the closest domain sentinel. Reviewers split on the target:
     `TimeInForce.Gtc` (code-reviewer — consistent with the resting-order default) vs
     `TimeInForce.Ioc` (architect — market orders fill-or-expire immediately). **Resolve by
     checking how `BybitValueParsers`/the Bybit mapping profile handles market-order TIF and
     mirror it** so the per-exchange internals stay uniform.
  2. OR, if the design intent is that callers must branch on order type before calling
     `ParseTimeInForce`, add an explicit doc/contract comment stating that and ensure the
     mapping profile guards the call.
- **Verification:** TASK-015 must include a market-order round-trip exercising both
  `ParseOrderType("market")` and `ParseTimeInForce("market")` so this gap cannot regress.

---

## NON-BLOCKING (CONCERN — below threshold, auto-approved; address opportunistically)

### C1. `FallbackQuoteAssets` unused with non-empty delimiter
- **File:** `src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs:14-18` (code-reviewer LOW @ 55)
- `SymbolFormat` documents `FallbackQuoteAssets` as ignored when `Delimiter` is non-empty
  (`Delimiter = "-"` here). Manifest explicitly keeps it for parity/consistency. No action required.

### C2. `public static readonly` member on `internal static` class
- **File:** `src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs:10` (architect LOW @ 60)
- Effective visibility is already `internal` (container wins), so no leak — modifier is cosmetically
  misleading. Cloned verbatim from `BybitSymbolFormat.cs:10`. If changed, change both sides in one
  cleanup pass; do not diverge from the Bybit pattern in isolation.

### C3. `ValidateHistoryWindow` throws above `MaxHistoryLimit`
- **File:** `src/CryptoExchanges.Net.Okx/Internal/OkxRequestValidation.cs:29-31` (code LOW @ 45)
- Matches the Bybit precedent (validation throws; the service clamps via `Math.Min` before calling).
  Reminder for TASK-014/TASK-015: the future OKX service must clamp `limit` before calling, as
  `BybitTradingService` does. No change to this file.

### C4. `decimal.Parse` without `NumberStyles` / on malformed input
- **File:** `OkxValueParsers.cs:14-32` (security LOW @ 55)
- `decimal.Parse` may throw `FormatException`/`OverflowException` on malformed exchange data, but
  this is a deterministic failure matching the Binance/Bybit reference pattern. Not exploitable;
  no secret/injection surface. No action required.

---

## Confirmed correct (no action)
- `ToWire(Symbol(Btc, Usdt))` → `"BTC-USDT"` and `FromWire` round-trips via Core `SymbolMapper`
  (dash delimiter + Upper casing). No Core change made or needed.
- All numeric parsing uses `CultureInfo.InvariantCulture`.
- side/type/tif reject malformed input with `ArgumentOutOfRangeException`; unknown order status →
  `OrderStatus.Unknown` (matches Bybit's non-throwing posture).
- OKX V5 enum tokens plausible: side `buy`/`sell` lower; `ordType` market/limit/post_only/fok/ioc
  with TIF folding; `state` live/partially_filled/filled/canceled/mmp_canceled.
- Deviations accepted by architect: `MaxHistoryLimit=100` (OKX documented cap) and no fixed
  window-span guard (OKX paginates via before/after cursors).
- All three types internal; purely additive to the Okx assembly; no public-surface or back-compat
  impact; no security/signing/secret code (signing is TASK-011).
