# PATCH-004: Await fire-and-forget reconnect task on dispose (CI follow-up to PATCH-003)

## Metadata
- **Status**: DONE
- **Created**: 2026-06-24T10:58:58Z
- **Source**: /nazgul:patch
- **Flags**: none

## Description
CI follow-up to PATCH-003 (branch patch/PATCH-003-reconnect-subscribe-race): the new test
`Engine_DisposeDuringReconnectBackoff_ReleasesGateExactlyOnce_NoOverRelease` fails on the
(slower) CI runner — it catches an unobserved `System.ObjectDisposedException`, NOT a
`SemaphoreFullException` (the gate holdsGate logic is correct). Root cause:
`StreamEngine.DisposeAsync` cancels `_disposeCts` and then disposes
`_gate`/`_disposeCts`/`_sendSemaphore` WITHOUT awaiting the fire-and-forget `ReconnectAsync`
background task (started via `_ = Task.Run(() => ReconnectAsync(), _disposeCts.Token)` at the
pump-error, venue-close, and watchdog sites). When dispose races the reconnect's inter-attempt
backoff window, the reconnect's re-acquire `await _gate.WaitAsync(_disposeCts.Token)`
(ReconnectCoreAsync, NOT inside the OCE catch around Task.Delay) runs against an
already-disposed SemaphoreSlim/CancellationTokenSource and throws ObjectDisposedException,
faulting the untracked Task.Run task → unobserved exception. Real latent dispose-race
(pre-existing; the new control flow made it observable on CI).

Fix (all internal, zero public API change):
1. In `ReconnectAsync`, make the fire-and-forget task dispose-safe: swallow a dispose-induced
   `OperationCanceledException` or `ObjectDisposedException` when `_isDisposed` is true (exception
   filters `when (_isDisposed)`; never reference `_disposeCts.IsCancellationRequested` in the
   filter — accessing a disposed CTS throws). Keep the existing
   `finally { Interlocked.Exchange(ref _reconnecting, 0); }`.
2. Track the reconnect task in a new field `private Task? _reconnectTask;` assigned at all three
   fire sites; in `DisposeAsync`, after `await _disposeCts.CancelAsync()` and BEFORE disposing
   `_gate`/`_sendSemaphore`/`_disposeCts`, await the captured `_reconnectTask` inside a
   try/catch that swallows exceptions, so primitives and the socket are never disposed while an
   in-flight reconnect still uses them.

## Subtasks
1. Add `_reconnectTask` field; make `ReconnectAsync` dispose-safe (swallow OCE/ODE when
   `_isDisposed`); assign `_reconnectTask` at the three fire sites; await it in `DisposeAsync`
   before disposing primitives.

## Implementation Log
- Added `private Task? _reconnectTask;` field next to the reconnect-backoff state.
- `ReconnectAsync` now swallows dispose-induced `OperationCanceledException`/`ObjectDisposedException`
  via `when (_isDisposed)` filters (no `_disposeCts` access in the filter); existing
  `finally { Interlocked.Exchange(ref _reconnecting, 0); }` retained.
- All three fire sites (pump receive-error, venue-close, watchdog) now assign
  `_reconnectTask = Task.Run(() => ReconnectAsync(), _disposeCts.Token)` instead of discarding.
- `DisposeAsync` awaits the captured `_reconnectTask` (best-effort try/catch) right after
  `_disposeCts.CancelAsync()` and before disposing `_gate`/`_sendSemaphore`/`_disposeCts`.
- Verified: build clean (0 warnings, Release). `dotnet test tests/CryptoExchanges.Net.Http.Tests.Unit
  -c Release` run 5x → 98/98 pass each time, no unobserved exception.
- Review (code-reviewer): APPROVED. Seven findings, six PASS + one non-blocking CONCERN
  (`_reconnectTask` plain non-volatile field read cross-thread; functionally correct via the
  implicit CTS lock fence and consistent with `_pumpTask`/`_heartbeatTask`/`_idleCloseTask` —
  no action required). Artifact: nazgul/reviews/PATCH-004/code-reviewer.md.
