---
id: TASK-050
status: IN_PROGRESS
commit:
claimed_at: 2026-06-19
---

# TASK-050: Address PR #26 review findings (streaming hardening)

**Status**: READY

**Blast radius**: LOW–MEDIUM — Http transport hardening + Binance parse perf + DI guard + test hygiene. No public API change.

## Findings to fix (from Copilot + CodeRabbit on PR #26)
1. **Max inbound message-size bound** — `ClientWebSocketConnection.ReceiveAsync` appends fragments to an unbounded `MemoryStream`; cap at a few MiB (const) and fail fast.
2. **Control-frame Ping/Pong correctness** — .NET `ClientWebSocket.SendAsync` sends only data frames, not RFC 6455 control frames. For `PingFormat.ControlFrame`/`ServerPingClientPong`, rely on `ClientWebSocket.Options.KeepAliveInterval` (framework keep-alive) rather than manual control-frame sends; make `SendPing/SendPongAsync` honest (Text/Json client-ping → data frame is correct; ControlFrame → framework keep-alive + liveness-on-any-frame). Don't have the engine manually "pong" control frames.
3. **Span parsing** — `BinanceStreamProtocol` calls `frame.ToArray()` (alloc/frame on hot path) while the comment claims span-based parsing; parse via `Utf8JsonReader`/`JsonDocument.Parse` over the span and correct the comment.
4. **DI disposal guard** — keyed-singleton factory in `StreamServiceRegistration` creates `StreamEngine` then `StreamClient` with no dispose-on-failure guard (the container-free `Create` path has one); mirror it.
5. **Test hygiene** — `StreamContractTests` use `await using` but the fake's final cleanup is in sync `Dispose()`; switch to `using`. Add `ArgumentNullException.ThrowIfNull(uri)` to the fake's `ConnectAsync`.
6. **Cheap** — URI scheme guard (ws/wss) in `ClientWebSocketConnection.ConnectAsync`; return `Task` directly from `SendPing/SendPongAsync` (drop async state machine).

## Acceptance
- Build 0W/0E; full non-integration suite green; new tests for the message-size bound + URI scheme guard; K1/C1/K3 hold; no competitor names.
