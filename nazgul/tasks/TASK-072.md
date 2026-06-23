---
id: TASK-072
status: PLANNED
depends_on: [TASK-071]
---
# TASK-072: Batched reconnect-replay — `IStreamProtocol` batch builders + Binance/KuCoin impls + batched `ReconnectCoreAsync`

## Metadata
- **ID**: TASK-072
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-071
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs, src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs, src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs, src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs, tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs]
- **Wave**: 2
- **Traces to**: FEAT-008 objective (config.json) — "respect a per-venue message-rate limit (batching ... per exchange)"; architect-reviewer advisory §"Step 2 — Batching"; risk register row "Reconnect-replay burst"
- **Created at**: 2026-06-23T22:35:00Z
- **Claimed at**:
- **Base SHA**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

This is the scale optimisation for reconnect replay. It is **additive and internal** — `IStreamProtocol`
is `internal`, the new members are default-implemented to return `null`, so existing fakes/impls keep
compiling. It composes on TASK-071: batched frames are still dispatched through `SendControlAsync`, so
each frame is still throttled by `MinOutboundInterval`. A 300-symbol replay becomes ~3 frames × 200 ms
≈ 600 ms instead of 300 frames × 200 ms ≈ 60 s.

**Depends on TASK-071** for `SendControlAsync` (the batched replay sends through it).

Steps (exactly as the architect advisory mandates):

1. **`IStreamProtocol.cs`** — add two **default interface members** returning `null`:
   - `string? BuildSubscribeBatch(IReadOnlyList<StreamRequest> requests) => null;`
   - `string? BuildUnsubscribeBatch(IReadOnlyList<StreamRequest> requests) => null;`
   Give each full XML docs (`<summary>`/`<param>`/`<returns>`): the method returns a single wire frame
   that subscribes/unsubscribes **all** of `requests` at once, or `null` when the venue does not
   support batching (the engine then falls back to the per-frame loop). Note that callers must chunk
   the input themselves only if a single frame would exceed the venue cap — i.e. document that an
   implementation may assume `requests` already fits one frame, OR that the engine passes the full set
   and the implementation must return a frame covering exactly that set. **Pick the latter contract**:
   the engine chunks before calling, so each `BuildSubscribeBatch` call receives a list that already
   fits one frame (≤ 100 tokens). Document this explicitly so impls don't re-chunk. (Default
   `null`-returning members are non-breaking because the interface is `internal`; fakes need no change
   unless they test batching.)

2. **`BinanceStreamProtocol.cs`** — implement `BuildSubscribeBatch` / `BuildUnsubscribeBatch`:
   multi-param array shape `{"method":"SUBSCRIBE","params":["a@depth20","b@depth20",...],"id":N}`
   (and `UNSUBSCRIBE`). Reuse the existing `BuildStreamToken` helper for each request; one `id` per
   frame via `Interlocked.Increment(ref _nextId)`. Return the JSON for the whole `requests` list (the
   engine guarantees ≤ 100 tokens per call). `<inheritdoc/>` on both.

3. **`KucoinStreamProtocol.cs`** — implement both: comma-joined topic
   `{"id":"N","type":"subscribe","topic":"/market/level2:BTC-USDT,ETH-USDT,...","privateChannel":false,"response":true}`
   (and `unsubscribe`). Reuse `BuildTopic` to derive the per-request suffix and join the **symbol**
   portion with commas under a single channel prefix. **Important**: a batch is only valid when all
   requests share the same channel prefix (same `StreamKind` + interval); if the engine passes a mixed
   set, group by channel prefix is the engine's responsibility (see step 4) — KuCoin's impl may assume
   a homogeneous channel group and return `null` if the set is not homogeneous (defensive). `<inheritdoc/>`.

4. **`StreamEngine.cs` — `ReconnectCoreAsync`** replay block (currently the `foreach` at ~line 531):
   - Snapshot the replay set: `var requests = _subscribeSet.Values.ToList();`.
   - **Chunk** into groups of ≤ 100 (Binance: 100 tokens/frame; KuCoin: 100 symbols/frame — use a
     single `const int MaxBatchSize = 100;`). For KuCoin homogeneity, group the chunk by routing-key
     channel prefix is NOT required if the impl returns `null` on mixed sets and the engine falls back
     per-frame; **simplest correct approach**: try `BuildSubscribeBatch(chunk)`; if it returns
     non-null, send that one frame via `SendControlAsync` (throttled); if it returns `null`, fall back
     to the existing per-request `BuildSubscribe` + `SendControlAsync` loop for that chunk. Keep the
     per-item broad-catch + `s_logReplayFailed` semantics (wrap each frame send; one failure must not
     abort the rest).
   - This preserves K2 (full replay) and stays under the rate limit because every frame — batched or
     not — goes through `SendControlAsync`.
   - Add a `LoggerMessage` delegate if you log batch sizes (optional; follow `s_log*` pattern). Do not
     add noisy per-frame logging.

5. **Tests** (`StreamEngineTests.cs`, fast unit, fakes). Extend `FakeStreamProtocol` to implement the
   batch members (return a recognisable batched frame, e.g. `"SUBSCRIBE_BATCH:" + count`, and support
   a toggle to return `null` to exercise the fallback path). Cover:
   - **Reconnect-replay is paced/batched (no burst)**: subscribe to ≥3 keys, simulate disconnect →
     reconnect; assert the replay uses the batch builder (few frames) AND that frames are spaced
     ≥ `MinOutboundInterval` (no N-frame instantaneous burst).
   - **Chunking at 100**: drive a replay set > 100 entries through the engine with a batch-capable
     fake; assert the engine emits ⌈N/100⌉ batch frames (chunk boundary honored). (Use the fake to
     assert chunk sizes ≤ 100.)
   - **Correct wire JSON for both venues** (protocol-level unit tests, no engine): call
     `BinanceStreamProtocol.BuildSubscribeBatch` / `BuildUnsubscribeBatch` and
     `KucoinStreamProtocol.BuildSubscribeBatch` / `BuildUnsubscribeBatch` with a small list and assert
     the exact `params:[...]` array (Binance) and comma-joined `topic` (KuCoin) shapes — including the
     ≤100 expectation by passing exactly 100 and 1.
   - **Fallback to per-frame when batch returns null**: a fake returning `null` from the batch builder
     replays via the per-frame throttled loop; assert N frames sent, still spaced.
   Place the Binance/KuCoin protocol JSON assertions in their respective unit-test projects if engine
   internals are not accessible there; otherwise keep protocol shape assertions co-located with the
   engine replay tests using the real protocols (they live in exchange assemblies — prefer the
   per-exchange `.Tests.Unit` projects for venue JSON shape).

Constraints: one type per file; LEAN comments; full XML docs on the new **interface** members and
`<inheritdoc/>` on impls (CLAUDE.md mandate — interface members get docs even as defaults). Build clean
under TreatWarningsAsErrors + AnalysisLevel=latest-all (mind CA1002/CA1822, CA2007, CA1031). Strict
layering preserved: batch **dispatch** (chunking + fallback) lives in Http `StreamEngine`; batch **wire
format** lives in each exchange protocol.

## Acceptance Criteria
- [ ] `IStreamProtocol` declares `string? BuildSubscribeBatch(IReadOnlyList<StreamRequest>)` and `string? BuildUnsubscribeBatch(IReadOnlyList<StreamRequest>)` as default members returning `null` (fully XML-documented; non-breaking — interface is `internal`, existing fakes/impls still compile); Binance implements the multi-param array shape and KuCoin the comma-joined topic shape (`<inheritdoc/>`), each chunked/handled at ≤ 100 per frame. **Zero public API change.** `dotnet build CryptoExchanges.Net.sln` 0W/0E.
- [ ] `ReconnectCoreAsync` replays via `BuildSubscribeBatch` first (chunked at 100, each frame sent through `SendControlAsync` so it is throttled), falling back to the per-frame throttled loop when the builder returns `null`; K2 full-replay semantics and the per-frame broad-catch/`s_logReplayFailed` behavior are preserved.
- [ ] New fast unit tests (no network) pass and prove: (a) reconnect replay is batched/paced with no burst; (b) chunking at 100 produces ⌈N/100⌉ frames; (c) Binance and KuCoin batch builders emit the correct wire JSON for a sample list (and for exactly 100 / exactly 1); (d) `null`-returning batch builder falls back to the throttled per-frame loop. `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- Default-interface-member with XML docs: `IStreamProtocol.cs:21-94` (existing members — match doc style; add defaults returning `null`).
- Per-frame Binance wire JSON to mirror in batch form: `BinanceStreamProtocol.cs:54-69` (`BuildSubscribe`/`BuildUnsubscribe`, `BuildStreamToken` 118-132).
- Per-frame KuCoin wire JSON to mirror: `KucoinStreamProtocol.cs:57-72` (`BuildSubscribe`/`BuildUnsubscribe`, `BuildTopic` 126-136).
- Reconnect replay loop to replace: `StreamEngine.cs:531-544` (the `foreach (var (routingKey, request) in _subscribeSet)` block) — now routes through `SendControlAsync` from TASK-071.
- Unit-test harness + fakes: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs`, `FakeStreamProtocol.cs` (extend with batch members), `FakeWebSocketConnection.cs` (`SimulateDisconnect`/`SimulateReconnect`, `SentText`).

## File Scope

**Creates**:
- (none — additive members on existing types; no new top-level type)

**Modifies**:
- src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs (two default members)
- src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs (batched + chunked reconnect replay with per-frame fallback)
- src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs (batch builders)
- src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs (batch builders)
- tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs (batch/chunk/fallback engine tests; extend FakeStreamProtocol)

**Deletes**:
- (none)

**Note**: Binance/KuCoin batch-JSON shape assertions may instead live in `tests/CryptoExchanges.Net.Binance.Tests.Unit/` and `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/` if engine internals there are inaccessible — the implementer chooses the home that compiles cleanly. If used, those test files join this task's file scope.

## Traceability
- **Objective**: config.json FEAT-008 — batching of the reconnect replay so a multi-symbol reconnect does not re-trigger the rate-limit/reconnect loop.
- **Advisory**: `nazgul/reviews/FEAT-008/architect-reviewer.md` §"Step 2 — Batching (reconnect-replay optimisation, additive protocol change)" + risk register row "Reconnect-replay burst".
- **Constraints**: CLAUDE.md (one type per file, XML docs on interface members incl. defaults, `<inheritdoc/>`, LEAN comments), TreatWarningsAsErrors/AnalysisLevel=latest-all, strict layering.

## Commits

- (pending)

## Implementation Log

- (pending)

## Review Results

- (pending)
