---
id: TASK-013
status: PLANNED
---

# TASK-013: OkxSymbolFormat + value parsers + request validation

**Milestone**: M-OKX
**Wave**: 8
**Group**: 8
**Status**: PLANNED
**Depends on**: TASK-010
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#okx (OKX wire format uses `-` delimiter, e.g. BTC-USDT)
**Blast radius**: LOW — new files in Okx project; uses existing Core SymbolFormat.

## Description
Create `OkxSymbolFormat.Instance` configured for OKX's hyphen-delimited upper-case instrument IDs (e.g. `BTC-USDT`) via the existing Core `SymbolFormat`/`SymbolCasing` (bespoke `ISymbolMapper` retained). Add `Internal/OkxValueParsers` and `Internal/OkxRequestValidation` mirroring the Binance internals.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs`
- `src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs`
- `src/CryptoExchanges.Net.Okx/Internal/OkxRequestValidation.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Okx/OkxSymbolFormat.cs
- src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs
- src/CryptoExchanges.Net.Okx/Internal/OkxRequestValidation.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/BinanceSymbolFormat.cs` (SymbolFormat.Instance)
- `src/CryptoExchanges.Net.Core/Models/SymbolFormat.cs` (delimiter + casing config)
- `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs`, `BinanceRequestValidation.cs`

## Acceptance Criteria
1. `ToWire(new Symbol(Asset.Btc, Asset.Usdt))` returns `"BTC-USDT"` and `FromWire("BTC-USDT")` round-trips.
2. `OkxValueParsers` use `CultureInfo.InvariantCulture` and reject malformed values deterministically.
3. `OkxRequestValidation` enforces per-order-type required fields.

## Test Requirements
- Unit tests in TASK-015 cover hyphen-delimited symbol round-trip, parser invariants, validation rejections.
