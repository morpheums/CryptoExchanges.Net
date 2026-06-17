---
id: TASK-004
status: PLANNED
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
