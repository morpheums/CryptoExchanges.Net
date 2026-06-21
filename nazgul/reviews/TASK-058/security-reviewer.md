---
reviewer: security-reviewer
task: TASK-058
verdict: APPROVE
---
# Security Review — TASK-058

## Verdict: APPROVE

## Summary
TASK-058 introduces the KuCoin data layer (wire DTOs, value parsers, symbol mapper, DeltaMapper profiles) and is entirely additive — no credential handling, no signing, no HTTP request construction. All security checks pass. One low-severity non-blocking concern is noted around `ParseDecimal`/`ParseOptionalDecimal` propagating `FormatException` on malformed (non-numeric, non-empty) exchange data, matching the established Binance pattern in this codebase.

## Findings

### ParseDecimal propagates FormatException on malformed wire values — CONCERN
- **File**: src/CryptoExchanges.Net.Kucoin/Internal/KucoinValueParsers.cs
- **Line**: 18, 29
- **Severity**: LOW
- **Confidence**: 65%
- **Rule reference**: N/A (no explicit codebase rule; matches Binance established pattern)
- **Description**: `ParseDecimal(string value)` and `ParseOptionalDecimal(string value)` guard for `null`/empty via `string.IsNullOrEmpty` but call `decimal.Parse(...)` unconditionally for any non-empty, non-null value. A malformed wire string from the exchange (e.g. `"N/A"`, `"--"`) will throw `FormatException` rather than returning a safe default. This is identical to `BinanceValueParsers.cs:18,29` and is therefore the established project pattern. The concern is that a misbehaving or temporarily degraded KuCoin endpoint could crash a mapping call on unexpected but non-empty string content.
- **Fix**: No change required if the project intentionally propagates `FormatException` upward (as Binance does). If greater exchange-data resilience is desired in a future task, replace with `decimal.TryParse(..., out var result) ? result : 0m`.

## Checklist
- [x] No secrets/credentials in source or tests — no ApiKey, SecretKey, passphrase, or credential literals appear anywhere in the diff; test data uses only symbolic names like "ord-123", "trade-1", "BTC-USDT".
- [x] No unsafe deserialization — all DTOs use `System.Text.Json` with `[JsonPropertyName]` on strongly-typed `string`/`long`/`bool`/`List<T>` properties; no `JsonDocument`, `JsonElement`, `dynamic`, or reflection-based deserialization.
- [x] Input validation in parsers (null/empty handled) — `ParseDecimal`, `ParseOptionalDecimal`, `ParseMs`, `ParseNsToMs` all guard null/empty via `string.IsNullOrEmpty` / `long.TryParse`; `ParseAssetOrNone` accepts `string?` and delegates to `Asset.TryOf`; enum parsers (`ParseOrderSide`, `ParseOrderType`, `ParseTimeInForce`) throw `ArgumentOutOfRangeException` on unknown tokens, which is expected documented behavior.
- [x] No sensitive data in test fixtures — all fixture JSON and DTO literals use synthetic exchange-format data (BTC-USDT, "ord-123", "trade-1", unix timestamps); no real credentials, PII, or production API responses.
- [x] No injection risk in symbol mapper — `KucoinSymbolMapper.ToWire` and `FromWire` produce/consume formatted strings only (no SQL, shell, or OS command construction); `ExchangeApiException` message includes the wire symbol string, which is a trading pair identifier, not a credential.
- [x] Appropriate exception messages (no sensitive state leakage) — `ExchangeApiException` in `KucoinSymbolMapper.FromWire` includes the wire symbol (e.g. `"BTC-USDT"`); `ArgumentOutOfRangeException` in enum parsers includes the unrecognized token value (e.g. `"unknown"`). Neither can contain credential material as these values originate from exchange response fields.
- [x] Financial values kept as strings until parsed — all `BalanceDto`, `OrderDto`, `FillDto`, `TickerDto`, `TradeDto`, `CandlestickDto`, `OrderBookDto` fields that carry prices, sizes, and amounts are declared `string` with default `"0"`; conversion to `decimal` only occurs inside `KucoinValueParsers` methods called from `KucoinResponseProfile` mapping expressions.
- [x] No logging of sensitive fields — zero `Console.Write`, `ILogger`, or `Log.` calls in any of the 16 new files.
