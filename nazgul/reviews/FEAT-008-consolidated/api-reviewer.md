# API Review — FEAT-008-consolidated

## Scope

Outbound control-frame throttling (TASK-071) and batched/chunked reconnect-replay (TASK-072), fixing multi-symbol subscriptions and Binance partial-book order-book decoding.

---

## Findings

### Finding: StreamConnectionInfo — new optional record parameter
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs:31`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: `MinOutboundInterval = default` added as a third positional parameter with a default value. The record is `internal sealed`, not part of any public NuGet surface. Existing internal call sites (`BinanceStreamProtocol`, `KucoinStreamProtocol`, `FakeStreamProtocol`) all compile because (a) named-parameter call sites pass it explicitly, and (b) omitted sites get `TimeSpan.Zero` (unthrottled, identical to prior behaviour).
- **Pattern reference**: `src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs:31`

### Finding: IStreamProtocol — two new default interface members
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:211,231`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: `BuildSubscribeBatch` and `BuildUnsubscribeBatch` added with `=> null` DIM bodies. The interface is `internal`, so external implementers cannot exist. Existing internal implementers (`BinanceStreamProtocol`, `KucoinStreamProtocol`, `FakeStreamProtocol`) all provide explicit overrides; the default fallback is correct semantics (batching unsupported → per-frame loop).

### Finding: SendControlAsync — private method on internal class
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:396`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: `private async Task SendControlAsync(string text, CancellationToken ct)` — private, on an `internal sealed` class. Zero public exposure.

### Finding: BinanceStreamDecoders — private helper methods
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs:68-103`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: `DeserializeData<T>`, `DeserializeDepth`, `WireSymbolFromStreamToken` are all `private static` on an `internal static class`. Zero public exposure.

### Finding: Public IStreamClient (Core) — untouched
- **Severity**: LOW
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Interfaces/IStreamClient.cs:13`
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: The diff contains no changes to `IStreamClient`, `IStreamSubscription`, or any other type under `CryptoExchanges.Net.Core`. Consumer-facing contract is byte-identical.

### Finding: BinanceOptions / AddBinanceExchange / AddCryptoExchanges — untouched
- **Severity**: LOW
- **Confidence**: 100
- **File**: n/a (not in diff)
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None of the DI extension signatures or public options types appear in the diff.

### Finding: Behavioral change — partial-book order book now resolves symbol from stream token
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs:40-46`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: Previously, partial-book (`@depthN`) frames threw `InvalidOperationException` unconditionally because the `s` field is absent. The fix falls back to the stream token symbol. This is a pure bug fix: callers subscribing to `depth: 20` would have received only exceptions before; now they receive data. No contract change, strictly additive correctness.

---

## Summary

- PASS: `StreamConnectionInfo.MinOutboundInterval` — internal record, optional parameter with `default`, no public surface change.
- PASS: `IStreamProtocol.BuildSubscribeBatch/BuildUnsubscribeBatch` — internal interface, DIM default returns null, non-breaking additive.
- PASS: `SendControlAsync` — private on internal sealed class, zero exposure.
- PASS: `BinanceStreamDecoders` helpers — private static on internal static class, zero exposure.
- PASS: `IStreamClient` (Core public) — not touched by this diff.
- PASS: Partial-book order book symbol resolution — bug fix only, no contract change.

## Final Verdict: APPROVED
