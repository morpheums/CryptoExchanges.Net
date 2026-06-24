# PATCH-003: Fix reconnect-vs-subscribe socket-disposal race in StreamEngine

## Metadata
- **Status**: DONE
- **Created**: 2026-06-24T10:14:39Z
- **Source**: /nazgul:patch
- **Flags**: none

## Description
Fix the reconnect-vs-subscribe socket-disposal race in
`CryptoExchanges.Net.Http.Streaming.StreamEngine`. In `ReconnectCoreAsync` the gate
(`_gate`) is released (after transitioning subscriptions to Reconnecting) BEFORE the dead
socket is disposed and before the new socket is created/connected — so the `_socket` field
teardown/reassignment runs OUTSIDE the gate, racing a concurrent gate-holding
`SubscribeAsync`→`OpenSocketAsync`. When a Binance multi-symbol order-book subscribe burst
trips an early venue close, the reconnect disposes the `_socket` field while a concurrent
open is mid-`ConnectAsync` (or orphans a concurrently-opened socket), surfacing as
`System.ObjectDisposedException 'System.Net.WebSockets.ClientWebSocket'` from
`OpenSocketAsync`→`ConnectAsync`.

Fix:
1. Hold `_gate` across the socket teardown and each reconnect connect-attempt in
   `ReconnectCoreAsync`, releasing it ONLY during the inter-attempt backoff `Task.Delay`;
   extract a `TeardownSocketAsync` helper that cancels+awaits the pump BEFORE disposing the
   socket; make `CloseAllSubscriptions` gate-free since it is now called under the gate.
2. In `SubscribeAsync`, when a reconnect is in progress (`_reconnecting==1`), register the
   subscription in `_subscriptions`/`_subscribeSet` and return a handle in Reconnecting
   state WITHOUT independently opening a socket or sending — the reconnect's K2 replay sends
   it on the new socket.

Add a deterministic regression test asserting no orphaned/second live socket and full
subscribe-set replay after a subscribe arrives mid-reconnect. Zero public API change — all
touched types are internal.

## Subtasks
1. Refactor `ReconnectCoreAsync` to hold `_gate` across teardown + connect attempts,
   releasing only during the backoff delay; extract `TeardownSocketAsync`; make
   `CloseAllSubscriptionsAsync` gate-free (caller holds the gate).
2. Make `SubscribeAsync` reconnect-aware: when `_reconnecting==1`, register subscription and
   return a Reconnecting-state handle without opening a socket or sending.
3. Add a deterministic regression test (single-use connection that throws
   ObjectDisposedException after dispose + recording factory) asserting no orphaned/second
   live socket and full subscribe-set replay when a subscribe arrives mid-reconnect.

## Implementation Log
- Subtask 1: `ReconnectCoreAsync` now holds `_gate` across teardown + each connect attempt,
  releasing it only during the inter-attempt backoff `Task.Delay`. Extracted
  `TeardownSocketAsync` (cancels + awaits the pump BEFORE disposing the socket). Made
  `CloseAllSubscriptionsAsync` gate-free (caller holds the gate).
- Subtask 2: `SubscribeAsync` is reconnect-aware: when `_reconnecting==1` it registers the
  subscription in `_subscriptions`/`_subscribeSet` and returns a Reconnecting-state handle
  without opening a socket or sending; the reconnect's K2 replay sends it on the new socket.
- Subtask 3: Added `Engine_SubscribeMidReconnect_NoOrphanedSocket_FullReplayOnNewSocket`
  regression test with a faithful single-use connection (throws ObjectDisposedException after
  dispose) + recording factory (tracks max simultaneous live sockets). Verified the test FAILS
  against baseline (mid-reconnect subscribe went Live by opening its own socket) and PASSES with
  the fix. Full solution suite green.
- Review (code-reviewer): Round 1 CHANGES_REQUESTED — caught a gate double-release on
  dispose-during-backoff (HIGH) plus a Reconnecting-handle stuck window (MEDIUM) and a CA1308
  pragma-span nit (LOW). All addressed: (1) `holdsGate` flag makes `_gate` release exactly once
  on every path (independently caught + fixed before review, with a dedicated regression test
  `Engine_DisposeDuringReconnectBackoff_ReleasesGateExactlyOnce_NoOverRelease` verified to catch
  the SemaphoreFullException); (2) `Interlocked.Exchange(ref _reconnecting, 0)` moved inside the
  gate before the K2 replay + Live broadcast; (3) CA1308 pragma tightened. Round 2: APPROVED.
- Final: 98/98 Http unit tests pass; full solution suite green; build clean (0 warnings,
  TreatWarningsAsErrors). Review artifact: nazgul/reviews/PATCH-003/code-reviewer.md.

