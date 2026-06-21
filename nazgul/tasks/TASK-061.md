---
id: TASK-061
status: IMPLEMENTED
depends_on: []
---
# TASK-061: Generalize streaming endpoint seam (ADR-002) — async `ResolveConnectionAsync` + migrate Binance

## Metadata
- **ID**: TASK-061
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: none
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs, src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs, src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs, src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs, tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineResolveConnectionTests.cs]
- **Wave**: 1
- **Traces to**: PRD-FEAT-006 AC-4, AC-5; TRD-FEAT-006 §"Work Stream A — Streaming Seam Generalization", §"Dependency Impact"; ADR-002 (full); FEAT-006 spec §"Streaming endpoint-seam generalization", §"Build approach" step 6; TEST-PLAN-FEAT-006 §8 + §"Regression Coverage"
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**: 2026-06-21T00:00:00Z
- **Base SHA**: 8837e79d6fe76bdb30f944dcfce9c56fcfb6ae4a
- **Implemented at**: 2026-06-21T00:00:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Land the one shared-engine change ahead of the KuCoin stream protocol: generalize `IStreamProtocol`'s
static `Uri Endpoint` + `HeartbeatPolicy Heartbeat` properties into a single async per-connection
resolution seam, per ADR-002, with **zero Binance behavior change**. This is a breaking change to an
`internal` interface only (no external implementors); the engine gains one `await` per connect/reconnect.

**Constraint K1 (hard REJECT line)**: `StreamConnectionInfo` carries ONLY `Uri` + `HeartbeatPolicy` —
NO `Core.Models`, NO DeltaMapper reference under `src/CryptoExchanges.Net.Http/`. The seam stays
byte/opaque. **Constraint C1**: the protocol *describes* heartbeat (via the returned record); the engine
*executes* it — no timer/thread enters the protocol.

Create:
- **`src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs`** —
  `internal sealed record StreamConnectionInfo(Uri Endpoint, HeartbeatPolicy Heartbeat);` with full XML docs.

Modify:
- **`IStreamProtocol.cs`** — remove `Uri Endpoint { get; }` and `HeartbeatPolicy Heartbeat { get; }`;
  add `ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct);` with full XML
  docs (note: invoked before every connect AND every reconnect; may perform IO; C1 still holds).
- **`StreamEngine.cs`** — replace the two static property reads with `await
  _protocol.ResolveConnectionAsync(ct)` at both call sites (`OpenSocketAsync` ~line 286 and
  `ReconnectCoreAsync` ~line 510). The heartbeat policy is now read from the resolved info on every
  reconnect. K2/K3 reconnect/backoff/replay behavior unchanged; propagate
  `OperationCanceledException` from resolve.
- **`BinanceStreamProtocol.cs`** — implement `ResolveConnectionAsync` returning a `StreamConnectionInfo`
  precomputed ONCE in the constructor from `BinanceStreamOptions` (cached field; return
  `ValueTask.FromResult`/`new ValueTask<...>(cached)`); drop the now-unused `Endpoint` and `Heartbeat`
  properties. Behavior identical.

Tests — extend `CryptoExchanges.Net.Http.Tests.Unit` with a fake `IStreamProtocol`
(`StreamEngineResolveConnectionTests.cs`), no network:
- `ResolveConnectionAsync` called once per `OpenSocketAsync` (first connect).
- `ResolveConnectionAsync` called on EVERY reconnect attempt (not cached from first connect).
- `BinanceStreamProtocol.ResolveConnectionAsync` returns the cached info (identical reference across
  calls).
- engine propagates/aborts when `ResolveConnectionAsync` throws `OperationCanceledException`.

Existing `Http.Tests.Unit` streaming tests + Binance streaming regression must pass unchanged.

## Acceptance Criteria
- [ ] `IStreamProtocol` drops `Endpoint`/`Heartbeat` properties and exposes `ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct)`; new `internal sealed record StreamConnectionInfo(Uri, HeartbeatPolicy)`; `StreamEngine` awaits it at both connect + reconnect call sites; full XML docs. K1 preserved (no Core.Models/DeltaMapper under Http) and C1 preserved (no timer/thread in protocol).
- [ ] `BinanceStreamProtocol` implements `ResolveConnectionAsync` returning a constructor-cached `StreamConnectionInfo`; `Endpoint`/`Heartbeat` properties removed; Binance streaming behavior unchanged (regression-free).
- [ ] New engine seam tests assert resolve-called-once-per-connect, resolve-called-on-each-reconnect, Binance-returns-cached-info, cancellation-propagation; ALL existing `Http.Tests.Unit` streaming tests + the Binance streaming integration suite pass unchanged; solution builds 0W/0E; NO network.

## Pattern Reference
- Seam to change: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs` (current `Endpoint`/`Heartbeat` properties).
- Engine call sites: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs` (`OpenSocketAsync` ~line 286, `ReconnectCoreAsync` ~line 510 — per ADR-002 §"Prior Documentation").
- Binance impl to migrate: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs` + `BinanceStreamOptions.cs`.
- HeartbeatPolicy record: `src/CryptoExchanges.Net.Http/Streaming/HeartbeatPolicy.cs`.
- Existing engine fake-transport tests to extend: `tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/` (FEAT-005 engine tests).

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs
- tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamEngineResolveConnectionTests.cs

**Modifies**:
- src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs
- src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs
- src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs

## Traceability
- **PRD Acceptance Criteria**: AC-4 (token re-negotiated on reconnect — seam enables it), AC-5 (Binance streaming regression-free), AC-7 (no-network engine tests)
- **TRD Component**: §"Work Stream A — Streaming Seam Generalization", §"Dependency Impact"
- **ADR Reference**: ADR-002 (full — accepted decision, rejected alternatives, K1/C1 preservation)

## Commits

- f25dc9d — feat(FEAT-006): ADR-002 streaming endpoint seam — async ResolveConnectionAsync + migrate Binance

## Implementation Log

- Created `StreamConnectionInfo.cs` (new `internal sealed record StreamConnectionInfo(Uri, HeartbeatPolicy)`) — K1 preserved: no Core.Models/DeltaMapper.
- Updated `IStreamProtocol.cs`: removed `Endpoint`/`Heartbeat` properties; added `ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct)` with full XML docs.
- Updated `StreamEngine.cs`: `OpenSocketAsync` and `ReconnectCoreAsync` now `await _protocol.ResolveConnectionAsync(ct)` before each `ConnectAsync`; `StartHeartbeat` receives `HeartbeatPolicy` from the resolved info (no longer reads `_protocol.Heartbeat`); C1/K2/K3 behavior preserved.
- Updated `BinanceStreamProtocol.cs`: constructor caches a `StreamConnectionInfo` built from `BinanceStreamOptions`; `ResolveConnectionAsync` returns it via `new ValueTask<StreamConnectionInfo>(_connectionInfo)` (no-alloc fast path); `Endpoint`/`Heartbeat` properties removed; behavior identical.
- Updated `FakeStreamProtocol.cs`: implements `ResolveConnectionAsync`, tracks `ResolveCount`, exposes `HeartbeatPolicy` setter; old `Endpoint`/`Heartbeat` members removed.
- Updated `StreamEngineTests.cs`: `VenueKeyProtocol` inner class updated to implement `ResolveConnectionAsync` and drop `Endpoint`/`Heartbeat`.
- Updated `BinanceStreamProtocolTests.cs`: replaced `Heartbeat_IsServerPingClientPong` with two async tests: `ResolveConnectionAsync_ReturnsServerPingClientPong` and `ResolveConnectionAsync_ReturnsCachedInstance`.
- Created `StreamEngineResolveConnectionTests.cs`: 4 new tests — resolve-on-first-connect, resolve-on-each-reconnect, not-cached-from-first-connect, cancellation-propagation.
- Build: 0W/0E. Test run: 584 passed, 0 failed.

## Review Results
