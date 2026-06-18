---
id: TASK-020
status: IMPLEMENTED
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

## Implementation Notes
- **Symbol format** (`BitgetSymbolFormat.Instance`): delimiter-less UPPER (`Delimiter=""`, `Casing=SymbolCasing.Upper`), mirroring Bybit (Bitget spot symbols are `BTCUSDT`, NOT OKX's `BTC-USDT`). `FallbackQuoteAssets` copied from Bybit. No new `ISymbolMapper` — the Core `SymbolMapper` consumes this format; `new SymbolMapper(BitgetSymbolFormat.Instance).ToWire(BTC/USDT)=="BTCUSDT"` and `FromWire` round-trips via the fallback quote list.
- **Value parsers** (`BitgetValueParsers`, internal, InvariantCulture) — Bitget V2 spot wire tokens:
  - side (`side`): `buy`→Buy, `sell`→Sell; unknown throws `ArgumentOutOfRangeException`.
  - type (`orderType`): `limit`→Limit, `market`→Market; unknown throws. TIF carried separately on `force`.
  - TIF (`force`): `gtc`/`post_only`→Gtc (post_only is a resting maker-only GTC order — closest domain fit), `ioc`→Ioc, `fok`→Fok; unknown throws.
  - status (`status`): Bitget V2 spot uses British spelling `cancelled`. Mapping: `init`/`new`/`live`→New (freshly accepted/resting), `partially_filled`→PartiallyFilled, `filled`→Filled, `cancelled`→Canceled; **unknown→OrderStatus.Unknown (non-throwing)**, mirroring Bybit/OKX. `init`/`new` included conservatively to cover both Bitget's accepted-but-unfilled tokens.
  - Also `ParseDecimal`/`ParseOptionalDecimal` (empty→0/null, zero→null for optional) and `ParseMs` (string epoch-ms, non-throwing→0), matching OKX/Bybit.
- **Request validation** (`BitgetRequestValidation`, internal): `MaxHistoryLimit = 100` — Bitget V2 spot history endpoints (`/api/v2/spot/trade/history-orders`, `/fills`) cap `limit` at 100 (source: Bitget V2 spot API docs). `ValidateHistoryWindow` enforces `limit∈1..100` and ordered start/end; no fixed max-span (Bitget paginates via `idLessThan` cursor), matching OKX's posture. Per-order field checks stay in Core `PlaceOrderRequest.Validate()`.

## Verification
- `dotnet build CryptoExchanges.Net.sln` → Build succeeded, 0 Warning(s), 0 Error(s). Tests arrive in TASK-022.

## Commits
- 03a81d2 feat(M4): TASK-020 BitgetSymbolFormat + value parsers + request validation
