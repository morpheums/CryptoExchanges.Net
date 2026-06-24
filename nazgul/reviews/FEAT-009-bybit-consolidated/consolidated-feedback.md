# Consolidated Review — FEAT-009 Bybit Streaming Group (TASK-075 through TASK-079)

**Overall Verdict: APPROVED**

Pre-checks: Build 0W/0E · Unit tests 127 Bybit + all suites green (0 failures)

---

## Reviewer Verdicts

| Reviewer           | Verdict  | Blocking | Non-Blocking |
|--------------------|----------|----------|--------------|
| architect-reviewer | APPROVED | 0        | 1            |
| code-reviewer      | APPROVED | 0        | 2            |
| security-reviewer  | APPROVED | 0        | 3            |
| api-reviewer       | APPROVED | 0        | 0            |

---

## Blocking Findings

**None.** All four reviewers returned APPROVED with zero blocking findings.

---

## Non-Blocking Concerns

### [architect-reviewer]

1. `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj:16` — `InternalsVisibleTo` for `CryptoExchanges.Net.Bybit.Tests.Unit` is added to the Http project. This is the same pattern already used for `Binance.Tests.Unit` and `Kucoin.Tests.Unit`; intentional and correct. Confidence: 85% (non-blocking, mirrors established pattern).

### [code-reviewer]

2. `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamProtocol.cs:123,131,138` — `topicProp.GetString()`, `opProp.GetString()`, and `successProp.GetBoolean()` are called without a prior `JsonElement.ValueKind` guard. `GetString()` throws `InvalidOperationException` (not `JsonException`) when the element kind is not String, which the `catch (JsonException)` at line 148 will not intercept. A malformed server frame with `"topic": 123` or `"success": "true"` (wrong JSON type) would escape the handler as an unhandled exception rather than `FrameKind.Error`. Operationally rare (Bybit v5 always sends correct types), but inconsistent with the project's own `BybitErrorTranslator.cs:59-65` guard pattern. Confidence: 75% (non-blocking).

3. `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamProtocol.cs:203-220` — `MapInterval()` throws `ArgumentOutOfRangeException` for `KlineInterval.EightHours` and `KlineInterval.ThreeDays` (which have no Bybit v5 equivalent), but this is not documented in the method summary or remarks. Adding a note listing the unsupported members would prevent future maintainer confusion. Confidence: 60% (non-blocking).

### [security-reviewer]

4. `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamOptions.cs:12` — `StreamBaseUrl` is a plain `string` with no `wss://` scheme enforcement. The `Uri` constructor in `BybitStreamProtocol` (line 58) accepts any scheme; a misconfigured `ws://` override would silently use an unencrypted connection without a fail-fast `ArgumentException`. Confidence: 60% (non-blocking — default is correct; operator concern).

5. `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamProtocol.cs:165-173` — `BuildBatch` interpolates `WireSymbol` directly into a raw JSON string via `StringBuilder`. `WireSymbol` is alphanumeric by contract, and `Asset.Of` performs upstream validation, but the frame builder provides no JSON-encoding safety net by construction. Confidence: 45% (non-blocking — very low practical risk).

6. `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamDecoders.cs:122-141` — Exception messages in `DeserializeData` and `DeserializeFirstArrayElement` include `typeof(T).Name` (internal DTO type names). These contain no PII or credentials; risk only if exceptions surface through a public error path. Confidence: 20% (non-blocking, very low risk).

---

## Summary

The Bybit streaming group (TASK-075 through TASK-079) is architecturally sound and correct. All four reviewers approve with zero blocking findings. Key highlights:

- **K1 upheld**: Http layer has no DeltaMapper dependency; decode/mapping closures live entirely in the Bybit package.
- **FEAT-008 lesson applied**: Both `DeserializeData<T>` and `DeserializeFirstArrayElement<T>` unwrap the `"data"` element before deserializing the leaf DTO; the raw envelope is never passed to the deserializer.
- **Routing-key single-sourcing**: `RoutingKeyFor()` and `Classify()` both call `BuildTopic()`, guaranteeing the subscribe-time and receive-time keyspaces agree.
- **Case-sensitive JSON**: `PropertyNameCaseInsensitive = false` is applied globally, preventing `"s"`/`"S"` collision in `StreamTradeDto`.
- **IsBuyerMaker semantics**: `S == "Sell"` → `IsBuyerMaker = true` is correctly implemented.
- **Public surface minimal**: Only `BybitStreamOptions` and `AddBybitStreams()` are public; all impl types are internal.
- **AddBybitStreams() signature**: Exactly mirrors `AddBinanceStreams()` and `AddKucoinStreams()` in signature, namespace, XML docs, and DI delegation.
- **Tests**: 127 Bybit unit tests cover all Classify branches, batch builders (empty + 100-item), decoder envelope-unwrap for all four stream kinds, DI wiring, and heartbeat parameters.

The most actionable concern (confidence 75%) is adding `ValueKind` guards in `Classify()` before `GetString()`/`GetBoolean()` calls to ensure any malformed server frame is cleanly classified as `FrameKind.Error` rather than escaping as `InvalidOperationException`. This is a quality improvement, not a blocking defect — the practical risk is low since Bybit v5 wire protocol always sends the correct JSON types. Recommend addressing in a follow-up simplify pass or as part of the OKX/Bitget work if a shared pattern emerges.
