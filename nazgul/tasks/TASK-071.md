---
id: TASK-071
status: IN_PROGRESS
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
- **Implemented at**:
- **Completed at**:
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
- [ ] `StreamConnectionInfo` carries `TimeSpan MinOutboundInterval` (default `TimeSpan.Zero`); `BinanceStreamProtocol` sets it to 200 ms and `KucoinStreamProtocol` sets it from bullet data or a safe default; **zero public API change** (all three types remain `internal`; `IStreamClient` untouched). `dotnet build CryptoExchanges.Net.sln` succeeds 0W/0E.
- [ ] `StreamEngine.SendControlAsync` exists, uses the dedicated `_sendSemaphore` (not `_gate`), enforces `MinOutboundInterval` via monotonic timing + `Task.Delay`, and a linked token of caller-ct + `_disposeCts.Token`; **every** former `_socket.SendTextAsync` call site (`SubscribeAsync`, `UnsubscribeAsync`, reconnect replay `foreach`, `ClientPingLoopAsync`) now routes through it; `_sendSemaphore` is disposed in `DisposeAsync`.
- [ ] New fast unit tests (no network) pass and prove: (a) initial multi-subscribe frames are spaced ≥ `MinOutboundInterval`; (b) sends are serialized (max in-flight concurrency = 1); (c) `MinOutboundInterval == Zero` preserves current un-delayed behavior; (d) dispose mid-throttle completes cleanly with no unobserved exception. `dotnet test --filter 'Category!=Integration'` green.

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

- (pending)

## Implementation Log

- (pending)

## Review Results

- (pending)
