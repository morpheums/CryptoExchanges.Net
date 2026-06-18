---
id: TASK-020
status: PLANNED
---

# TASK-020: BitgetSymbolFormat + value parsers + request validation

**Milestone**: M-BITGET
**Wave**: 13
**Group**: 13
**Status**: PLANNED
**Depends on**: TASK-017
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bitget (per-exchange wire format + parsing)
**Blast radius**: LOW — new files in Bitget project; uses existing Core SymbolFormat.

## Description
Create `BitgetSymbolFormat.Instance` for Bitget's delimiter-less upper-case spot symbols (e.g. `BTCUSDT`) via the existing Core `SymbolFormat`/`SymbolCasing` (bespoke `ISymbolMapper` retained). Add `Internal/BitgetValueParsers` and `Internal/BitgetRequestValidation` mirroring the Binance internals.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bitget/BitgetSymbolFormat.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetValueParsers.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetRequestValidation.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bitget/BitgetSymbolFormat.cs
- src/CryptoExchanges.Net.Bitget/Internal/BitgetValueParsers.cs
- src/CryptoExchanges.Net.Bitget/Internal/BitgetRequestValidation.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/BinanceSymbolFormat.cs` (delimiter-less upper)
- `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs`, `BinanceRequestValidation.cs`

## Acceptance Criteria
1. `ToWire(new Symbol(Asset.Btc, Asset.Usdt))` returns `"BTCUSDT"` and `FromWire` round-trips.
2. `BitgetValueParsers` use `CultureInfo.InvariantCulture` and reject malformed values deterministically.
3. `BitgetRequestValidation` enforces per-order-type required fields.

## Test Requirements
- Unit tests in TASK-022 cover symbol round-trip, parser invariants, validation rejections.
