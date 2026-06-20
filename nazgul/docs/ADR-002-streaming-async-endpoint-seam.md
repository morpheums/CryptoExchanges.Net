# ADR-002: Async per-connection endpoint resolution seam for `IStreamProtocol`

- **Status**: Accepted
- **Date**: 2026-06-20
- **Context**: FEAT-006 (KuCoin integration). Raised during TRD design; assessed against
  FEAT-005 constraints (C1, K1, K2, K3) and the locked streaming architecture.

## Context

`IStreamProtocol` (shipped in FEAT-005) exposes `Uri Endpoint { get; }` — a synchronous,
static property the engine reads before each `ConnectAsync`. This was sufficient for Binance,
whose WebSocket URL is a hard-coded constant.

KuCoin requires a two-phase setup before each connection:

1. `POST /api/v1/bullet-public` → JSON `{ token, instanceServers:[{endpoint, pingInterval, pingTimeout}] }`.
2. Connect to `{instanceServer.endpoint}?token={token}&connectId={uuid}`.

The token is short-lived and must be re-fetched on **every reconnect**. The `pingInterval` and
`pingTimeout` values are server-dictated (not a KuCoin constant) and vary per response, so the
`HeartbeatPolicy` is also only known after negotiation.

Additionally, `HeartbeatPolicy` today lives as a separate `Heartbeat { get; }` property on
`IStreamProtocol`. Both `Endpoint` and `Heartbeat` are read-once-at-connect properties; they
belong together as outputs of the same negotiation step.

## Decision

Replace the two static properties `Uri Endpoint { get; }` and `HeartbeatPolicy Heartbeat { get; }`
on `IStreamProtocol` with a single async method:

```csharp
ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct);
```

Where `StreamConnectionInfo` is a new `internal sealed record` in `CryptoExchanges.Net.Http.Streaming`:

```csharp
internal sealed record StreamConnectionInfo(Uri Endpoint, HeartbeatPolicy Heartbeat);
```

`StreamEngine` calls `await _protocol.ResolveConnectionAsync(ct)` in both `OpenSocketAsync`
(first connect) and `ReconnectCoreAsync` (on every reconnect attempt), replacing the two prior
property reads. The rest of the engine is unchanged.

`BinanceStreamProtocol` implements the new method by returning a cached `StreamConnectionInfo`
built once in the constructor from `BinanceStreamOptions`. No behavior change for Binance.

`KucoinStreamProtocol` implements it by calling `KucoinBulletPublicClient.NegotiateAsync`,
picking the first instance server, appending the token+connectId query params, and constructing
a `HeartbeatPolicy` from the server-dictated intervals.

## Consequences

- **Interface is a breaking change** for any external implementor of `IStreamProtocol`.
  The interface is `internal` — no external implementors exist. Zero consumer impact.
- `BinanceStreamProtocol` drops two public properties (`Endpoint`, `Heartbeat`) and gains one
  async method. Binance streaming behavior is identical.
- The engine gains one `await` per connection/reconnect (a `POST` HTTP call for KuCoin;
  a no-alloc `ValueTask.FromResult` for Binance). The fast path for Binance has negligible overhead.
- `StreamConnectionInfo` carries only `Uri` + `HeartbeatPolicy` — no `Core.Models`, no DeltaMapper
  references. **Constraint K1 (no Core.Models/DeltaMapper under Http) is preserved.**
- Heartbeat policy is now naturally re-read on every reconnect. For KuCoin this is required;
  for Binance the value is stable (no regression).
- C1 preserved: protocol *describes* heartbeat (via `StreamConnectionInfo.Heartbeat`), engine
  *executes* it. No timer or thread enters the protocol.

## Alternatives Considered

### A — Keep `Endpoint` as static property; add a separate `NegotiateAsync` lifecycle hook

The engine would call `NegotiateAsync` first, then read `Endpoint`. This splits what is logically
one step (negotiate → get URL + heartbeat) into two interface members that must be called in order,
introducing a contract that is easy to misuse (call `Endpoint` without calling `NegotiateAsync` first).
Rejected: worse API cohesion, same change surface.

### B — Put the bullet-public HTTP call outside `IStreamProtocol` entirely (in `KucoinStreamOptions`)

Options are resolved once at DI wiring time; they cannot make async HTTP calls. A one-time static
token in options would expire before the first reconnect. Rejected: does not support reconnect re-negotiation.

### C — Keep `Endpoint` static; store the token externally and mutate the URI atomically

Race condition on reconnect: two concurrent reconnect attempts (watchdog + pump error) could
each negotiate a token and connect to stale data. The ADR-002 approach serializes negotiation
inside the engine's reconnect lock. Rejected: introduces a concurrency hazard.

### D — Add `ValueTask<Uri> ResolveEndpointAsync(CancellationToken ct)` but keep `HeartbeatPolicy Heartbeat { get; }`

For KuCoin the heartbeat policy (intervals) is also returned by the negotiation call. Splitting
the two would require two round-trips or a KuCoin-specific cached-value hack. Rejected: same cost
as the accepted decision but less cohesive.

## Prior Documentation

- `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs` — current contract (static property).
- `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs` — call sites: `OpenSocketAsync` (line 286)
  and `ReconnectCoreAsync` (line 510).
- `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs` — Binance implementation
  to migrate.
- `nazgul/archive/2026-06-19-033855-FEAT-001-M2/docs/ADR-001-per-exchange-di-and-conventions.md` —
  preceding ADR (numbered 001, this is 002).
