# Code Review — FEAT-009 Bybit Consolidated

## Verdict: APPROVED

## Blocking Findings

None.

## Non-Blocking Concerns

`BybitStreamProtocol.cs:123,131,138` — `topicProp.GetString()`, `opProp.GetString()`, and `successProp.GetBoolean()` are called without a prior `ValueKind` guard inside `Classify()`. The project pattern (established at `BybitErrorTranslator.cs:59-65`) always guards with `c.ValueKind == JsonValueKind.Number` / `m.ValueKind == JsonValueKind.String` before invoking typed accessors, because `JsonElement.GetString()` throws `InvalidOperationException` (not `JsonException`) when the element's ValueKind is wrong, and the surrounding `catch (JsonException)` will not catch it. In practice Bybit v5 will always send string-typed `"topic"`, `"op"`, and boolean-typed `"success"` values in legitimate frames; a malformed frame with, say, `"topic":123` would escape the catch and surface as an unhandled `InvalidOperationException` in the engine pump. Because `Classify` is called on every received frame, a single malformed server frame could crash the pump rather than be classified as `FrameKind.Error`. — Confidence: 75/100, non-blocking (only reachable via a malformed or adversarial server frame; not a normal operational path).

`BybitStreamProtocol.cs:203-220` — `MapInterval()` covers 13 codes (1 3 5 15 30 60 120 240 360 720 D W M). The `KlineInterval` enum (`KlineInterval.cs:25`) defines `EightHours` and `ThreeDays` which have no Bybit v5 equivalents and are not listed in the switch. This is correct behaviour (they throw `ArgumentOutOfRangeException`), but a caller iterating all `KlineInterval` values against Bybit would get a surprise exception for those two entries. A short comment on the `MapInterval` summary noting which canonical enum members Bybit v5 does not support would prevent future misuse. — Confidence: 60/100, non-blocking (caller contract is correct; cosmetic only).

## Checklist Results

1. PASS — `Classify()` handles all four frame types: Data (topic+type), Pong (op=="pong"), Ack (op present + success==true), Error (success==false or unrecognised shape). `BybitStreamProtocol.cs:108-151`
2. PASS — Pong branch is checked before Ack branch: the `if (string.Equals(op, "pong", ...))` guard at line 132 returns early, so a pong frame with `"success":true` cannot fall through to the Ack branch. `BybitStreamProtocol.cs:129-141`
3. PASS — `RoutingKeyFor()` calls `BuildTopic(request)` directly (line 77); `Classify()` reads the `"topic"` field (line 123) which Bybit v5 sets to the same venue-native string that `BuildTopic` produces. Single-sourced. `BybitStreamProtocol.cs:71-78, 120-124`
4. PASS — `BuildTopic()` produces: Ticker → `tickers.{sym}`, Trade → `publicTrade.{sym}`, OrderBook without depth → `orderbook.50.{sym}`, OrderBook with depth → `orderbook.{depth}.{sym}`, Kline with interval → `kline.{code}.{sym}`, Kline without interval → `kline.1.{sym}`. `BybitStreamProtocol.cs:186-197`
5. PASS — `MapInterval()` covers all 13 Bybit v5 codes: 1 3 5 15 30 60 120 240 360 720 D W M. `BybitStreamProtocol.cs:203-220`
6. PASS — `BuildBatch()` emits valid JSON with `"req_id"`, `"op"`, `"args"` array; returns null for empty list. `BybitStreamProtocol.cs:157-174`
7. PASS — `MinOutboundInterval` = 100 ms, `HeartbeatDirection` = `ServerPingClientPong`, Interval = 20 s, Timeout = 60 s. `BybitStreamProtocol.cs:36-63`
8. PASS — `JsonOpts` has `PropertyNameCaseInsensitive = false`; this single instance is passed to every `Deserialize<T>` call in both helpers. `BybitStreamDecoders.cs:25-28, 124, 142`
9. PASS — Ticker decoder calls `DeserializeData<StreamTickerDto>` (object unwrap). `BybitStreamDecoders.cs:48`
10. PASS — Trade decoder calls `DeserializeFirstArrayElement<StreamTradeDto>` (array unwrap, first element). `BybitStreamDecoders.cs:57`
11. PASS — `IsBuyerMaker = string.Equals(dto.Side, "Sell", StringComparison.Ordinal)` correctly maps `S=="Sell"` (taker is seller → buyer is maker) to true. `BybitStreamDecoders.cs:67`
12. PASS — OrderBook decoder calls `DeserializeData<StreamDepthDto>` (object unwrap); maps `dto.UpdateId` to `LastUpdateId`. `BybitStreamDecoders.cs:75, 87`
13. PASS — Kline decoder calls `DeserializeFirstArrayElement<StreamKlineDto>` (array unwrap, first element). `BybitStreamDecoders.cs:94`
14. PASS — `DeserializeData<T>` extracts `doc.RootElement.GetProperty("data")` and deserializes the sub-element, NOT the whole frame. `BybitStreamDecoders.cs:117-125`
15. PASS — `DeserializeFirstArrayElement<T>` extracts `"data"`, checks `ValueKind == JsonValueKind.Array`, takes first element via `EnumerateArray().MoveNext()`. `BybitStreamDecoders.cs:128-143`
16. PASS — `MapWireInterval()` is symmetric with `MapInterval()`: "1"→OneMinute, "60"→OneHour, "D"→OneDay, "W"→OneWeek, "M"→OneMonth; all 13 codes covered. `BybitStreamDecoders.cs:147-163`
17. PASS — `StreamTradeDto`: `[JsonPropertyName("s")]` Symbol, `[JsonPropertyName("S")]` Side, `[JsonPropertyName("T")]` TradeTime (long ms), `[JsonPropertyName("v")]` Quantity, `[JsonPropertyName("p")]` Price, `[JsonPropertyName("i")]` TradeId. `StreamTradeDto.cs:12-36`
18. PASS — `StreamDepthDto`: `[JsonPropertyName("s")]` Symbol, `[JsonPropertyName("b")]` Bids, `[JsonPropertyName("a")]` Asks, `[JsonPropertyName("u")]` UpdateId, `[JsonPropertyName("seq")]` Seq. `StreamDepthDto.cs:11-33`
19. PASS — `StreamKlineDto`: `[JsonPropertyName("start")]` OpenTime, `[JsonPropertyName("interval")]` Interval, `[JsonPropertyName("confirm")]` Confirm (bool). `StreamKlineDto.cs:11-44`
20. PASS — `StreamTickerDto` carries `[JsonPropertyName("price24hPcnt")]` → `Price24hPcnt`; the DeltaMapper profile (tested via `Ticker_CannedSnapshotFrame_MapsAllFields`) verifies `PriceChangePercent = 3.08m` from `"0.0308"`. `StreamTickerDto.cs:41-42`
21. PASS — Snapshot vs delta same routing key: `Classify_OrderBookDeltaDataFrame_ReturnsDataWithSameRoutingKey` (line 65). IsBuyerMaker for both sides: `Trade_CannedFrame_BuySide_MapsAllFields` (line 82) and `Trade_SellSide_IsBuyerMakerIsTrue` (line 105). DeserializeData unwrap verified by `Ticker_CannedSnapshotFrame_MapsAllFields` passing a full envelope. `BybitStreamDecodeTests.cs:65,82,105,51`
22. PASS — Empty list returns null: `BuildSubscribeBatch_EmptyList_ReturnsNull` (line 408) and `BuildUnsubscribeBatch_EmptyList_ReturnsNull` (line 414). 100-item list: `BuildSubscribeBatch_OneHundredRequests_EmitsExactlyOneHundredArgs` (line 392). `BybitStreamProtocolTests.cs:392,408,414`
23. PASS — `ResolveConnectionAsync` tested for `ServerPingClientPong` (line 500), 20 s interval (line 510), 60 s timeout (line 519), 100 ms `MinOutboundInterval` (line 528). `BybitStreamProtocolTests.cs:500-535`

## Summary

All 23 checklist items pass. The implementation correctly applies the FEAT-008 lesson (envelope unwrap before DTO deserialization), properly singles-sources the routing key through `BuildTopic()`, handles case-sensitive JSON for `"s"`/`"S"` fields, and maps `IsBuyerMaker` semantics correctly. Build is clean (0 warnings, 0 errors under `TreatWarningsAsErrors=true`) and all 48 unit tests pass. The only concern worth noting is that the three typed `JsonElement` accessor calls inside `Classify()` (`GetString()` on `topicProp` and `opProp`, `GetBoolean()` on `successProp`) lack `ValueKind` guards, inconsistent with the project's own `BybitErrorTranslator` pattern; a malformed server frame with a wrong-typed field would escape the `catch (JsonException)` as an `InvalidOperationException`. This is non-blocking given the operational rarity and the clear engineering mandate from `BybitErrorTranslator.cs:59-65` to guard before typed reads.
