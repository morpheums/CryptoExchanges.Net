# Architect Review — FEAT-008 Advisory (Pre-Implementation)

> **Note**: This is a pre-implementation architectural advisory, not a task-based code review.
> No TASK-ID has been assigned. No diff exists. This file satisfies the stop-hook file requirement
> while clearly marking the distinction from a normal task review.

## Final Verdict
APPROVED

*Approved in the advisory sense: the recommended design is sound and the planner should proceed
with decomposing FEAT-008 along the lines described below.*

---

## Advisory Summary

### Bug confirmed
`StreamEngine` sends one control frame per subscription with no pacing. Subscribing to N symbols
fires an N-frame burst. Binance enforces 5 inbound msgs/sec and closes the socket (~300 ms) with
PolicyViolation. `ReconnectCoreAsync` replays the entire `_subscribeSet` as another burst on every
reconnect, producing an infinite reconnect loop with zero data delivered.

### Recommended approach: throttling as the base fix + batching for reconnect replay

**Do not choose one or the other.** They solve different problems and compose cleanly.

#### Step 1 — Throttling (correctness fix, no interface changes)

- Add `TimeSpan MinOutboundInterval` to `StreamConnectionInfo` (default = zero = no limit).
- `BinanceStreamProtocol.ResolveConnectionAsync` sets it to `TimeSpan.FromMilliseconds(200)` (5 msg/s with margin).
- `KucoinStreamProtocol.ResolveConnectionAsync` sets it from server-negotiated data or a safe default.
- Add a private `SendControlAsync(string text, CancellationToken ct)` to `StreamEngine`.  
  - Uses a dedicated `SemaphoreSlim(1,1)` `_sendSemaphore` (separate from `_gate`) for send serialisation.
  - Enforces `MinOutboundInterval` via `Task.Delay` before each send.
  - Respects a linked token combining caller `ct` and `_disposeCts.Token`.
- Every `_socket.SendTextAsync` call site routes through `SendControlAsync`: `SubscribeAsync`,
  `UnsubscribeAsync`, the reconnect-replay `foreach`, and `ClientPingLoopAsync`.
- This also fixes the existing concurrent-send hazard: heartbeat `SendTextAsync` in
  `ClientPingLoopAsync` currently runs outside `_gate` and races with subscribe on KuCoin.
- Public surface change: **zero**.

#### Step 2 — Batching (reconnect-replay optimisation, additive protocol change)

- Add `string? BuildSubscribeBatch(IReadOnlyList<StreamRequest> requests)` to `IStreamProtocol`
  as a default interface member returning `null` (non-breaking; `IStreamProtocol` is `internal`).
- Add `string? BuildUnsubscribeBatch(IReadOnlyList<StreamRequest> requests)` likewise.
- `BinanceStreamProtocol` implements: multi-param array (`"params":["a@ticker","b@ticker",...]`),
  chunked at 100 tokens per frame.
- `KucoinStreamProtocol` implements: comma-joined topic (`"topic":"/market/snapshot:BTC-USDT,ETH-USDT,..."`),
  chunked at 100 symbols per frame.
- `ReconnectCoreAsync` calls `BuildSubscribeBatch` first; falls back to throttled per-frame loop if null.
- Result: 300-symbol reconnect = 3 frames × 200 ms = ~600 ms instead of 60 s.
- Public surface change: **zero**.

### Where the rate-limit value lives

`StreamConnectionInfo.MinOutboundInterval` — NOT `StreamEngineOptions`. The limit is a venue
property (like `HeartbeatPolicy`), not a consumer preference. Putting it in `StreamEngineOptions`
would let consumers accidentally break exchange policy. The protocol sets it; the engine enforces it.
This is identical to the established `HeartbeatPolicy` precedent.

### Risk register

| Risk | Severity | Mitigation |
|---|---|---|
| Concurrent `ClientWebSocket.SendAsync` (heartbeat vs subscribe on KuCoin) | HIGH | `_sendSemaphore` in `SendControlAsync` serialises all sends |
| Reconnect-replay burst (300 symbols × 200 ms = 60 s under throttle-only) | HIGH | Batching in Step 2 reduces to ~600 ms |
| `SubscribeAsync` holds `_gate` across throttle delay | MEDIUM | Correct and intentional; document in XML doc |
| Dispose during throttle delay | MEDIUM | Linked `CancellationToken` with `_disposeCts.Token` |
| `IStreamProtocol` test fakes must implement new method | LOW | Default-null implementation; fakes need no change unless testing batch behaviour |
| 300-symbol consumer stalls ~60 s if batching is deferred | MEDIUM | Do not defer; treat batched-replay as required in FEAT-008 |

### Minimal acceptable public API surface

**Zero changes to public API.** `IStreamProtocol`, `StreamConnectionInfo`, and `StreamEngineOptions`
are all `internal`. `IStreamClient` (public, in Core) does not change. `SubscribeAsync` already
returns `Task<IStreamSubscription>` asynchronously; the throttle delay is absorbed there correctly.

