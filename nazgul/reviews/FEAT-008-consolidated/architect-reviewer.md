# Architect Review — FEAT-008 Consolidated (Objective-Level Gate)

**Scope**: Full diff in `nazgul/reviews/FEAT-008-consolidated/diff.patch`.
TASK-071 (throttle / SendControlAsync) and TASK-072 (batch builders + batched replay) were
previously APPROVED 4/4. This review re-confirms those tasks show no regression under the
later work, and provides first-pass scrutiny of TASK-074 (BinanceStreamDecoders
envelope-unwrap fix) and the multi-symbol integration regression tests (TASK-073/074).

---

## Checks

### Finding: Layering — decoder stays in the exchange package (K1)
- **Severity**: N/A — PASS
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: `BinanceStreamDecoders` is `internal static class` in the Binance package. Its
  `using` imports are `CryptoExchanges.Net.Binance.Dtos.Streaming`, `CryptoExchanges.Net.Binance.Internal`,
  `CryptoExchanges.Net.Http.Streaming` (only `StreamDecoderRegistry`), and `DeltaMapper`.
  `StreamEngine` remains in `CryptoExchanges.Net.Http` and receives only the opaque
  `StreamDecoderRegistry` of `Func<ReadOnlyMemory<byte>, object>`. No DTOs, no Core.Models,
  no exchange-specific types cross into the Http layer. K1 is fully observed.

### Finding: Envelope-unwrap pattern — mirrors established KuCoin pattern
- **Severity**: N/A — PASS
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs:143-151`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: `DeserializeData<T>` uses `Utf8JsonReader` + `JsonDocument.ParseValue`, then
  `.TryGetProperty("data"u8, ...)`, then `.Deserialize<T>(JsonOpts)` — an exact structural
  match for `KucoinStreamDecoders.DeserializeData<T>`. The one intentional divergence in
  error handling is correct: Binance throws on a missing `data` property (the `/stream`
  combined-stream endpoint always wraps; absence is a hard protocol violation), while KuCoin
  falls through to bare deserialization (the KuCoin per-socket path can deliver bare frames).
  Both behaviours are appropriate to their venue contract.

### Finding: Symbol-resolution fallback for partial-book frames
- **Severity**: N/A — PASS
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs:75-82, 169-178`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: The two-tier fallback (`dto.Symbol` when present; `WireSymbolFromStreamToken`
  from the envelope `stream` field otherwise; throw when neither resolves) is sound and
  correctly addresses the documented Binance split: diff-depth (`@depth`) includes `"s"`,
  partial-book (`@depthN`) does not. `WireSymbolFromStreamToken` upper-cases the symbol prefix
  so it matches `ISymbolMapper.FromWire`'s warm table (keyed upper-case), which is the correct
  normalization. The new unit test `OrderBook_PartialBookEnvelope_ResolvesSymbolFromStreamToken`
  exercises this exact path with the `btcusdt@depth20` envelope shape.

### Finding: TASK-071 throttle/SendControlAsync — no regression
- **Severity**: N/A — PASS
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: `SendControlAsync`, `_sendSemaphore`, `_minOutboundInterval`, `_lastSendTicks`,
  `ApplyConnectionPacing`, and the linked-token pattern are all present and structurally
  unchanged from the previously-approved design. The heartbeat ping now routes through
  `SendControlAsync` (fixing the pre-existing concurrent-send race noted in the FEAT-008
  advisory). `_livenessFlag` correctly drops the `volatile` annotation — `Interlocked.Exchange`
  provides the necessary memory visibility without it. `_sendSemaphore.Dispose()` is called in
  `DisposeAsync`. No regression.

### Finding: TASK-072 batch builders + batched replay — no regression
- **Severity**: N/A — PASS
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:461-514`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: `ReplaySubscribeSetAsync` chunks `_subscribeSet.Values` at `MaxBatchSize = 100`,
  calls `BuildSubscribeBatch` per chunk, falls back to the per-frame throttled loop on `null`,
  and routes every send through `SendControlAsync`. Both Binance and KuCoin implementations of
  `BuildSubscribeBatch`/`BuildUnsubscribeBatch` are correct: Binance emits a multi-param array
  (`"params":[...]`); KuCoin emits a comma-joined topic (returning `null` for mixed channels so
  the engine falls back correctly). Default interface members return `null` so all other
  protocols and test fakes compile unchanged. No regression.

### Finding: Public API surface — zero change
- **Severity**: N/A — PASS
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs`,
  `src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: `IStreamProtocol` is `internal`; adding default members is non-breaking.
  `StreamConnectionInfo` gains `MinOutboundInterval = default` as an optional named parameter
  on an `internal sealed record`; all existing call sites that pass two arguments compile
  unchanged. Zero public API changes to `IStreamClient`, `IMarketDataService`, `ITradingService`,
  `IAccountService`, or any other public interface.

### Finding: Build gate
- **Severity**: N/A — PASS
- **Confidence**: 100
- **File**: solution root
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- **Detail**: `dotnet build -c Release` exits with 0 warnings and 0 errors under
  `TreatWarningsAsErrors=true`.

---

## Summary

- PASS: K1 layering — decode + unwrap stays fully within the Binance exchange package; Http engine is byte-opaque.
- PASS: Envelope-unwrap pattern — `DeserializeData<T>` and `DeserializeDepth` mirror the KuCoin reference pattern structurally and correctly diverge only in error-handling (Binance throws; KuCoin falls through), reflecting each venue's wire contract.
- PASS: Symbol-resolution fallback — two-tier (`dto.Symbol` -> stream token -> throw) is sound; `ToUpperInvariant()` ensures compatibility with `FromWire` key table; covered by dedicated unit test.
- PASS: TASK-071 throttle/SendControlAsync — no regression; heartbeat concurrency race resolved; `_livenessFlag volatile` cleanup is correct.
- PASS: TASK-072 batch builders + batched replay — no regression; chunking, batching, and per-frame fallback all intact; default interface members preserve backward compatibility for other protocols and fakes.
- PASS: Public API surface — zero public-facing changes across all packages.
- PASS: Build — 0 warnings, 0 errors, `TreatWarningsAsErrors=true`.

---

## Final Verdict: APPROVED
