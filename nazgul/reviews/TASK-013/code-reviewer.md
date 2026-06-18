# Code Review — TASK-013 (OKX symbol format + value parsers + request validation)

VERDICT: CHANGES_REQUESTED
CONFIDENCE: 90

## Summary
The three files mirror the Bybit internals pattern well: dash-delimiter Upper SymbolFormat
produces `BTC-USDT` and round-trips; parsers use `CultureInfo.InvariantCulture`; side/type/tif
reject malformed input with `ArgumentOutOfRangeException`; unknown order status maps to
`OrderStatus.Unknown` (matches Bybit's non-throwing posture). One blocking latent bug found.

## Findings

[SEVERITY MEDIUM] [confidence 92] src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs:91-97 —
`ParseTimeInForce` has no arm for `"market"`, but both `ParseOrderType` and `ParseTimeInForce`
operate on the same OKX V5 `ordType` wire field. The XML doc for `ParseOrderType` (line 58)
explicitly states "the fill nuance is carried by `ParseTimeInForce`", confirming callers will
call both parsers on the same value. A market order (`ordType = "market"`) maps correctly via
`ParseOrderType` → `OrderType.Market` but throws `ArgumentOutOfRangeException` in
`ParseTimeInForce`. Latent runtime crash guaranteed to surface when a mapping profile is added.
Fix: add a `"market"` arm to `ParseTimeInForce` mapping to `TimeInForce.Gtc` (market orders have
no meaningful TIF in OKX V5 spot), OR add an explicit doc/contract requiring callers to branch on
order type before calling `ParseTimeInForce`. Confirm against the Bybit precedent for how market
TIF is handled.

[SEVERITY LOW] [confidence 55] src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs:14-18 —
`FallbackQuoteAssets` is populated but `SymbolFormat` documents it as ignored when `Delimiter` is
non-empty. Since `Delimiter = "-"`, this list is unused at runtime. Manifest acknowledges this is
intentional for parity/consistency. Non-blocking; no fix required.

[SEVERITY LOW] [confidence 45] src/CryptoExchanges.Net.Okx/Internal/OkxRequestValidation.cs:29-31 —
`ValidateHistoryWindow` throws when `limit > 100`. Bybit clamps at the service call site
(`Math.Min(limit, MaxHistoryLimit)`) before calling validation, so the helper itself is consistent
with precedent. Reminder that future OKX services must clamp before calling. Non-blocking.
