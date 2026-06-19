---
id: TASK-044
status: IN_PROGRESS
commit: 75ec4e8
depends_on: [TASK-043]
---
# TASK-044: Http reconnecting byte-engine (pump / route / backoff / replay / heartbeat / channels)

## Metadata
- **ID**: TASK-044
- **Group**: 3
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-043
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs, src/CryptoExchanges.Net.Http/Streaming/StreamSubscriptionChannel.cs, src/CryptoExchanges.Net.Http/Streaming/BackoffSchedule.cs, src/CryptoExchanges.Net.Http/Streaming/StreamEngineOptions.cs, tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs, tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeWebSocketConnection.cs]
- **Wave**: 3
- **Traces to**: FEAT-005 spec §Architecture "Delivery"/"Connection" + C1/K2/K3; design §"Delivery model" + §"Connection & disposal (R5)"; DESIGN-STREAMING-V1 R4/R5; DECISION-STREAMING-SHARED §1 (Http byte-engine) + §6 (K1/K2/K3, C1)
- **Created at**: 2026-06-19T17:20:00Z
- **Claimed at**: 2026-06-19T20:00:00Z
- **Base SHA**: c18c48d0fdd53bd3f6934484ad028e9cff61637a
- **Implemented at**: 2026-06-19T21:00:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 1/3
- **Test failures**: 1

## Description

Build the exchange-agnostic reconnecting **byte-engine** — the heart of streaming. Consumes the
TASK-043 contracts (`IStreamProtocol`, `IWebSocketConnection`, `HeartbeatPolicy`, `StreamFrame`,
`StreamRequest`) + the TASK-042 `StreamLag`/`StreamConnectionState`. All `internal sealed`, under
`src/CryptoExchanges.Net.Http/Streaming/`. One type per file. Tested entirely via the fake transport
from TASK-043 — NO network.

**K1**: the engine handles `byte` / `object` / opaque `Func<ReadOnlyMemory<byte>, object>` only — NO
`Core.Models`, NO DeltaMapper. The engine carries decode closures as opaque delegates; it never knows `T`.

Engine responsibilities (design §"Delivery model" + §"Connection & disposal"):

- **Connect / lazy-open**: open one `IWebSocketConnection` on first subscribe; keep-warm while ≥1 active
  subscription; **idle-close after a configurable window** (NOT close-on-last-sub — avoids reconnect thrash).
- **Single receive-pump per socket**: read frames, `protocol.Classify(frame)` → act on `Kind`
  (skip `Ack`/`Pong`, surface `Error` via log + appropriate handling, route `Data` by `RoutingKey`).
  The pump must NEVER die on a callback or decode exception (catch + log via `ILogger`).
- **Per-subscription bounded channel** (`StreamSubscriptionChannel`): single-reader/single-writer,
  `FullMode = DropOldest`. A per-subscription consumer task awaits the channel and invokes the delivery
  callback; **per-subscription FIFO** preserved. On DropOldest eviction, track per-subscription
  dropped-count and raise `OnLagged(new StreamLag(n))` (R4) — never injected into the data stream, never thrown.
- **Heartbeat EXECUTION (C1)**: drive timers / liveness watchdog / send / pong from `HeartbeatPolicy`
  *data*. `ServerPingClientPong` → engine pongs the control frame; `ClientPing` → engine sends
  `ClientPingPayload` per `PingFormat` at `Interval`; `Timeout` exceeded without liveness → forced reconnect.
  The protocol owns no timing — the engine does.
- **Reconnect = engine backoff (K3)**: own bounded backoff loop (`BackoffSchedule` — exponential + cap +
  jitter), NOT the REST Polly pipeline. On reconnect: transition affected subscriptions
  `Live → Reconnecting → Live`, run lifecycle callbacks through the same isolation wrapper, and
  **replay the stored subscribe set (K2)** — re-send `BuildSubscribe` for every still-live subscription.
  `BuildUnsubscribe` removes from the replay set so a dead stream is not resurrected. The bounded channel
  is **NOT** torn down across reconnect (consumer resumes; no gap-fill guarantee in v1).
- **`StreamEngineOptions`**: idle-close window, per-subscription channel capacity, backoff bounds,
  per-socket subscription cap (shard trigger). Validatable.

Lifecycle/state surfaced so the subscription handle (TASK-045) can expose `State`.

Tests (fake transport, no network): routing by `RoutingKey`; skip Ack/Pong; surface Error;
reconnect + resubscribe replays the stored set (K2) and respects unsubscribe-removal; backpressure
DropOldest raises `OnLagged` with correct dropped-count; callback exception does NOT kill the pump;
lifecycle-state transitions `Connecting → Live → Reconnecting → Live → Closed`; idle-close after window;
heartbeat send/pong driven by each `HeartbeatPolicy` variant; backoff schedule bounds.

## Acceptance Criteria
- [x] Engine implements pump+`Classify`-route, per-subscription `DropOldest` channel + consumer + FIFO + `OnLagged`, isolated/logged callback exceptions, lazy-open + keep-warm + idle-close, engine-backoff reconnect (K3) with subscribe-set replay (K2), and heartbeat execution driven by `HeartbeatPolicy` (C1) — no timers/threads in the protocol.
- [x] `StreamEngineTests` cover routing, reconnect+resubscribe (incl. unsubscribe-removal), backpressure/`OnLagged`, pump-survives-callback-exception, and lifecycle-state transitions — all via the fake transport with NO network; solution builds 0W/0E.
- [x] **K1 verified**: zero `Core.Models`/DeltaMapper references under `src/CryptoExchanges.Net.Http/`; reconnect does NOT route through `ExchangeResiliencePipeline` (K3); existing 499 tests stay green.

## Pattern Reference
- Engine behavior spec: design §"Delivery model" (lines 92-97) + §"Connection & disposal (R5)" (lines 99-103); binding C1/K1/K2/K3 (lines 124-128).
- Contracts to consume: `src/CryptoExchanges.Net.Http/Streaming/{IStreamProtocol,IWebSocketConnection,HeartbeatPolicy,StreamFrame,StreamRequest}.cs` (TASK-043).
- Internal-sealed + no-Core.Models discipline: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs`; resilience-pipeline boundary to stay clear of: `src/CryptoExchanges.Net.Http/ExchangeResiliencePipeline.cs` (reconnect must NOT use it — K3).
- Fake transport to test against: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeWebSocketConnection.cs` (TASK-043).

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs
- src/CryptoExchanges.Net.Http/Streaming/StreamSubscriptionChannel.cs
- src/CryptoExchanges.Net.Http/Streaming/BackoffSchedule.cs
- src/CryptoExchanges.Net.Http/Streaming/StreamEngineOptions.cs
- tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineTests.cs

**Modifies**:
- tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeWebSocketConnection.cs

## Traceability
- **PRD Acceptance Criteria**: n/a — FEAT-005 spec Success criterion "auto-reconnect + auto-resubscribe verified" + "new unit tests via an injected fake transport"
- **TRD Component**: Http reconnecting byte-engine (design §"Delivery model", §"Connection & disposal")
- **ADR Reference**: DESIGN-STREAMING-V1 R4 (`OnLagged`), R5 (lazy-open/keep-warm/idle-close, backoff-replay, channel-survives-reconnect); DECISION-STREAMING-SHARED §6 C1/K1/K2/K3; Inv 2/8/9

## Implementation Log

### Attempt 1
Initial implementation (commit 2fb3895). One pre-check test failure: `Engine_Reconnect_AutoResubscribes_StoredSubscribeSet` — FakeWebSocketConnection.DisposeAsync disposed _available semaphore; reconnect loop couldn't complete.

### Attempt 2
Fixed (commit 75ec4e8): FakeWebSocketConnection.ConnectAsync now swaps in a fresh SemaphoreSlim and drains stale frames, making the fake resilient to reuse across reconnect cycles. `Engine_Unsubscribe_RemovesFromReplaySet_NotResurrectedOnReconnect` also fixed to wait for the K2 replay send before snapshotting SentText (ConnectCount >= 2 does not guarantee replay has committed). All 70 Http unit tests pass + 546 total suite passes. Build 0W/0E.

## Review Results

### Attempt 1
Pre-check FAIL — test failure in reconnect tests (see Implementation Log Attempt 1).

### Attempt 2
Pre-checks PASS — proceeding to review board.
