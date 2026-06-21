---
reviewer: api-reviewer
task: TASK-062
cycle: bugfix
verdict: APPROVE
---
# TASK-062 API Review — Bugfix Cycle (commit 32f75f7)

## Verdict: APPROVE

---

## Scope

This review covers the re-submission of TASK-062 after the real-bug fix:
the KuCoin ticker stream was subscribed to `/market/ticker:{sym}` (wrong — minimal frame, no symbol, no 24h stats)
instead of `/market/snapshot:{sym}` (correct — full 24h stats frame with all fields).
The diff touches `KucoinStreamProtocol.cs`, `KucoinStreamDecoders.cs`, `StreamTickerDto.cs`,
`KucoinMappingProfiles.cs`, `KucoinStreamDecodeTests.cs`, `KucoinStreamProtocolTests.cs`,
`StreamKlineDto.cs`, and `.github/workflows/ci.yml`.

---

## Finding 1: Ticker channel fix — root cause correctly addressed

- **Severity**: HIGH (was the bug; now the fix)
- **Confidence**: 99
- **Blocking**: false (PASS — fix is correct)
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs:141`
- **Category**: API Design / Wire Contract

`BuildTopic` now returns `/market/snapshot:{request.WireSymbol}` for `StreamKind.Ticker`.
`RoutingKeyFor` and `Classify` both delegate to `BuildTopic` — single source of truth.
A consumer subscribing to `StreamKind.Ticker` for "BTC-USDT" receives routing key `/market/snapshot:BTC-USDT`.
Incoming frames with `topic: /market/snapshot:BTC-USDT` are classified to that same key.
Subscribe key and classify key are identical — routing is correct.

The `KucoinStreamProtocolTests` test `BuildSubscribe_Ticker_ProducesSnapshotTopic` and
`Unsubscribe_Ticker_ProducesSnapshotTopic` and `RoutingKey_MatchesTicker_Snapshot_ClassifiedKey`
all confirm the end-to-end routing contract. **PASS.**

---

## Finding 2: `Ticker.Symbol` — populated from snapshot payload

- **Severity**: HIGH
- **Confidence**: 99
- **Blocking**: false (PASS — correctly populated)
- **File**: `src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs:101`
- **Category**: API Design

`StreamTickerDto.Symbol` (mapped from `[JsonPropertyName("symbol")]`) is present in the snapshot
channel payload. The profile maps it via `symbolMapper.FromWire(s.Symbol)`.
The unit test `Ticker_CannedInnerPayload_MapsAllFields` asserts `result.Symbol.Should().Be(BtcUsdt)`.
Consumers will never receive an empty `Symbol`. **PASS.**

---

## Finding 3: `PriceChangePercent` — fraction correctly converted to percent

- **Severity**: HIGH
- **Confidence**: 99
- **Blocking**: false (PASS — correctly multiplied)
- **File**: `src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs:109`
- **Category**: API Design / Wire Contract

The snapshot channel delivers `changeRate` as a fraction (e.g., `0.0014` ≡ 0.14%).
The mapping expression is `s.ChangeRate * 100m`, producing `0.14` as `PriceChangePercent`.
The DTO comment explicitly documents this: "Multiply by 100 to obtain a percentage."
The unit test asserts `result.PriceChangePercent.Should().Be(0.0014m * 100m)`. **PASS.**

---

## Finding 4: `Timestamp` — milliseconds, not nanoseconds

- **Severity**: HIGH
- **Confidence**: 99
- **Blocking**: false (PASS — correctly converted)
- **File**: `src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs:111-113`
- **Category**: API Design / Wire Contract

The snapshot channel `datetime` field is unix milliseconds (JSON number, `long`).
The mapping uses `DateTimeOffset.FromUnixTimeMilliseconds(s.Datetime)` directly — no division required.
The previous implementation was dividing a nanosecond string by 1,000,000 to get milliseconds.
The new implementation is correct for the snapshot channel.
Unit test asserts `result.Timestamp!.Value.ToUnixTimeMilliseconds().Should().Be(1782053405029L)`. **PASS.**

---

## Finding 5: `BidPrice`/`AskPrice` gap assessment — NOT a gap

- **Severity**: LOW
- **Confidence**: 99
- **Blocking**: false (PASS — no gap)
- **File**: `src/CryptoExchanges.Net.Core/Models/Ticker.cs`
- **Category**: API Design

`StreamTickerDto` carries `Buy` (bid) and `Sell` (ask) from the snapshot payload,
but these are NOT mapped to `Ticker`. This is correct: the `Core.Models.Ticker` record
(defined at `src/CryptoExchanges.Net.Core/Models/Ticker.cs`) has no `BidPrice` or `AskPrice`
members. The record's full positional surface is:
`Symbol`, `LastPrice`, `OpenPrice`, `HighPrice`, `LowPrice`, `Volume`, `QuoteVolume`,
`PriceChange`, `PriceChangePercent`, `Timestamp`.
There is nothing to map `Buy`/`Sell` to. `StreamTickerDto.Buy` and `StreamTickerDto.Sell`
are correctly documented for completeness but properly left unmapped. **PASS.**

---

## Finding 6: Double-nested snapshot payload deserialization

- **Severity**: HIGH
- **Confidence**: 99
- **Blocking**: false (PASS — correctly handled)
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs:143-169`
- **Category**: API Design / Wire Contract

The `/market/snapshot` channel wraps the actual ticker data as `data.data` (double-nested):
```
{"type":"message","topic":"/market/snapshot:BTC-USDT","data":{"sequence":"...","data":{...actual ticker...}}}
```
The new `DeserializeSnapshotData<T>` method navigates `root → data → data` to reach the inner payload.
Three fallback levels are handled:
1. Full push frame with `data.data` (production path).
2. Single-level `data` present but no inner `data` (intermediate test fixtures).
3. Bare payload with no envelope (unit-test path).

The `catch (JsonException)` prevents malformed outer wrappers from propagating.
Both test cases (`Ticker_CannedInnerPayload_MapsAllFields` and
`Ticker_FullSnapshotFrame_DoubleNestedData_MapsAllFields`) exercise paths 3 and 1 respectively. **PASS.**

---

## Finding 7: `StreamKlineDto.Time` type change — internal, no consumer impact

- **Severity**: LOW
- **Confidence**: 99
- **Blocking**: false (PASS — internal DTO)
- **File**: `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamKlineDto.cs:22-23`
- **Category**: API Design

`StreamKlineDto.Time` changed from `string` to `long`. This DTO is `internal sealed record` — not
visible to consumers. The Kline decoder (`KucoinStreamDecoders.cs:88-108`) reads only `Candles`
and never reads `Time`. The type change reflects the live wire format (JSON number, not string)
and prevents silent deserialization to default `"0"` when a number arrives on the wire. **PASS.**

---

## Finding 8: `RestBaseUrl` wiring — unaffected by this fix

- **Severity**: MEDIUM
- **Confidence**: 99
- **Blocking**: false (PASS — unchanged and correct)
- **File**: `src/CryptoExchanges.Net.Kucoin/StreamServiceCollectionExtensions.cs:37-52`
- **Category**: API Design

The cycle-2 fix wiring of `RestBaseUrl` into the bullet-public `HttpClient` via
`options.RestBaseUrl` → `Uri.TryCreate` → `httpClient.BaseAddress = baseUri` is untouched
by this bugfix commit. The `LR-001` guard (`ArgumentException.ThrowIfNullOrWhiteSpace`) is
still in place. No regression introduced. **PASS.**

---

## Finding 9: CI filter — correct behavior

- **Severity**: LOW
- **Confidence**: 97
- **Blocking**: false (PASS)
- **File**: `.github/workflows/ci.yml:10`
- **Category**: NuGet Conventions

`--filter 'Category!=Integration'` prevents live-exchange integration tests
(which require real KuCoin credentials) from blocking CI on runners without secrets.
This is the standard pattern for this project (confirmed by `nazgul/config.json` `test_command`). **PASS.**

---

## Finding 10: Public API surface — no regressions

- **Severity**: HIGH
- **Confidence**: 99
- **Blocking**: false (PASS)
- **File**: Various
- **Category**: Compatibility

No changes to:
- `IStreamClient` (public interface — unchanged)
- `AddKucoinStreams` signature (`Action<KucoinStreamOptions>? configure = null` — unchanged)
- `KucoinStreamOptions` public surface (`RestBaseUrl` — unchanged)
- `ExchangeId.Kucoin` enum value (unchanged)
- Any `Core` model, interface, or enum

No breaking changes to any externally visible type. **PASS.**

---

## Minor Non-Blocking Concern

### CONCERN: Second ticker test does not assert `QuoteVolume` or `PriceChangePercent`

- **Severity**: LOW
- **Confidence**: 70
- **Blocking**: false
- **File**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamDecodeTests.cs:97-103`
- **Category**: API Design

`Ticker_FullSnapshotFrame_DoubleNestedData_MapsAllFields` asserts `Symbol`, `LastPrice`,
`OpenPrice`, `HighPrice`, `LowPrice`, `Volume`, `PriceChange`, `Timestamp` — but omits
`QuoteVolume` and `PriceChangePercent`. These fields are fully asserted in the first test
(`Ticker_CannedInnerPayload_MapsAllFields`), so functional correctness is not at risk.
The concern is that the second test (the full-envelope path) gives slightly less coverage.
Non-blocking — the mapping logic is identical for both paths; only deserialization differs.

---

## Summary

- PASS: Ticker channel fix (`/market/snapshot` replaces `/market/ticker`) — root cause correctly addressed, routing round-trip verified
- PASS: `Ticker.Symbol` populated from snapshot payload `symbol` field
- PASS: `PriceChangePercent` = `changeRate * 100m` — fraction correctly converted to percent
- PASS: `Timestamp` = `DateTimeOffset.FromUnixTimeMilliseconds(datetime)` — milliseconds correctly consumed
- PASS: `BidPrice`/`AskPrice` not a gap — `Core.Models.Ticker` has no such members
- PASS: Double-nested `data.data` deserialization with correct three-level fallback
- PASS: `StreamKlineDto.Time` type change — internal DTO, decoder ignores the field
- PASS: `RestBaseUrl` wiring from cycle 2 — unaffected, no regression
- PASS: CI integration filter — correct for no-credential CI runners
- PASS: No public API surface regressions of any kind
- CONCERN: Second ticker test omits `QuoteVolume`/`PriceChangePercent` assertions (confidence: 70, non-blocking)
