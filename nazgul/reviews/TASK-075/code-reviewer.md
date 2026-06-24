# code-reviewer — TASK-075

## Verdict: CHANGES_REQUESTED

## Findings

| # | Severity | Confidence | File | Finding | Rule reference |
|---|----------|------------|------|---------|----------------|
| 1 | MEDIUM | 90 | `StreamKlineDto.cs:1-41` | Missing `turnover` field. Bybit v5 kline wire sends a `"turnover"` field (quote-asset volume) alongside `"volume"`. Omitting it means callers can never surface quote-volume from a kline bar without re-fetching. The review instruction explicitly asks to flag this and determine intent. There is no comment explaining the omission, so it reads as an oversight rather than a deliberate choice. | LR-009 |
| 2 | LOW | 75 | `StreamTradeDto.cs:1-37` | Missing `BT` (block-trade flag) field. Bybit v5 `publicTrade` frames include a boolean `"BT"` field. Omission silently discards that signal. Lower confidence because block-trade filtering is rarely needed by consumers at this layer; mark as non-blocking concern. | LR-009 |
| 3 | LOW | 70 | `StreamDepthDto.cs:19,25` | `List<List<string>>` is mutable. Binance `StreamDepthDto` uses the same shape, so this is consistent with the codebase pattern and the Roslyn build is clean. However, `IReadOnlyList<IReadOnlyList<string>>` would better express the immutable intent of an `init`-only record. Non-blocking given pattern consistency. | N/A |
| 4 | INFO | 95 | All new files | LR-001 (guard string parameters) is N/A. These are pure data-holder records with no methods and no parameters to guard. | LR-001 |
| 5 | INFO | 95 | All new DTO files | Wire-type correctness (LR-009) is satisfied for all verified fields: price/volume strings are `string`; `T` timestamp is `long`; `u`/`seq` in depth are `long`; `confirm` is `bool`; `start` is `long`. | LR-009 |
| 6 | INFO | 95 | All new files | Build passes clean: 0 warnings, 0 errors under `TreatWarningsAsErrors=true` on .NET 10. All 76 existing unit tests pass. | N/A |
| 7 | LOW | 80 | Tests (absent) | No unit tests were added for any of the four new DTOs or `BybitStreamOptions`. The project rule is "tests are mandatory" (Nazgul Rule 4). No deserialization round-trip tests verify that field names, types, and defaults behave correctly for real Bybit v5 JSON payloads. Blocking per project rules but low-impact in isolation because the types are trivial records; the real risk is that future wire-type regressions will go undetected. | N/A |

## Summary

The five new files are structurally correct: `internal sealed record` with `init`-only properties, `[JsonPropertyName]` attributes, non-null string defaults (`"0"` or `string.Empty`), PascalCase C# names, and XML doc on every property. The build is clean and all existing tests pass. Two issues drive `CHANGES_REQUESTED`. First, `StreamKlineDto` is missing the `turnover` (quote-volume) field that Bybit v5 kline wire always includes — with no comment explaining the deliberate omission, this is most likely an oversight (severity MEDIUM, confidence 90, blocking per LR-009). Second, no unit tests were added for any of the new types, which violates the project's mandatory test rule; at minimum, JSON deserialization round-trip tests should verify field name mappings against real wire payloads. The two non-blocking concerns (missing `BT` field in trades, mutable `List<List<string>>`) can be addressed or documented-as-intentional in a follow-up.
