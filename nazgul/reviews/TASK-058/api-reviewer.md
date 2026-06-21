---
reviewer: api-reviewer
task: TASK-058
verdict: APPROVE
---
# API Review — TASK-058

## Verdict: APPROVE

## Summary
TASK-058 delivers the KuCoin wire DTO layer, DeltaMapper profiles, value parsers, and symbol mapper. All required DTO→model mappings are present and correct, ISymbolMapper is fully implemented, and reserved wrapper naming is respected. Two non-blocking findings are noted below.

## Findings

### Finding: `FromWire` wraps `FormatException` into `ExchangeApiException`, diverging from `ISymbolMapper` contract docs
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Kucoin/Internal/KucoinSymbolMapper.cs:27-39`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `ISymbolMapper.FromWire` documents `<exception cref="FormatException">The wire string cannot be resolved.</exception>`. `KucoinSymbolMapper.FromWire` catches the inner `FormatException` from `SymbolMapper` and re-throws as `ExchangeApiException`. A caller who follows the interface contract docs and catches `FormatException` would not catch this re-wrapped exception. All other exchanges use the core `SymbolMapper` directly, which does throw `FormatException`.
- **Fix**: Either (a) align with the interface doc by letting `FormatException` propagate, or (b) update the `ISymbolMapper.FromWire` XML doc to declare `ExchangeException` as the canonical throw type and have all exchange mappers follow suit. Option (b) is the cleaner long-term fix but is a documentation-only change to a Core interface.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/SymbolMapper.cs:108` — base mapper throws `FormatException`; `src/CryptoExchanges.Net.Core/Interfaces/ISymbolMapper.cs:17` — interface doc declares `FormatException`.

### Finding: Two `IsSupported` test methods assert the opposite of their declared behavior
- **Severity**: LOW
- **Confidence**: 95
- **File**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinSymbolAndMappingTests.cs:139-158`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence < 80 for blocking threshold, but factually incorrect)
- **Issue**: `IsSupported_UnregisteredSymbol_ReturnsFalse` is named to assert that an unregistered symbol returns false, but the body asserts `mapper.IsSupported(BtcUsdt).Should().BeTrue()` — BtcUsdt is the registered symbol, not an unregistered one. Similarly, `IsSupported_DefaultSymbol_ReturnsFalse` claims to test `default(Symbol)` returning false but actually calls `mapper.IsSupported(BtcUsdt)` and expects true. Both tests are vacuously passing positive assertions that don't cover the stated negative case. The actual behavior of `IsSupported` when given an unregistered or default symbol goes untested.
- **Fix**: `IsSupported_UnregisteredSymbol_ReturnsFalse` should build a mapper with only `BtcUsdt` registered, then call `mapper.IsSupported(new Symbol(Asset.Of("SOL"), Asset.Of("USDT")))` (a symbol with non-standard tickers that won't cold-parse via the fallback quote list) and assert false. Alternatively, document clearly that the cold-path fallback means any parseable delimited symbol is "supported". `IsSupported_DefaultSymbol_ReturnsFalse` should call `mapper.IsSupported(default)` and assert false or document that default is handled gracefully.
- **Pattern reference**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinSymbolAndMappingTests.cs:132-136` — correct pattern for positive assertion.

## Checklist
- [x] ISymbolMapper: ToWire → BTC-USDT dash format
- [x] ISymbolMapper: FromWire → Symbol(base, quote)
- [x] ISymbolMapper: IsSupported reflects registered symbols
- [x] ISymbolMapper: throws ExchangeException for unsupported (re-wrapped as ExchangeApiException — see CONCERN above)
- [x] Round-trip: FromWire(ToWire(s)) == s
- [x] DeltaMapper: core DTO→model mappings present (TickerDto→Ticker, OrderDto→Order, FillDto→Trade, BalanceDto→AssetBalance, SymbolInfoDto→SymbolInfo)
- [x] Core.Models consistency (cross-exchange parity)
- [x] Enum coverage (all KuCoin side/type/status values)
- [x] Reserved wrappers only (ResponseDto<T>, ListDto<T>)
- [x] No typed list-wrappers
