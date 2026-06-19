---
id: TASK-043
status: PLANNED
depends_on: [TASK-042]
---
# TASK-043: Http engine contracts + fake-transport test seam

## Metadata
- **ID**: TASK-043
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-042
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Http/Streaming/FrameKind.cs, src/CryptoExchanges.Net.Http/Streaming/StreamFrame.cs, src/CryptoExchanges.Net.Http/Streaming/HeartbeatDirection.cs, src/CryptoExchanges.Net.Http/Streaming/PingFormat.cs, src/CryptoExchanges.Net.Http/Streaming/HeartbeatPolicy.cs, src/CryptoExchanges.Net.Http/Streaming/StreamRequest.cs, src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs, src/CryptoExchanges.Net.Http/Streaming/IWebSocketConnection.cs, tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeWebSocketConnection.cs, tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamContractTests.cs]
- **Wave**: 2
- **Traces to**: FEAT-005 spec §Architecture "Http" + §"Binding constraints" C1; design §"The seam" + §"Testing"; DECISION-STREAMING-SHARED §2 (`IStreamProtocol`) + §6 (C1, Inv 11)
- **Created at**: 2026-06-19T17:20:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Define the Http-layer engine **contracts** and the **fake-transport test seam** — the data types and
interfaces the byte-engine (TASK-044) will consume. NO engine logic yet; NO live transport. Everything
under `src/CryptoExchanges.Net.Http/Streaming/`. One type per file.

**K1 is binding here and forever:** these types handle `byte` / `string` / `Uri` / `Func` only.
NO `using CryptoExchanges.Net.Core.Models`, NO DeltaMapper reference anywhere under
`src/CryptoExchanges.Net.Http/`.

Types to create (shapes locked by design §"The seam"):

- **`FrameKind`** (enum): `Data, Ack, Pong, Error`.
- **`StreamFrame`** (`readonly record struct`): `StreamFrame(FrameKind Kind, string? RoutingKey)`.
- **`HeartbeatDirection`** (enum): `ServerPingClientPong, ClientPing`.
- **`PingFormat`** (enum): `ControlFrame, Text, Json`.
- **`HeartbeatPolicy`** (`sealed record`): `Direction`, `Interval`, `Timeout`,
  `ReadOnlyMemory<byte> ClientPingPayload = default`, `PingFormat PingFormat = PingFormat.ControlFrame`.
  **Pure data** — no timers/threads/behavioral methods (C1).
- **`StreamRequest`** — the venue-neutral subscribe descriptor the protocol turns into wire text.
  Carry the stream-type discriminator token (ticker/trade/orderbook/kline — the same token the consumer
  requests and the decode registry is keyed by), the already-resolved wire symbol string, and the
  optional params (depth / kline interval). Keep it a small immutable record. (Symbol resolution happens
  exchange-side BEFORE this is built — the Http layer never sees a `Core.Models.Symbol`.)
- **`IStreamProtocol`** (`internal`): `Uri Endpoint { get; }`, `string BuildSubscribe(StreamRequest)`,
  `string BuildUnsubscribe(StreamRequest)`, `StreamFrame Classify(ReadOnlySpan<byte> frame)`,
  `HeartbeatPolicy Heartbeat { get; }`. Injected interface, never `static` (Inv 11). The engine acts on
  `Kind` (skip Ack/Pong, surface Error, route Data by `RoutingKey`); pong recognition folds into
  `Classify` returning `FrameKind.Pong`.
- **`IWebSocketConnection`** (`internal`): the raw-transport abstraction wrapping `ClientWebSocket`
  (`ConnectAsync`, `SendAsync`(text + control/pong), `ReceiveAsync` → frame bytes + close signal,
  `CloseAsync`/dispose, a `State`/`IsOpen` signal). This is the seam the fake replaces — keep the surface
  minimal and transport-only (bytes in/out + connect/close), no protocol knowledge.

Test seam (in the Http unit-test project):
- **`FakeWebSocketConnection`** — an `IWebSocketConnection` test double that emits canned frames on demand,
  simulates disconnects/reconnects, records sent frames (for asserting subscribe/heartbeat sends), and lets
  tests drive the receive sequence deterministically. This is what makes TASK-044/045 testable with NO network.
- **`StreamContractTests`** — light tests asserting the contract types behave as values (e.g. `HeartbeatPolicy`
  defaults; `StreamFrame` equality) and that `FakeWebSocketConnection` round-trips a canned frame.

## Acceptance Criteria
- [ ] All eight contract types exist under `src/CryptoExchanges.Net.Http/Streaming/` (one per file) with the exact locked shapes; `IStreamProtocol`/`IWebSocketConnection` are `internal`, `HeartbeatPolicy` is pure data (no timers/threads/behavioral methods — C1).
- [ ] `FakeWebSocketConnection` + `StreamContractTests` pass; the fake can emit canned frames and simulate disconnect with no network; solution builds 0W/0E under `TreatWarningsAsErrors`.
- [ ] **K1 verified**: zero `Core.Models` and zero DeltaMapper references under `src/CryptoExchanges.Net.Http/` (`grep -rn "Core.Models\|DeltaMapper\|IMapper" src/CryptoExchanges.Net.Http/` returns nothing); existing 499 tests stay green.

## Pattern Reference
- Exact seam member signatures: design doc §"The seam" (lines 64-87) — copy verbatim; §"Testing" (line 118-119) for the fake-transport approach.
- Http internal-type + DI conventions: `src/CryptoExchanges.Net.Http/ExchangeServiceRegistration.cs` (internal static shared body; no Core.Models/DeltaMapper anywhere).
- Existing Http unit-test fake/stub style to mirror: `tests/CryptoExchanges.Net.Http.Tests.Unit/StubHandler.cs`.
- `InternalsVisibleTo` for the test project: check `src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj` — if internals are not already visible to `CryptoExchanges.Net.Http.Tests.Unit`, add it (mirror however other internal Http types are tested).

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Http/Streaming/FrameKind.cs
- src/CryptoExchanges.Net.Http/Streaming/StreamFrame.cs
- src/CryptoExchanges.Net.Http/Streaming/HeartbeatDirection.cs
- src/CryptoExchanges.Net.Http/Streaming/PingFormat.cs
- src/CryptoExchanges.Net.Http/Streaming/HeartbeatPolicy.cs
- src/CryptoExchanges.Net.Http/Streaming/StreamRequest.cs
- src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs
- src/CryptoExchanges.Net.Http/Streaming/IWebSocketConnection.cs
- tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/FakeWebSocketConnection.cs
- tests/CryptoExchanges.Net.Http.Tests.Unit/Streaming/StreamContractTests.cs

**Modifies**:
- src/CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj (only if `InternalsVisibleTo` for the Http unit-test project is not already present)

## Traceability
- **PRD Acceptance Criteria**: n/a — FEAT-005 spec §Architecture "Http" + Success criterion "new unit tests via an injected fake transport (no network)"
- **TRD Component**: Http engine contracts + transport seam (design §"The seam", §"Testing")
- **ADR Reference**: DECISION-STREAMING-SHARED §2 (`IStreamProtocol` finalized) + §6 (C1 protocol-describes-heartbeat, K1 no-Core.Models-in-Http, Inv 11 injected-internal-not-static)

## Implementation Log

### Attempt 1
<!-- implementer fills this in -->

## Review Results

### Attempt 1
<!-- review-gate fills this in -->
