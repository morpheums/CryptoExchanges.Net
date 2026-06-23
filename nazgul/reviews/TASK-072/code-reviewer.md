# Code Review — TASK-072 (FEAT-008): Batched Reconnect-Replay

**Reviewer**: Code Reviewer (automated)
**Date**: 2026-06-23

---

## Findings

### Finding: IStreamProtocol XML docs are verbose/essay-like on `<remarks>`
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:68-113`
- **Category**: Style
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: Both `BuildSubscribeBatch` and `BuildUnsubscribeBatch` carry two-paragraph `<remarks>` blocks explaining the null contract and chunking contract. The lean-comments mandate calls for `<summary>` + `<param>`/`<returns>`/`<exception>` only where they add information the signature doesn't. These remarks are genuinely load-bearing (they define a two-part contract that implementations must honour), so the content is justified — but the essays could be condensed.
- **Fix**: Consider tightening to one-sentence contract lines rather than full paragraphs, e.g. "Null return = batching unsupported; engine falls back to per-frame loop. Engine pre-chunks so each invocation receives ≤100 entries." This is a style nit, not a blocking defect.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:54-58` (concise single-sentence `<summary>` on `BuildSubscribe`)

### Finding: KuCoin `BuildBatch` — no guard for `colon == -1`
- **Severity**: LOW
- **Confidence**: 40
- **File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs:144-145`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `firstTopic.LastIndexOf(':')` returns -1 if the string contains no colon. `firstTopic[..colon]` with `colon == -1` produces an empty prefix (index 0 to -1 is an empty range in C# slicing — it throws `ArgumentOutOfRangeException` since `^1` is `Length-1`, not -1). In practice this is unreachable because `BuildTopic` always emits a colon-prefixed path (e.g. `/market/snapshot:`) and throws `ArgumentOutOfRangeException` for unknown `StreamKind` before reaching `BuildBatch`. There is no code path today that can produce a colon-free topic. Not a blocking defect but a latent footgun if `BuildTopic` is ever extended carelessly.
- **Fix**: No change needed now. If `BuildTopic` is extended, add a guard: `if (colon < 0) return null;` before the slice.
- **Pattern reference**: Not applicable (unreachable with current `BuildTopic`)

### Finding: `s_logBatchedReplay` EventId 18 placed non-sequentially in source
- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:77-78`
- **Category**: Style
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: Event IDs 12–17 follow 11 in order, but 18 is inserted between 11 and 12 in the static field list. This is minor ordering noise; the runtime EventId values are correct and non-colliding.
- **Fix**: Reorder the declaration to appear after EventId 17 for readability. Not blocking.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:44-96` (sequential 1–17 ordering)

---

## Checklist

### Correctness
- PASS: All `await` calls use `.ConfigureAwait(false)` — `StreamEngine.cs:603`, `643`, `661`.
- PASS: `_disposeCts.Token` forwarded to every `SendControlAsync` call in the replay loop.
- PASS: `OperationCanceledException` is not swallowed — existing `catch (OperationCanceledException) { return; }` at reconnect loop top still present; `ReplaySubscribeSetAsync` uses `CA1031`-justified broad catches only for logging continuations.
- PASS: No new `IDisposable`/`IAsyncDisposable` instances without `using`/`await using`.
- PASS: `chunk[0]` in `s_logReplayFailed` on the batched-fail path is safe — chunk is non-empty by loop invariant (`GetRange` with `Math.Min` never produces an empty list when `offset < requests.Count`).
- PASS: `_subscribeSet.Values.ToList()` snapshot taken while `_gate` is held (reconnect loop acquires `_gate` at line 567 before calling `ReplaySubscribeSetAsync` at line 603).
- PASS: `GetRange(offset, Math.Min(MaxBatchSize, requests.Count - offset))` arithmetic is correct for all boundary cases: empty list (early return), exactly 100 (single chunk), 250 (three chunks: 100/100/50), single item (one chunk of 1).
- PASS: KuCoin channel-grouping: `LastIndexOf(':')` correctly isolates the prefix (e.g. `/market/level2`) from the symbol; mixed-channel set returns `null` and falls back per-frame as documented.
- PASS: Binance `BuildBatch` returns `null` on empty list, valid JSON array for all other sizes.
- PASS: `_nextId` atomicity: both Binance (`int`) and KuCoin (`long`) use `Interlocked.Increment` in `BuildBatch` — same pattern as their existing single-item methods.
- PASS: EventId 18 is unique (1–17 already used; 18 is new and non-colliding).

### Null safety
- PASS: `ArgumentNullException.ThrowIfNull(requests)` present on both `Binance.BuildBatch` and `Kucoin.BuildBatch`.
- PASS: Interface default implementations (`=> null`) need no guards — they discard the argument.
- PASS: `FakeStreamProtocol.BuildSubscribeBatch` and `BuildUnsubscribeBatch` handle `SupportsBatch = false` → null path; the `SupportsBatch = true` path never dereferences a nullable.

### Documentation
- PASS: New interface members `BuildSubscribeBatch`/`BuildUnsubscribeBatch` have full `<summary>`, `<param>`, `<returns>` on the interface.
- PASS: All implementations (`BinanceStreamProtocol`, `KucoinStreamProtocol`, `FakeStreamProtocol`) use `/// <inheritdoc/>` — no doc duplication.
- PASS: `FakeStreamProtocol` new properties (`SupportsBatch`, `SubscribeBatchChunkSizes`) have concise `<summary>` docs; they implement no interface so docs are needed.
- PASS: `ReplaySubscribeSetAsync` is private — no XML doc required.
- PASS: `MaxBatchSize` constant has a single-line explanatory comment (non-obvious exchange-constraint rationale — justified).
- PASS: `s_logBatchedReplay` has no inline comment (self-evident from the log template string).
- CONCERN: `BuildSubscribeBatch`/`BuildUnsubscribeBatch` `<remarks>` are somewhat verbose (see Finding 1 above).

### Code style
- PASS: No new banner/separator comments added outside the existing `// ── X ──` pattern already used throughout `StreamEngine.cs`.
- PASS: `StringBuilder` pre-sizing in `Binance.BuildBatch` follows idiomatic high-throughput pattern.
- PASS: `requests.GetRange(...)` on `List<T>` is idiomatic; no unnecessary `new List<T>()` allocations.
- PASS: No new `public` mutable fields.

### CA / Roslyn compliance
- PASS: `dotnet build` — 0 warnings, 0 errors with `TreatWarningsAsErrors=true`.
- PASS: All `#pragma warning disable CA1031` blocks are paired with `#pragma warning restore CA1031` and carry justification comments.
- PASS: No new `NoWarn` entries added.

### Tests
- PASS: 15 new tests across `BinanceStreamProtocolTests`, `KucoinStreamProtocolTests`, and `StreamEngineTests`.
- PASS: Empty-list → null, single-item, exactly-100, >100 (250 → 3 chunks), fallback-to-per-frame, and pacing all covered.
- PASS: `dotnet test` — all 616 non-integration tests green.

---

## Summary

- PASS: Chunking arithmetic (`GetRange` + `Math.Min`) — correct for all boundary cases including empty, single, exactly-100, and >100.
- PASS: KuCoin channel-grouping — `LastIndexOf(':')` correctly partitions prefix/symbol; mixed-channel null fallback is correct and tested.
- PASS: XML docs on new interface members; `<inheritdoc/>` on all implementations.
- PASS: No banner or self-evident comments introduced outside existing codebase conventions.
- PASS: CA2007 / CA1031 / dispose — all clean; build 0/0; all tests green.
- CONCERN: `IStreamProtocol` `<remarks>` blocks are verbose (confidence: 55/100, non-blocking).
- CONCERN: KuCoin `colon == -1` is unreachable today but unguarded (confidence: 40/100, non-blocking).
- CONCERN: `s_logBatchedReplay` EventId 18 declared out of sequence in source (confidence: 60/100, non-blocking).

Final Verdict: APPROVED
