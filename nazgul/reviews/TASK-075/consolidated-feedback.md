# Consolidated Review Feedback: TASK-075

## Summary
- **Verdict**: CHANGES_REQUESTED
- **Total findings**: 10 raw (6 unique after deduplication)
- **Blocking**: 1 finding requiring fix
- **Non-blocking**: 5 concerns for awareness
- **Reviewers**: 4/4 submitted
- **Missing reviewers**: none
- **Granularity**: task

---

## Blocking Issues (MUST FIX)

### 1. Missing `turnover` field in `StreamKlineDto` (Correctness)
- **Severity**: MEDIUM | **Confidence**: 90/100
- **Flagged by**: code-reviewer, api-reviewer (Finding 2 — cross-confirmed, merged)
- **File(s)**: `src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamKlineDto.cs:1-41`
- **Issue**: Bybit v5 kline wire frames always include a `"turnover"` field (quote-asset volume, i.e. the equivalent of Binance's `"q"` / `QuoteVolume`). The field is absent from `StreamKlineDto` with no explanatory comment. Because there is no comment documenting the omission as intentional, this reads as an oversight rather than a deliberate narrowing. Any downstream decoder (e.g. TASK-078's `BybitStreamDecoders`) that needs to populate `Candlestick.QuoteVolume` will have no source field to map from, requiring a breaking DTO change at that point.
- **Fix**: Add the following property to `StreamKlineDto`, after the `Volume` property and before `Interval`:
  ```csharp
  /// <summary>Quote-asset volume (turnover) for the kline bar. String-encoded decimal.</summary>
  [JsonPropertyName("turnover")]
  public string Turnover { get; init; } = "0";
  ```
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Dtos/Streaming/StreamKlineBarDto.cs:37-39` — Binance uses `[JsonPropertyName("q")] public string QuoteVolume { get; init; } = "0";` as the equivalent quote-volume field. The same `string` type with `"0"` default applies here.
- **Fix classification**: AUTO-FIX — mechanical field addition with no ambiguity; follows the established pattern exactly.

---

## Non-Blocking Concerns (AWARENESS ONLY)

### 1. No unit tests for new DTO types (Project rule)
- **Severity**: LOW | **Confidence**: 80/100
- **Flagged by**: code-reviewer (Finding 7)
- **File(s)**: Tests (absent) — `tests/CryptoExchanges.Net.Bybit.Tests.Unit/Streaming/` (directory does not yet exist)
- **Concern**: Nazgul Rule 4 states "tests are mandatory." No JSON deserialization round-trip tests exist for any of the four new wire DTOs (`StreamTickerDto`, `StreamTradeDto`, `StreamDepthDto`, `StreamKlineDto`) or for `BybitStreamOptions`. The types are trivial `init`-only records so the immediate risk is low, but wire-type regressions (e.g. a future field rename in the Bybit API) will go undetected without these tests.
- **Recommendation**: Add a `BybitStreamDecodeTests.cs` (or `BybitStreamDtoTests.cs`) mirroring `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDecodeTests.cs`. At minimum, one `[Fact]` per DTO that feeds a canned Bybit v5 JSON payload through `JsonSerializer.Deserialize<T>()` and asserts each mapped property value. These tests can be added in this task (covering just the DTOs) ahead of the full decoder tests that TASK-078 will introduce.
- **Fix classification**: ASK — the test scope decision (DTO-only tests here vs. deferred to TASK-078's full decoder suite) requires implementer judgment. Adding tests here is the safer option.

### 2. Missing `BT` (block-trade flag) field in `StreamTradeDto` (Wire completeness)
- **Severity**: LOW | **Confidence**: 75/100
- **Flagged by**: code-reviewer (Finding 2)
- **File(s)**: `src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamTradeDto.cs:1-37`
- **Concern**: Bybit v5 `publicTrade` frames include a boolean `"BT"` field indicating whether the trade was a block trade. The field is omitted without explanation. Block-trade filtering is rarely needed by consumers at this layer, so this is not a blocking gap, but the omission silently discards a signal that some consumers may want.
- **Confidence is below threshold (75 < 80) — classified as non-blocking concern.**
- **Suggestion**: Either add `[JsonPropertyName("BT")] public bool IsBlockTrade { get; init; }` with an XML doc comment, or add a comment in `StreamTradeDto.cs` explicitly noting the omission is intentional (e.g. `// "BT" block-trade flag intentionally omitted — not surfaced by Core.Models.Trade`).
- **Fix classification**: ASK — omission may be deliberate given `Core.Models.Trade` has no block-trade field; requires implementer decision.

### 3. `List<List<string>>` mutability in `StreamDepthDto` (Style / defensive design)
- **Severity**: LOW | **Confidence**: 70/100
- **Flagged by**: code-reviewer (Finding 3)
- **File(s)**: `src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamDepthDto.cs:19,25`
- **Concern**: `List<List<string>>` on `init`-only record properties is mutable at the element level despite the record being `internal sealed`. `IReadOnlyList<IReadOnlyList<string>>` would more accurately express immutable-after-deserialisation intent. However, the Binance `StreamDepthDto` uses the same `List<List<string>>` shape, so diverging would create an inconsistency within the project's streaming DTO layer.
- **Confidence is below threshold (70 < 80) — classified as non-blocking concern.**
- **Suggestion**: No action required to unblock. If consistency with Binance is the priority, leave as-is. If you prefer stronger immutability guarantees, change the property type to `IReadOnlyList<IReadOnlyList<string>>` (System.Text.Json deserialises to `List<List<string>>` at runtime; no behavioural difference for callers).
- **Fix classification**: ASK — requires a consistency decision across Binance/KuCoin/Bybit DTO shapes.

### 4. `StreamBaseUrl` has no URI scheme validation (Security — low-confidence)
- **Severity**: LOW | **Confidence**: 55/100
- **Flagged by**: security-reviewer (Finding 1)
- **File(s)**: `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamOptions.cs:12`
- **Concern**: `StreamBaseUrl` is a mutable `string` with no validation that enforces the `wss://` scheme. A misconfigured caller supplying `ws://` would silently downgrade to a plaintext connection. The default is correctly `wss://stream.bybit.com/v5/public/spot`. This is the same pattern as `BinanceStreamOptions` — validation belongs at connection time (in `BybitStreamProtocol.ResolveConnectionAsync`), not at the property setter.
- **Confidence is below threshold (55 < 80) — classified as non-blocking concern.**
- **Suggestion**: No property-level change needed. TASK-078's `BybitStreamProtocol.ResolveConnectionAsync` should validate the URI scheme matches `wss` (or `wss`/`ws` depending on environment) before opening the connection, consistent with how `BinanceStreamProtocol` consumes its options.
- **Fix classification**: ASK — enforcement point is in TASK-078, not this task.

### 5. `StreamBaseUrl` default value is spot-only; linear/inverse endpoints will need override (API surface)
- **Severity**: INFO | **Confidence**: 90/100
- **Flagged by**: api-reviewer (Finding 1)
- **File(s)**: `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamOptions.cs:12`
- **Concern**: The default `wss://stream.bybit.com/v5/public/spot` serves spot topics only. Bybit v5 also exposes `linear` and `inverse` public endpoints. This is intentional for the current scope (spot-first), but if TASK-078's `BybitStreamProtocol` targets any non-spot topics, callers must manually override this URL. The class-level XML doc or property summary should state this constraint explicitly.
- **Suggestion**: Before shipping TASK-078, confirm the protocol target is spot-only. If so, add a note to the `StreamBaseUrl` XML summary: `/// <para>The default is the Bybit v5 public spot endpoint. Override for linear or inverse topics.</para>`. No code change required.
- **Fix classification**: AUTO-FIX — purely a documentation clarification; no logic change.

---

## AUTO-FIX Items

| # | File | Line | Change | Reviewer |
|---|------|------|--------|----------|
| 1 | `src/CryptoExchanges.Net.Bybit/Dtos/Streaming/StreamKlineDto.cs` | after line 32 (after `Volume`) | Add `[JsonPropertyName("turnover")] public string Turnover { get; init; } = "0";` with XML doc | code-reviewer, api-reviewer |

---

## ASK Items

| # | File | Description | Severity | Confidence | Reviewer | Reason requires judgment |
|---|------|-------------|----------|------------|----------|--------------------------|
| 1 | Tests (absent) | Add DTO deserialization round-trip tests (deferred scope decision) | LOW | 80 | code-reviewer | Implementer decides: add minimal DTO tests now vs. defer to TASK-078's full decode suite |
| 2 | `StreamTradeDto.cs` | Include or explicitly exclude `BT` (block-trade flag) | LOW | 75 | code-reviewer | `Core.Models.Trade` has no block-trade field; omission may be correct by design |
| 3 | `StreamDepthDto.cs:19,25` | `List<List<string>>` vs `IReadOnlyList<IReadOnlyList<string>>` | LOW | 70 | code-reviewer | Cross-DTO consistency decision (Binance uses same shape) |
| 4 | `BybitStreamOptions.cs:12` | URI scheme validation for `StreamBaseUrl` | LOW | 55 | security-reviewer | Enforcement belongs in TASK-078 protocol; not a property-setter concern |
| 5 | `BybitStreamOptions.cs:12` | Document spot-only constraint on default URL | INFO | 90 | api-reviewer | Scope of documentation update and whether TASK-078 needs non-spot endpoints |

---

## Contradictions Resolved

None. All four reviewers are consistent. The `turnover` omission in `StreamKlineDto` is independently identified by code-reviewer (Finding 1, MEDIUM/90) and api-reviewer (Finding 2, INFO/70) — these are the same logical finding on the same file, merged into a single blocking issue using the higher severity and confidence.

---

## Reviewer Verdicts

| Reviewer | Verdict | Blocking Findings | Concerns |
|----------|---------|-------------------|----------|
| architect-reviewer | ✦ APPROVED | 0 | 0 |
| code-reviewer | ✗ CHANGES_REQUESTED | 1 | 4 |
| security-reviewer | ✦ APPROVED | 0 | 1 |
| api-reviewer | ✦ APPROVED | 0 | 2 |

**Gate verdict: ✗ CHANGES_REQUESTED** — `require_all_approve: true` is satisfied for 3 of 4 reviewers. The single blocking finding from code-reviewer (missing `turnover` field, MEDIUM/90) must be resolved before the gate can be re-evaluated. All non-blocking concerns are auto-approved per `auto_approve_concerns: true`.
