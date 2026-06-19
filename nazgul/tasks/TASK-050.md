---
id: TASK-050
status: DONE
commit: 00b894b
claimed_at: 2026-06-19
---

# TASK-050: Address PR #26 review findings (streaming hardening)

**Status**: IMPLEMENTED

**Blast radius**: LOW–MEDIUM — Http transport hardening + Binance parse perf + DI guard + test hygiene. No public API change.

## Base SHA
- **Base SHA**: 5195052

## Findings fixed

1. **Max inbound message-size bound** — Added `const int MaxMessageBytes = 4 * 1024 * 1024`
   to `ClientWebSocketConnection`. Before each append in `ReceiveAsync`, checks
   `ms.Length + result.Count > MaxMessageBytes` → throws `InvalidOperationException`.
   Tests: reflection const check + URI scheme guard tests exercise the same class.

2. **Control-frame Ping/Pong correctness** — `ClientWebSocket.SendAsync` cannot emit
   RFC 6455 control frames. Fixed:
   - `ClientWebSocketConnection` constructor sets `_ws.Options.KeepAliveInterval`
     (default 20s) so framework handles control-frame keep-alive automatically.
   - `SendPingAsync`/`SendPongAsync` send binary data frames (correct for Text/Json venues).
     XML docs updated to state the .NET limitation.
   - `StreamEngine.ClientPingLoopAsync`: `PingFormat.ControlFrame` → no manual send (break
     only; framework keep-alive handles it). Text/Json → still sends data-frame payload.
   - Adjusted test `Engine_HeartbeatClientPing_ControlFrame_SendsPingNotPong` →
     `Engine_HeartbeatClientPing_ControlFrame_NoManualSend` asserting no SentPings/SentPongs
     and only the subscribe text in SentText.

3. **Span parsing** — `BinanceStreamProtocol.Classify`: replaced `JsonDocument.Parse(frame.ToArray())`
   with `var reader = new Utf8JsonReader(frame); using var doc = JsonDocument.ParseValue(ref reader)`.
   No intermediate array allocation on the hot path. Corrected the misleading comment.

4. **DI disposal guard** — `StreamServiceRegistration` keyed-singleton factory wraps
   `StreamClient` construction in try/catch; on failure calls `engine.DisposeAsync().AsTask().GetAwaiter().GetResult()`
   to prevent resource leak, mirroring `StreamClientFactory.Create`.

5. **Test hygiene** — `StreamContractTests`: `await using` → `using` for all
   `FakeWebSocketConnection` instances (sync `Dispose()` is the correct path).
   Added `ArgumentNullException.ThrowIfNull(uri)` to `FakeWebSocketConnection.ConnectAsync`.
   Added test `Fake_ConnectAsync_NullUri_Throws`.

6. **Cheap** — `ClientWebSocketConnection.ConnectAsync`: guards `uri.Scheme` is `ws`/`wss`
   else `ArgumentException`. `SendPingAsync`/`SendPongAsync` return `Task` directly
   (`.AsTask()` on the `ValueTask` from `ClientWebSocket.SendAsync` — no `async`/`await`
   state machine). New tests: `ClientWebSocketConnection_ConnectAsync_HttpScheme_Throws`,
   `ClientWebSocketConnection_ConnectAsync_HttpsScheme_Throws`,
   `ClientWebSocketConnection_ConnectAsync_WssScheme_DoesNotThrowArgumentException`,
   `ClientWebSocketConnection_MaxMessageBytes_Is4Mib`.

## Acceptance

- Build: 0W/0E ✦
- Tests: 572 non-integration tests pass (83 in Http.Tests.Unit)
- New tests: message-size bound const, URI scheme guard (2 throw cases + 1 pass case),
  ControlFrame no-send path, fake null-uri guard
- K1 clean: no new Core.Models/DeltaMapper refs in Http engine/transport layer
- C1: heartbeat timing remains engine-only; ControlFrame path now framework-delegated

## Commits

- `00b894b` — feat(FEAT-005): address PR #26 review findings — streaming hardening (TASK-050)
