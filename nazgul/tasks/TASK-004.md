---
id: TASK-004
status: IMPLEMENTED
---

# TASK-004: BybitSymbolFormat + value parsers + request validation

**Milestone**: M-BYBIT
**Wave**: 2
**Group**: 2
**Status**: PLANNED
**Depends on**: TASK-001
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (per-exchange wire format + parsing; bespoke ISymbolMapper retained)
**Blast radius**: LOW — new files in Bybit project; uses existing Core `SymbolFormat`/`SymbolCasing`.

## Description
Create `BybitSymbolFormat.Instance` configured for Bybit's delimiter-less upper-case spot symbols (e.g. `BTCUSDT`) via the existing Core `SymbolFormat`/`SymbolCasing` value types — bespoke `ISymbolMapper` is retained per mandate (no new mapper). Add `Internal/BybitValueParsers` (string→decimal/enum parse helpers for Bybit's encodings) and `Internal/BybitRequestValidation` (pre-flight guards) mirroring the Binance internals.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs`
- `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs`
- `src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs
- src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs
- src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/BinanceSymbolFormat.cs` (SymbolFormat.Instance, delimiter-less upper)
- `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs`
- `src/CryptoExchanges.Net.Binance/Internal/BinanceRequestValidation.cs`

## Acceptance Criteria
1. `new SymbolMapper(BybitSymbolFormat.Instance).ToWire(new Symbol(Asset.Btc, Asset.Usdt))` returns `"BTCUSDT"` and `FromWire` round-trips.
2. `BybitValueParsers` parse helpers use `CultureInfo.InvariantCulture` and reject malformed input deterministically.
3. `BybitRequestValidation` enforces required fields per order type using `PlaceOrderRequest.Validate()`-style guards.

## Test Requirements
- Unit tests in TASK-008 cover symbol round-trip, parser invariants, and validation rejection cases.

## Implementation Notes

Created three new files in the Bybit project, mirroring the Binance internals' style exactly:

- `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs` — `internal static class` exposing
  `BybitSymbolFormat.Instance`, a Core `SymbolFormat` configured with `Delimiter = ""`,
  `Casing = SymbolCasing.Upper`, and a Bybit-tuned `FallbackQuoteAssets` list. No new
  `ISymbolMapper` was introduced — the bespoke mapper is retained per mandate; this only
  supplies the format value object. Because the config matches Binance's delimiter-less upper
  format, `new SymbolMapper(BybitSymbolFormat.Instance).ToWire(new Symbol(Asset.Btc, Asset.Usdt))`
  yields `"BTCUSDT"`, and `FromWire` round-trips via the warm table / the `USDT` fallback quote.

- `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs` — string→decimal/enum parse
  helpers using `CultureInfo.InvariantCulture`. `ParseDecimal`/`ParseOptionalDecimal` mirror
  Binance semantics (empty→0 / empty-or-zero→null). Enum parsers use Bybit V5 wire encodings:
  side `Buy`/`Sell` (capitalized vs Binance's UPPER), type `Limit`/`Market`, TIF
  `GTC`/`IOC`/`FOK`/`PostOnly`, and order status `New`/`PartiallyFilled`/`Filled`/`Cancelled`
  (British spelling) etc. Malformed side/type/TIF throw `ArgumentOutOfRangeException`
  deterministically; unknown statuses map to `OrderStatus.Unknown` (matching Binance posture).

- `src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs` — pre-flight guards
  mirroring `BinanceRequestValidation`. `ValidateHistoryWindow` enforces `limit` in 1..50 and an
  ordered start/end window of at most 7 days, using Bybit V5 constants. Per-order field guards
  remain in Core `PlaceOrderRequest.Validate()` (the Binance pattern calls that in the trading
  service rather than duplicating order-shape checks here).

### Deviations from the Binance pattern (with justification)
- Enum wire strings differ because Bybit V5 uses mixed-case tokens (`Buy`, `Limit`, `New`,
  `Cancelled`) and a `PostOnly` TIF that Binance does not; these reflect the actual Bybit API.
- History constants are 50/7-days (Bybit V5) vs Binance's 1000/24-hours.
- Bybit spot has no distinct stop/take-profit order *types* on the wire (trigger orders use a
  separate field), so `ParseOrderType` maps only `Limit`/`Market`; the broader domain
  `OrderType` values are still produced by Core validation, not by the response parser.

### Verification
`dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s).**
