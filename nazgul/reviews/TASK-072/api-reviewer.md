# API Review — TASK-072 (FEAT-008)

## Scope
`BuildSubscribeBatch` / `BuildUnsubscribeBatch` added to `IStreamProtocol` (internal), implemented in `BinanceStreamProtocol` and `KucoinStreamProtocol` (both internal sealed), consumed in `StreamEngine` (internal sealed). `FakeStreamProtocol` (internal test helper) also updated.

## Findings

### Finding: Zero public surface change
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:21`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. `IStreamProtocol` is `internal interface`; new members never reach the public API surface. `IStreamClient` is untouched. No public type in `Core`, `Binance`, or `KuCoin` packages is modified.
- **Fix**: N/A
- **Pattern reference**: `IStreamProtocol.cs:21` — `internal interface IStreamProtocol`

### Finding: Default interface members are additive and safe for internal consumers
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:93,113`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. Both new members carry `=> null` default implementations. Existing internal implementers (and any test fakes not yet updated) compile unchanged and automatically get the "batching unsupported" fallback path with no behavioral regression. `FakeStreamProtocol` opts in explicitly and the engine falls back correctly when `SupportsBatch = false`.
- **Fix**: N/A

### Finding: No accidental public exposure
- **Confidence**: 100
- **File**: diff at large
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. All four changed files (`IStreamProtocol`, `BinanceStreamProtocol`, `KucoinStreamProtocol`, `StreamEngine`) are `internal`. `FakeStreamProtocol` is `internal` in a test project (`IsPackable=false`). No `InternalsVisibleTo` is added that would expose these to external consumers.
- **Fix**: N/A

## Summary

- PASS: Zero public API surface change — all modified types are `internal`; `IStreamClient` is untouched; no SemVer impact.
- PASS: Default interface members (`=> null`) on the internal `IStreamProtocol` are fully additive; every internal consumer and test fake remains source-compatible.
- PASS: No accidental public exposure — no `InternalsVisibleTo` added, no access modifier widening.

Final Verdict: APPROVED
