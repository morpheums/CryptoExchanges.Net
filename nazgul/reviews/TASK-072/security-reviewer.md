# Security Review — TASK-072 (FEAT-008)

## Scope
Batch-building of WebSocket subscribe/unsubscribe control frames for Binance (params array) and KuCoin (comma-joined topics), plus chunked reconnect-replay through the existing throttled send path. No signing, credential, or HTTP request-signing code is touched.

---

## Findings

### Finding: Input injection into wire frames
- **Severity**: LOW
- **Confidence**: 95
- **File**: `BinanceStreamProtocol.cs:155-168`, `KucoinStreamProtocol.cs:169-176`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: `WireSymbol` flows directly into the JSON frame string without JSON-escaping. However, `WireSymbol` is exclusively mapper-derived (set by the exchange adapter before `StreamRequest` is constructed, then stored in `_subscribeSet` at `StreamEngine.cs:242`). It is never supplied raw from untrusted consumer input — consumers pass canonical `Symbol` types that the adapter resolves. No code path allows arbitrary strings to be written into `WireSymbol`.
- **Pattern reference**: `StreamRequest.cs:8` ("Symbol resolution … happens exchange-side before this record is built")

### Finding: Chunk bound prevents unbounded frame size
- **Severity**: LOW
- **Confidence**: 98
- **File**: `StreamEngine.cs:138`, `StreamEngine.cs:187-189`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: `MaxBatchSize = 100` is enforced by `Math.Min(MaxBatchSize, requests.Count - offset)` in the chunker loop. Both venue protocols document the same 100-entry cap. Tests `Engine_ReconnectReplay_ChunksAt100_Produces3FramesFor250` and `Engine_ReconnectReplay_Batched_IsPacedByMinOutboundInterval` assert the bound and throttling. No unbounded frame is possible.

### Finding: Resource leak in replay path
- **Severity**: LOW
- **Confidence**: 97
- **File**: `StreamEngine.cs:180-227`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: `ReplaySubscribeSetAsync` snapshots `_subscribeSet.Values.ToList()` (no open enumerator or unmanaged resource), routes every send through the existing `SendControlAsync` which is already guarded by `_disposeCts`, and wraps each send in a broad `catch` so one failure does not leak state or leave locks held. `_gate` is not held during replay (replay is called from within the reconnect task after `_gate` is already released), consistent with the prior per-frame loop it replaces.

### Finding: DoS via reconnect amplification
- **Severity**: MEDIUM
- **Confidence**: 90
- **File**: `StreamEngine.cs:180-227`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: A rapid reconnect loop with 250 subscriptions previously sent 250 frames per reconnect. The new code reduces this to 3 batched frames (100/100/50), each still throttled by `MinOutboundInterval` via `SendControlAsync`. The change strictly reduces the reconnect-storm footprint, not increases it. No new DoS surface is introduced.

### Finding: KuCoin mixed-channel colon-split robustness
- **Severity**: LOW
- **Confidence**: 85
- **File**: `KucoinStreamProtocol.cs:145,154`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: `BuildBatch` splits each topic string on `LastIndexOf(':')`. If `BuildTopic` ever returned a string with no colon, `colon` would be `-1` and `topic.AsSpan(0, sep)` on `sep = -1` would throw `ArgumentOutOfRangeException`. However, every branch of `BuildTopic` (lines 171-176) produces a string beginning with `/market/<channel>:`, so a colon is always present. The guard is structural, not runtime-validated, which is acceptable for an `internal` type whose inputs are fully controlled. Not a blocking issue.

---

## Summary

- PASS: Input injection — `WireSymbol` is mapper-derived and structurally controlled; no consumer string reaches the frame builder.
- PASS: Chunk bound — `MaxBatchSize = 100` hard-caps every chunk with `Math.Min`; tested by two unit tests covering 250-subscription scenarios.
- PASS: Resource leak — snapshot + `_disposeCts`-guarded sends + per-frame catch; no unmanaged resources or lock leaks.
- PASS: DoS via reconnect — batch path reduces frame count 100x for large sets; throttling is preserved through `SendControlAsync`.
- PASS: KuCoin colon-split — no runtime risk given `BuildTopic` always produces a colon-prefixed channel; structurally safe for an `internal` type.

## Final Verdict: APPROVED
