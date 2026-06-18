---
id: TASK-013
status: DONE
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

## Implementation Notes

**Base SHA**: c9243437133b98700aa5ffd1cd0f55615fd3549b

### OkxSymbolFormat
- `internal static`, mirroring `BybitSymbolFormat`. Supplies only the Core `SymbolFormat` instance
  (no new `ISymbolMapper` — bespoke mapper retained per mandate).
- `Delimiter = "-"`, `Casing = SymbolCasing.Upper`, producing OKX instrument IDs like `BTC-USDT`.
- `FallbackQuoteAssets` matches Bybit's list (USDT, USDC, USDE, DAI, USD, EUR, BTC, ETH). With a
  non-empty delimiter, `SymbolMapper.FromWire` splits on the delimiter and the fallback list is only a
  cold-cache aid, but it is supplied for parity/consistency with the other exchanges.
- Acceptance: `new SymbolMapper(OkxSymbolFormat.Instance).ToWire(new Symbol(Asset.Btc, Asset.Usdt))`
  → `"BTC-USDT"`; `FromWire("BTC-USDT")` round-trips. (Test coverage lands in TASK-015.)

### OkxValueParsers (OKX V5 wire-token mappings)
Mirrors `BybitValueParsers` structure (ParseDecimal / ParseOptionalDecimal empty+zero handling,
ParseAssetOrNone, enum parsers). All numeric parsing uses `CultureInfo.InvariantCulture`.

- **Order side** (`side`): `buy` → `Buy`, `sell` → `Sell`. OKX uses lower-case tokens. Anything else
  → `ArgumentOutOfRangeException` (deterministic reject).
- **Order type** (`ordType`): OKX folds TIF into `ordType`.
  - `market` → `OrderType.Market`
  - `limit` / `post_only` / `fok` / `ioc` → `OrderType.Limit` (all are limit-priced resting/IOC orders;
    the fill nuance is carried by the TIF parser). The domain `OrderType` has no maker-only or
    explicit-IOC member, so this is the conservative mapping.
  - OKX V5 spot does not expose stop/take-profit as `ordType` values (algo orders use a separate API),
    so those domain members are intentionally not mapped here.
  - Unknown → `ArgumentOutOfRangeException`.
- **Order status** (`state`, American spelling):
  - `live` → `New`, `partially_filled` → `PartiallyFilled`, `filled` → `Filled`,
    `canceled` / `mmp_canceled` → `Canceled`.
  - Unknown → `OrderStatus.Unknown` (mirrors Bybit's non-throwing posture for unrecognized status).
- **Time-in-force** (derived from `ordType`): `limit` / `post_only` → `Gtc` (`post_only` is a maker-only
  GTC order), `ioc` → `Ioc`, `fok` → `Fok`. Unknown → `ArgumentOutOfRangeException`.

### OkxRequestValidation
- `MaxHistoryLimit = 100`. **Source**: OKX V5 documented cap for market/trade list endpoints
  (`/api/v5/trade/orders-history`, `/api/v5/market/trades`, `/api/v5/market/candles`) — `limit` max 100.
  (Bybit's equivalent is 50; OKX's documented value is higher.)
- `ValidateHistoryWindow(limit, startTime, endTime)`: enforces `limit ∈ 1..100` and start ≤ end.
  **Deviation from Bybit**: no fixed max-window-span guard. Bybit caps the window at 7 days; OKX does not
  enforce a fixed maximum span on these endpoints (it paginates via `before`/`after` cursors), so only the
  ordering invariant is checked. Documented inline.
- Per-order field checks remain in Core `PlaceOrderRequest.Validate()` as with Bybit.

### Verification
```
dotnet build CryptoExchanges.Net.sln
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Commits
- 5bd9589a74ebde59b9dae15d074eaf9f67c9345d — feat(M3): TASK-013 OkxSymbolFormat + value parsers + request validation

## Rework (review round 1)
- **Blocking B1** (code-reviewer REJECT@92, corroborated by all 4): `ParseTimeInForce` had no `"market"` arm but `ParseOrderType` accepts `"market"` — both key off OKX `ordType`, so a market-order response would throw when TASK-015's mapping calls both. **Fix**: added `"market" => TimeInForce.Ioc`. Decision (reviewers split Gtc vs Ioc): chose **Ioc** — a market order is non-resting (fills immediately, never rests on the book), and the domain enum is {Gtc,Ioc,Fok}; Gtc ("rest until canceled") is semantically wrong for a market order. Matches what Bybit's separate timeInForce field returns for market orders (IOC). Documented in the parser XML.
- TASK-015 must add a market-order round-trip test so this can't regress (noted for TASK-015).
- Build after fix: 0w/0e.
- **Commit (rework)**: a73e1bb fix(M3): TASK-013 add market arm to OkxValueParsers.ParseTimeInForce

## Review
- **Review Gate**: PASSED (round 2) — architect 88, security 97, api 97 (round 1) + code-reviewer round 1 REJECT@92 → round 2 APPROVE after the market-TIF fix. No blocking items remain.
- **Verified**: BTC-USDT round-trips; InvariantCulture parsing; deterministic rejects; ParseOrderType/ParseTimeInForce now accept the identical ordType token set (asymmetry closed).
- **Owed in TASK-015**: market-order round-trip regression test.
