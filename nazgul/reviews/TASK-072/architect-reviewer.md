# Architect Review — TASK-072 (FEAT-008 Step 2: batched reconnect-replay)

## Checks

**Layering (Http engine vs per-exchange protocol):** `IStreamProtocol.BuildSubscribeBatch` / `BuildUnsubscribeBatch` are added in `CryptoExchanges.Net.Http` with default-null implementations. Exchange-specific logic lives exclusively in `BinanceStreamProtocol` and `KucoinStreamProtocol`. `StreamEngine` calls only the interface — no exchange knowledge in Http. PASS.

**Conformance to approved Step-2 design:** Every design point matches verbatim — default-null DIM on the internal interface (non-breaking), Binance multi-param array, KuCoin comma-joined topic returning null on heterogeneous channel prefix, engine chunks `_subscribeSet` at ≤100 (`MaxBatchSize = 100`), one batched frame per chunk, null-return falls back to the per-frame throttled loop. PASS.

**K2 replay semantics preserved:** `ReplaySubscribeSetAsync` snapshots `_subscribeSet.Values`, iterates all entries, and a per-chunk catch block mirrors the prior per-item catch (one failure cannot abort remaining chunks). K2 contract upheld. PASS.

**Batched frames still throttled:** Every frame — batched or per-frame fallback — routes through `SendControlAsync`. Three new test cases assert pacing (`Engine_ReconnectReplay_Batched_IsPacedByMinOutboundInterval` drives 150 subs → 2 frames, verifies ≥ `MinOutboundInterval` gap). PASS.

**No public API change:** `IStreamProtocol`, `StreamConnectionInfo` are `internal`. No public type touched. PASS.

**Minor observation (non-blocking):** On a batched-frame send failure, `s_logReplayFailed` is called with `RoutingKeyFor(chunk[0])` — only the first key of the chunk is logged. The rest of the chunk's routing keys are silently lost from the log. Given this is a diagnostic-only code path and the exception itself is logged, this is acceptable but worth noting for future observability work.

## Summary

- PASS: Layering — Http engine holds only the interface; exchange impls in their own assemblies
- PASS: Step-2 design conformance — DIMs, Binance multi-param, KuCoin comma-joined, engine chunking all match advisory exactly
- PASS: K2 replay semantics — snapshots full set, per-chunk error isolation, all entries replayed
- PASS: Throttling preserved — all frames (batched and fallback) route through SendControlAsync; pacing tests added
- PASS: Zero public API change
- CONCERN: Batch send-failure logs only chunk[0] routing key (confidence: 60, non-blocking)

Final Verdict: APPROVED
