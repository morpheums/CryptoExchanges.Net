---
id: TASK-071
status: DONE
depends_on: []
---
# TASK-071: Throttle + serialize outbound control frames in StreamEngine (`MinOutboundInterval` + `SendControlAsync`)

## Metadata
- **ID**: TASK-071
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: none
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs, src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs, src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs, src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs, tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs]
- **Wave**: 1
- **Traces to**: FEAT-008 objective (config.json) — "respect a per-venue message-rate limit (pacing)"; architect-reviewer advisory §"Step 1 — Throttling"; risk register rows "Concurrent ClientWebSocket.SendAsync", "Dispose during throttle delay"
- **Created at**: 2026-06-23T22:35:00Z
- **Claimed at**: 2026-06-23T22:50:00Z
- **Base SHA**: ac49fca0717de18899a918d9b25b91212a38fadc
- **Implemented at**: 2026-06-23T23:10:00Z
- **Completed at**: 2026-06-23T23:55:00Z
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

This is the correctness fix. It introduces a per-venue outbound message-rate limit and routes
**every** `_socket.SendTextAsync` call through one serialized, paced send path. Zero public API
change — every type touched is `internal`.

Steps (exactly as the architect advisory mandates — do NOT re-evaluate the approach):

1. **`StreamConnectionInfo.cs`** — add a third positional record parameter
   `TimeSpan MinOutboundInterval` with a **default of `TimeSpan.Zero`** so existing call sites that
   construct the two-arg shape continue to compile and mean "no limit". Add a `<param>` XML doc:
   `MinOutboundInterval` is the minimum wall-clock spacing the engine enforces between consecutive
   outbound control frames (subscribe/unsubscribe/ping/replay) on this connection;
   `TimeSpan.Zero` = unthrottled. Note in the doc that this is a **venue property** (like
   `HeartbeatPolicy`), not a consumer setting. Keep the K1 `<remarks>` accurate (the record still
   carries only venue/transport policy data — no `Core.Models`, no DeltaMapper).

2. **`StreamEngine.cs`** — add send infrastructure:
   - A dedicated field `private readonly SemaphoreSlim _sendSemaphore = new(1, 1);` — **separate from
     `_gate`** (do not reuse `_gate`; sends must serialize independently of the subscribe/reconnect
     lock so a throttle delay never blocks the reconnect critical section in a way that deadlocks).
   - Fields to hold the active connection's interval and the last-send timestamp, set when a socket
     opens/reconnects: e.g. `private TimeSpan _minOutboundInterval;` and a monotonic
     `private long _lastSendTicks;` (use `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime` for
     monotonic timing — do NOT use `DateTime.UtcNow`). Populate `_minOutboundInterval` from
     `info.MinOutboundInterval` in **both** `OpenSocketAsync` and the reconnect connect block in
     `ReconnectCoreAsync` (right after a successful `ConnectAsync`, before pump start / replay).
   - A new `private async Task SendControlAsync(string text, CancellationToken ct)`:
     - Combine the caller token with `_disposeCts.Token` via
       `CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token)` (dispose the linked
       CTS in a `using`).
     - `await _sendSemaphore.WaitAsync(linked.Token).ConfigureAwait(false);` then `try { … } finally
       { _sendSemaphore.Release(); }`.
     - Inside the lock: if `_minOutboundInterval > TimeSpan.Zero`, compute elapsed since
       `_lastSendTicks`; if `elapsed < _minOutboundInterval`, `await Task.Delay(_minOutboundInterval -
       elapsed, linked.Token).ConfigureAwait(false);`. Then `if (_socket is not null && _socket.IsOpen)
       await _socket.SendTextAsync(text, linked.Token).ConfigureAwait(false);` and update
       `_lastSendTicks = Stopwatch.GetTimestamp();`.
     - When `_minOutboundInterval == TimeSpan.Zero`, the delay branch is skipped → behavior is
       byte-identical to today (apart from serialization, which is correct and harmless).
   - **Route every existing `_socket.SendTextAsync` call site through `SendControlAsync`**:
     - `SubscribeAsync` (the `BuildSubscribe` send, ~line 221-223).
     - `UnsubscribeAsync` (the `BuildUnsubscribe` send inside the `try`, ~line 256-257) — keep the
       existing broad-catch + `s_logUnsubFailed` around the call.
     - `ReconnectCoreAsync` replay `foreach` (~line 531-544) — keep the per-item broad-catch +
       `s_logReplayFailed`.
     - `ClientPingLoopAsync` `PingFormat.Text`/`PingFormat.Json` branch (~line 651-655) — replace the
       direct `_socket.SendTextAsync(text, ct)` with `await SendControlAsync(text, ct)`. This also
       removes the latent concurrent-`SendAsync` race (ping previously ran outside `_gate`); document
       that quirk with a one-line comment.
   - The `SubscribeAsync` path holds `_gate` across the throttle delay — this is **correct and
     intentional** (advisory risk register). Add a short XML-doc note on `SubscribeAsync` that the
     returned `Task` may be delayed up to `MinOutboundInterval` under throttling.
   - Dispose `_sendSemaphore` in `DisposeAsync` alongside `_gate.Dispose()`.

3. **`BinanceStreamProtocol.cs`** — change the cached `StreamConnectionInfo` construction to pass
   `MinOutboundInterval: TimeSpan.FromMilliseconds(200)` (5 msg/s with margin against Binance's
   5 inbound/sec limit). Add a one-line comment citing the 5/sec venue limit.

4. **`KucoinStreamProtocol.cs`** — pass `MinOutboundInterval` to the `StreamConnectionInfo` it builds
   in `ResolveConnectionAsync`. Derive it from the server-negotiated bullet data if a usable
   rate-limit field is available; otherwise use a safe default of `TimeSpan.FromMilliseconds(100)`.
   (KuCoin is more lenient than Binance; a 100 ms floor is safe.) Add a one-line comment noting the
   value source.

5. **Tests** (`StreamEngineTests.cs`, fast unit, fakes only — no network). Add a `FakeStreamProtocol`
   knob for `MinOutboundInterval` if needed (set its `StreamConnectionInfo` to carry a non-zero
   interval), or construct the engine via a protocol whose `ResolveConnectionAsync` returns a
   throttled `StreamConnectionInfo`. Capture send timestamps in `FakeWebSocketConnection`
   (`SendTextAsync` already enqueues to `SentText`; add a parallel timestamp capture or assert via a
   recording wrapper). Cover:
   - **Initial multi-subscribe spacing**: subscribe to ≥3 routing keys with `MinOutboundInterval`
     set; assert consecutive outbound control frames are spaced ≥ the interval (allow a small
     scheduler tolerance).
   - **Serialization**: no two `SendTextAsync` calls overlap (a recording fake that detects
     re-entrancy / concurrent in-flight sends asserts a max concurrency of 1).
   - **Zero preserves current behavior**: with `MinOutboundInterval == TimeSpan.Zero`, multiple
     subscribes are not artificially delayed (timing within a tight bound; frames still all sent).
   - **Dispose during throttle delay**: dispose the engine while a paced send is mid-delay; assert no
     unobserved exception escapes and `DisposeAsync` completes (the linked `_disposeCts` token
     cancels the `Task.Delay`).

Constraints: one type per file (no new top-level types expected here; `SendControlAsync` is a private
method); LEAN comments only on non-obvious venue quirks; `<inheritdoc/>` already present on impls; new
logging (if any) must use a `LoggerMessage` delegate following the existing `s_log*` pattern. Build
clean under `TreatWarningsAsErrors` + `AnalysisLevel=latest-all` (mind CA2007 ConfigureAwait, CA1031,
CA2213 dispose of `_sendSemaphore`). Strict layering: the pacing primitive lives in Http
(`StreamEngine` + `StreamConnectionInfo`); per-venue values live in each exchange protocol.

## Acceptance Criteria
- [x] `StreamConnectionInfo` carries `TimeSpan MinOutboundInterval` (default `TimeSpan.Zero`); `BinanceStreamProtocol` sets it to 200 ms and `KucoinStreamProtocol` sets it from bullet data or a safe default (100 ms — bullet response carries no rate-limit field); **zero public API change** (all three types remain `internal`; `IStreamClient` untouched). `dotnet build CryptoExchanges.Net.sln` succeeds 0W/0E.
- [x] `StreamEngine.SendControlAsync` exists, uses the dedicated `_sendSemaphore` (not `_gate`), enforces `MinOutboundInterval` via monotonic timing (`Stopwatch.GetTimestamp`/`GetElapsedTime`) + `Task.Delay`, and a linked token of caller-ct + `_disposeCts.Token`; **every** former `_socket.SendTextAsync` call site (`SubscribeAsync`, `UnsubscribeAsync`, reconnect replay `foreach`, `ClientPingLoopAsync`) now routes through it; `_sendSemaphore` is disposed in `DisposeAsync`.
- [x] New fast unit tests (no network) pass and prove: (a) initial multi-subscribe frames are spaced ≥ `MinOutboundInterval`; (b) sends are serialized (max in-flight concurrency = 1); (c) `MinOutboundInterval == Zero` preserves current un-delayed behavior; (d) dispose mid-throttle completes cleanly with no unobserved exception; plus (e) reconnect-replay path is paced. `dotnet test --filter 'Category!=Integration'` green.

## Pattern Reference
- Record-with-defaulted-positional-param + K1 `<remarks>`: `src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs:23` (the record being extended) and `HeartbeatPolicy` (same folder) for the venue-property precedent.
- `SemaphoreSlim` gate + `try/finally Release` + linked-CTS pattern: `StreamEngine.cs` `_gate` usage (e.g. `SubscribeAsync` 183-231) and `CreateLinkedTokenSource` (`StartPump` 585).
- `LoggerMessage` delegate pattern (if new logging is added): `StreamEngine.cs:43-92` (`s_log*`).
- Per-venue `StreamConnectionInfo` construction: `BinanceStreamProtocol.cs:35`, `KucoinStreamProtocol.cs:53`.
- Unit-test harness (engine + fakes, decoder registry, handlers): `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs:18-80`; fakes `FakeWebSocketConnection.cs` (SentText capture, ConnectAsync recreate-semaphore) and `FakeStreamProtocol.cs` (`ResolveConnectionAsync` returning a `StreamConnectionInfo`).

## File Scope

**Creates**:
- (none — all edits to existing files; no new top-level type)

**Modifies**:
- src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs (add `MinOutboundInterval` param + doc)
- src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs (`_sendSemaphore`, `SendControlAsync`, route all send sites, capture interval on open/reconnect, dispose semaphore)
- src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs (set 200 ms)
- src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs (set from bullet/default)
- tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs (4 new unit tests; small fake recording helper if needed)

**Deletes**:
- (none)

## Traceability
- **Objective**: config.json FEAT-008 — pacing of outbound control frames so Binance's 5 msg/s policy is respected; fix the concurrent-send race on KuCoin.
- **Advisory**: `nazgul/reviews/FEAT-008/architect-reviewer.md` §"Step 1 — Throttling (correctness fix, no interface changes)" + risk register.
- **Constraints**: CLAUDE.md (one type per file, LEAN comments, `<inheritdoc/>`), TreatWarningsAsErrors/AnalysisLevel=latest-all, strict Core→Http→Exchange layering.

## Commits

- `2d2a3aaa271cd2b4490962323afb9f2fe103593a` — feat(FEAT-008): throttle + serialize outbound control frames in StreamEngine
- `a45059f6b5b4595a670e9a775fe5f77e1ad5a16f` — feat(FEAT-008): post-review simplify (per-task simplifier — 5 safe fixes; build 0/0, tests green)

## Implementation Log

- Base SHA: `ac49fca0717de18899a918d9b25b91212a38fadc` (branch `feat/FEAT-008-stream-control-msg-rate-limit`).
- `StreamConnectionInfo.cs`: added `TimeSpan MinOutboundInterval = default` positional record param
  with `<param>` doc (venue property, like `HeartbeatPolicy`; `Zero` = unthrottled); updated the K1
  `<remarks>` to note it still carries only venue/transport policy data.
- `StreamEngine.cs`:
  - Added `private readonly SemaphoreSlim _sendSemaphore = new(1, 1)` (separate from `_gate`),
    `private TimeSpan _minOutboundInterval`, `private long _lastSendTicks`.
  - Added `SendControlAsync(string text, CancellationToken ct)`: linked CTS (caller ct + `_disposeCts`),
    `_sendSemaphore` WaitAsync/try/finally Release, monotonic pacing via `Stopwatch.GetElapsedTime` +
    `Task.Delay` (skipped when interval is `Zero`), sends only if socket open, updates `_lastSendTicks`.
    Guards `text` per LR-001 (`ArgumentException.ThrowIfNullOrWhiteSpace`).
  - Added `ApplyConnectionPacing(info)` called in `OpenSocketAsync` and the reconnect connect block to
    capture the interval and reset the last-send clock (first frame never delayed).
  - Routed all four send sites through `SendControlAsync`: `SubscribeAsync`, `UnsubscribeAsync`,
    reconnect-replay `foreach`, `ClientPingLoopAsync` (Text/Json branch) — the ping route also removes
    the latent concurrent-`SendAsync` race (documented inline).
  - Added an XML-doc `<remarks>` on `SubscribeAsync` noting the task may be delayed up to one interval.
  - Disposed `_sendSemaphore` in `DisposeAsync`.
- `BinanceStreamProtocol.cs`: `MinOutboundInterval: 200 ms` (5 msg/s with margin; comment cites the cap).
- `KucoinStreamProtocol.cs`: `MinOutboundInterval: 100 ms` safe default — verified `BulletPublicDto`/
  `InstanceServerDto` carry only Token/Endpoint/PingInterval/PingTimeout (no rate-limit field), comment notes source.
- Tests (`StreamEngineTests.cs`): added a `RecordingWebSocketConnection` fake (records send-start
  timestamps + max in-flight concurrency, optional `SendDuration` to widen the overlap window) and a
  `BuildEngineWith` helper. Added `MinOutboundInterval` knob to `FakeStreamProtocol`. Five new `[Fact]`s
  (LR-005 coverage; LR-010: no dead vars / self-evident remarks).
- Build: `dotnet build CryptoExchanges.Net.sln` → 0 Warning(s) / 0 Error(s).
- Tests: `dotnet test --filter 'Category!=Integration'` → all projects green; Http unit project 92 passed
  (was 87, +5 new). No regressions across the suite.

## Review Results

- **Gate decision**: ✦ APPROVED (require_all_approve satisfied — all 4 reviewers APPROVED). 2026-06-23.
- **Pre-checks**: test (197 passed / 0 failed) ✦, lint (0W/0E) ✦, build (0W/0E) ✦, smoke (not configured — skipped).
- **Simplify pass (Step 0)**: 5 safe fixes applied (removed redundant `volatile` on `_livenessFlag`; hoisted `Encoding.UTF8.GetString` out of the ping loop; removed duplicate `Interlocked.Exchange`; stripped a stale task-id banner reference; fixed `RecordingWebSocketConnection.DisposeAsync` to release the semaphore). Build 0/0, tests green. Squash commit `a45059f6`.
- **architect-reviewer**: ✦ APPROVED — layering intact (pacing primitive in Http, per-venue values in protocols); advisory Step-1 conformance verified; no public API change; no deadlock (no `_sendSemaphore`→`_gate` ordering). Non-blocking CONCERNs: memory-visibility comment suggestion (conf 55); Step-2 batching deferred (conf 70, → TASK-072, already planned). See `nazgul/reviews/TASK-071/architect-reviewer.md`.
- **code-reviewer**: ✦ APPROVED — concurrency/dispose correctness confirmed; LR-001 guard present; LR-005 (5 tests) and LR-010 satisfied; CA2007/CA2213 clean. One non-blocking CONCERN (conf 72): `SubscribeAsync` `<remarks>` borderline-LEAN but describes caller-observable behavior. See `nazgul/reviews/TASK-071/code-reviewer.md`.
- **security-reviewer**: ✦ APPROVED — no credential/signing path touched; no secret leakage; linked-CTS disposed per call (no leak); bounded waiters; dispose-mid-delay cancels cleanly. Non-blocking CONCERNs only (conf ≤ 60). See `nazgul/reviews/TASK-071/security-reviewer.md`.
- **api-reviewer**: ✦ APPROVED — all touched types `internal`; public surface byte-identical; additive defaulted record param (TimeSpan.Zero = unthrottled); no SemVer/NuGet impact; LR-004 N/A. See `nazgul/reviews/TASK-071/api-reviewer.md`.
- **Rule citations bumped**: LR-001, LR-004, LR-005, LR-010.
