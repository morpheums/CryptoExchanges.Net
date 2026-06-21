---
reviewer: code-reviewer
task: TASK-062
cycle: bugfix
verdict: APPROVE
---
# TASK-062 Code Review — Bugfix Re-Review (commit 32f75f7)

## Summary

Build: `dotnet build` — 0 warnings, 0 errors (`TreatWarningsAsErrors=true` confirmed).
Tests: 87/87 unit tests pass with `--filter 'Category!=Integration'`.

All hard-REJECT lines checked: none triggered.

---

## Findings

### Finding: Channel fix is correct — /market/snapshot replaces /market/ticker
- **Severity**: N/A (this is the fix being reviewed)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs:141`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: Previously `BuildTopic` returned `/market/ticker:{symbol}`; the snapshot channel that carries 24h stats is `/market/snapshot:{symbol}`.
- **Fix**: Applied correctly. `StreamKind.Ticker => $"/market/snapshot:{request.WireSymbol}"`.

---

### Finding: StreamTickerDto fields and [JsonPropertyName] annotations
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTickerDto.cs:1-60`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: Verified all eleven annotations: `symbol`, `lastTradedPrice`, `buy`, `sell`, `high`, `low`, `open`, `vol`, `volValue`, `changePrice`, `changeRate`, `datetime`. All match the KuCoin `/market/snapshot` wire format (JSON numbers, not strings). No stale `/market/ticker` field names (`price`, `bestBid`, `bestBidSize`, `bestAsk`, `bestAskSize`, `sequence`, `time`). DTO class-level `<summary>` correctly references `/market/snapshot` and "JSON numbers (not strings)".

---

### Finding: Mapping profile correctness
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs:96-113`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: Verified all eight member mappings:
  - `LastPrice ← LastTradedPrice` (direct decimal — no parse)
  - `OpenPrice ← Open` (direct decimal)
  - `HighPrice ← High` (direct decimal)
  - `LowPrice ← Low` (direct decimal)
  - `Volume ← Vol` (direct decimal)
  - `QuoteVolume ← VolValue` (direct decimal)
  - `PriceChange ← ChangePrice` (direct decimal — exchange-provided, not derived)
  - `PriceChangePercent ← ChangeRate * 100m` (fraction → percent conversion, correct direction)
  - `Timestamp ← Datetime > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(Datetime) : null` (ms not ns — correct for snapshot channel)
- `ParseNsTimestamp` removed without trace; `ParseMs` overloads remain (still used by REST mappings — correct).
- Comment accurately states the mapping intent, no stale references.

---

### Finding: DeserializeSnapshotData fallback logic
- **Severity**: LOW
- **Confidence**: 65
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs:143-169`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: The fallback case (2) — outer `data` present but no inner `data` — deserializes `outerData` as `T`. In a production `data.sequence` wrapper frame where the inner `data` is missing (e.g. a partial delivery or a vendor bug), the outer object `{"sequence":"33762550100"}` would be deserialized as `StreamTickerDto`. Because all fields are `decimal`/`long` with `init` defaults of `0`/`0L`, this produces a zero-filled DTO that maps to a `Ticker` with all prices zero — silently, with no error logged or thrown. The caller only sees a zero `Ticker` reach the subscription handler. The comment describes this as handling "bare single-level test fixtures", but the actual unit tests use either the full double-nested format or the bare inner payload with no envelope — neither exercises this path. In practice, the path is dead code unless KuCoin sends a malformed frame.
- **Fix**: This is acceptable for now given the edge case nature. If future observability is added to the stream engine, a warning log here would help. Non-blocking.
- **Pattern reference**: `KucoinStreamDecoders.cs:130-131` (DeserializeData's fallthrough comment is analogous).

---

### Finding: StreamKlineDto.Time changed from string to long
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamKlineDto.cs:18-20`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: `time` is a JSON number (unix nanoseconds) per live wire. Changing from `string Time = "0"` to `long Time` is correct. The decoder never reads `dto.Time` — it only reads `dto.Candles` — so this has no behavioral effect on Kline mapping. Comment "not used by the decoder" is accurate. The test fixture now sends `"time":1589968800000000000` (JSON number) matching the wire.

---

### Finding: Test fixtures use real double-nested snapshot format
- **Severity**: N/A
- **Confidence**: 100
- **File**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamDecodeTests.cs:46-104`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: Verified both test cases:
  - `Ticker_CannedInnerPayload_MapsAllFields`: bare inner payload (no envelope), all fields are JSON numbers. Asserts `PriceChangePercent == 0.0014m * 100m` (= 0.14m) — correct. Asserts `Timestamp!.Value.ToUnixTimeMilliseconds() == 1782053405029L` — correct for ms input.
  - `Ticker_FullSnapshotFrame_DoubleNestedData_MapsAllFields`: full double-nested envelope with `data.sequence` + `data.data`. `DeserializeSnapshotData` navigates both levels correctly. Field values match the inner payload.
- Old string-encoded fixtures (`"price":"67000.00"`, `"time":"1718784000000000000"`) fully replaced. No stale string-encoded assertions.

---

### Finding: Protocol tests updated consistently
- **Severity**: N/A
- **Confidence**: 100
- **File**: `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamProtocolTests.cs:139-413`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: `BuildSubscribe_Ticker_ProducesSnapshotTopic`, `BuildUnsubscribe_Ticker`, and `RoutingKeyFor_Ticker_MatchesClassifyRoutingKey` all updated to assert `/market/snapshot:BTC-USDT`. The `Classify_MessageFrame_ReturnsDataWithTopic` test at line 284 still uses `/market/ticker:BTC-USDT` as the topic string — this is intentional: it tests the generic classify path, not the snapshot channel, and the classifier routes any topic verbatim. This is correct.

---

### Finding: CI filter correctness
- **Severity**: N/A
- **Confidence**: 100
- **File**: `.github/workflows/ci.yml:10`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: `--filter 'Category!=Integration'` uses the xUnit Trait filter syntax. All integration test classes in the repo carry `[Trait("Category", "Integration")]`. The KuCoin streaming smoke tests that were timing out all have this trait (`KucoinStreamingSmokeTests.cs:20`, `KucoinRestSmokeTests.cs:16`). The filter correctly excludes them. Note: `BinanceMarketDataIntegrationTests` lacks the `[Trait("Category", "Integration")]` annotation and thus still runs in CI — this is a pre-existing condition unrelated to this diff; those tests passed in the 87-test run above.

---

### Finding: No stale /market/ticker or "nanoseconds" references in ticker-specific code
- **Severity**: N/A
- **Confidence**: 100
- **File**: `StreamTickerDto.cs`, `KucoinMappingProfiles.cs`, `KucoinStreamDecoders.cs`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: Grep of source files (excluding bin/obj) confirms zero occurrences of `/market/ticker` in source (only in generated XML artifacts and a test using it as an arbitrary non-snapshot topic). All "nanoseconds" references are in `StreamKlineDto` (kline frame time — correct), `StreamTradeDto` (trade stream time — correct), `KucoinValueParsers.ParseNsToMs` (still used by Trade decoder — correct), and `KucoinMarketDataService` (REST recent-trades — correct). None are in the ticker DTO or ticker mapping.

---

### Finding: ChangeRate * 100m mapping direction
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs:109`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: `ChangeRate` is a fraction (e.g. `0.0014` ≡ 0.14%). Multiplying by `100m` gives the percentage. The DTO `<summary>` documents this explicitly ("e.g. `0.0014` ≡ 0.14%, multiply by 100"). The mapping applies `s.ChangeRate * 100m`. Test asserts `result.PriceChangePercent.Should().Be(0.0014m * 100m)` (= 0.14m). Correct direction.

---

### Finding: Datetime treated as milliseconds
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs:111-113`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: `DateTimeOffset.FromUnixTimeMilliseconds(s.Datetime)` is correct. The snapshot channel delivers `datetime` as unix milliseconds (e.g. `1782053405029`). The test asserts `result.Timestamp!.Value.ToUnixTimeMilliseconds().Should().Be(1782053405029L)` — round-trips correctly.

---

### Finding: XML docs — no duplication, LEAN compliance
- **Severity**: N/A
- **Confidence**: 100
- **File**: All modified files
- **Category**: Documentation
- **Verdict**: PASS
- **Issue**: `StreamTickerDto` has a concise class-level `<summary>` with factual wire-format info. Per-member `<summary>` are single short lines. `DeserializeSnapshotData` has `<summary>` documenting the double-nested format. The mapping profile comment on lines 96-99 explains the non-obvious `changeRate` fraction convention — this is justified commentary, not restate-the-code noise. No banner separators added.

---

## Final Verdict: APPROVED

All hard-REJECT lines cleared:
- No stale `/market/ticker` reference in source (ticker path)
- `ChangeRate * 100m` mapping is present and correct direction
- `Datetime` treated as milliseconds (`FromUnixTimeMilliseconds`)
- Test fixtures use new JSON-number fields, not old string-encoded fields

Build: 0 warnings, 0 errors.
Tests: 87/87 pass.

One non-blocking CONCERN: the fallback path (2) in `DeserializeSnapshotData` (outer `data` present, no inner `data`) silently produces a zero-filled DTO rather than surfacing an error. This is an edge-case robustness issue, not a regression, and does not block approval.
