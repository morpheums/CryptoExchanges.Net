---
id: TASK-042
status: DONE
depends_on: []
---
# TASK-042: Core streaming abstractions (`IStreamClient` family)

## Metadata
- **ID**: TASK-042
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: none
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Core/Streaming/StreamConnectionState.cs, src/CryptoExchanges.Net.Core/Streaming/StreamLag.cs, src/CryptoExchanges.Net.Core/Streaming/StreamHandlers.cs, src/CryptoExchanges.Net.Core/Streaming/IStreamSubscription.cs, src/CryptoExchanges.Net.Core/Interfaces/IStreamClient.cs, src/CryptoExchanges.Net.Core/Interfaces/IStreamClientFactory.cs, tests/CryptoExchanges.Net.Core.Tests.Unit/Streaming/StreamHandlersTests.cs]
- **Wave**: 1
- **Traces to**: FEAT-005 spec §Architecture "Core"; design §"Public surface (Core)"; DESIGN-STREAMING-V1 R1/R2/R4; DECISION-STREAMING-SHARED §1 (public surface is `IStreamClient`/`IStreamSubscription` + records)
- **Created at**: 2026-06-19T17:20:00Z
- **Claimed at**: 2026-06-19T17:30:00Z
- **Base SHA**: aa6fe22e4b9c0ed480bcf2c898bd41b60386902d
- **Implemented at**: 2026-06-19T17:45:00Z
- **Completed at**: 2026-06-19T18:30:00Z
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Define the Core-layer streaming abstractions over `Core.Models` only — **no transport, no exchange
knowledge** (Inv 1). This is the public consumer surface; everything else in later tasks sits behind it.

One type per file (CLAUDE.md). Place the new types under a `src/CryptoExchanges.Net.Core/Streaming/`
folder, except the two interfaces that are the public entry points, which go alongside the existing
abstractions in `src/CryptoExchanges.Net.Core/Interfaces/` to match `IExchangeClient` /
`IExchangeClientFactory`.

Types to create (exact shapes locked by the design — do NOT add members):

- **`StreamConnectionState`** (enum): `Connecting, Live, Reconnecting, Closed`.
- **`StreamLag`** (`readonly record struct`): `StreamLag(int DroppedCount)`.
- **`StreamHandlers<T>`** (`sealed record`): `OnUpdate` (`Func<T, ValueTask>`, required) +
  optional `OnReconnecting`/`OnReconnected` (`Func<ValueTask>?`) + `OnLagged` (`Func<StreamLag, ValueTask>?`).
  One handler bundle per subscription (R2). Lifecycle delivered via awaitable callbacks, NOT events (R1).
- **`IStreamSubscription : IAsyncDisposable`** — `StreamConnectionState State { get; }` (source of truth,
  R1) + `bool IsConnected { get; }` (convenience `=> State == Live`). Disposing = unsubscribe.
- **`IStreamClient : IAsyncDisposable`** — `ExchangeId ExchangeId { get; }` + the four async subscribe
  methods, each returning `Task<IStreamSubscription>` and taking `(Symbol, …, StreamHandlers<T>, CancellationToken = default)`:
  ticker (`Ticker`), trades (`Trade`), order book (`int depth`, `OrderBook`), klines (`KlineInterval`, `Candlestick`).
  A bare-`Func<T,ValueTask> onUpdate` convenience overload per method is allowed (wraps into
  `StreamHandlers<T>`; non-breaking) — implement as default-interface or leave for the impl task; if added
  here, keep it a thin wrapper only.
- **`IStreamClientFactory`** — mirror `IExchangeClientFactory` exactly:
  `IReadOnlyCollection<ExchangeId> Available { get; }`, `IStreamClient GetClient(ExchangeId)`
  (throws `ExchangeNotRegisteredException`), `bool TryGet(ExchangeId, out IStreamClient?)`.

**No "reserved for v1.1" members** — the order-book-maintenance hook is a future separate interface,
not a reserved member here. XML docs on every public type/member; LEAN comments.

Tests: a small Core unit test asserting `StreamHandlers<T>` construction (required `OnUpdate`,
optional callbacks default to null) and `IsConnected` semantics via a tiny in-test fake subscription.
No transport — these are pure value/contract tests.

## Acceptance Criteria
- [x] All seven types exist with the exact locked shapes (one type per file); `IStreamClient`/`IStreamClientFactory` live in `Interfaces/`, the records/enum/`IStreamSubscription` under `Streaming/`; no "reserved-for-v1.1" members anywhere.
- [x] Core unit tests pass (`StreamHandlers<T>` required/optional handler construction + `IsConnected => State == Live`); solution builds 0W/0E under `TreatWarningsAsErrors`.
- [x] Existing 499 tests (`Category!=Integration`) stay green; no edits to existing REST interfaces or `Core.Models`.

## Pattern Reference
- Factory interface shape to mirror: `src/CryptoExchanges.Net.Core/Interfaces/IExchangeClientFactory.cs` (full file).
- Client interface + `ExchangeId` property + `IAsyncDisposable` shape: `src/CryptoExchanges.Net.Core/Interfaces/IExchangeClient.cs` (full file).
- Models the handlers carry: `src/CryptoExchanges.Net.Core/Models/{Ticker,Trade,OrderBook,Candlestick}.cs`; `Symbol.cs`; enum `src/CryptoExchanges.Net.Core/Enums/KlineInterval.cs`.
- Exact public surface text: design doc §"Public surface (Core)" (lines 31-59) — copy member signatures verbatim.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Core/Streaming/StreamConnectionState.cs
- src/CryptoExchanges.Net.Core/Streaming/StreamLag.cs
- src/CryptoExchanges.Net.Core/Streaming/StreamHandlers.cs
- src/CryptoExchanges.Net.Core/Streaming/IStreamSubscription.cs
- src/CryptoExchanges.Net.Core/Interfaces/IStreamClient.cs
- src/CryptoExchanges.Net.Core/Interfaces/IStreamClientFactory.cs
- tests/CryptoExchanges.Net.Core.Tests.Unit/Streaming/StreamHandlersTests.cs

**Modifies**:
- none

## Traceability
- **PRD Acceptance Criteria**: n/a — FEAT-005 spec §Architecture "Core" + Success criteria ("`IStreamClient` delivers … `Core.Models`")
- **TRD Component**: Core streaming abstractions (design §"Public surface (Core)")
- **ADR Reference**: DESIGN-STREAMING-V1 R1 (status-enum + awaitable callbacks), R2 (`StreamHandlers<T>`), R4 (`OnLagged`); DECISION-STREAMING-SHARED §1 (no per-exchange client; public surface = `IStreamClient` + records); Inv 1/4/5/11

## Commits

- `1c041b5` feat(FEAT-005): add Core streaming abstractions (TASK-042)

## Implementation Log

### Attempt 1

Created 6 source files + 1 test file. All types match the locked design signatures verbatim.

**Files created:**
- `src/CryptoExchanges.Net.Core/Streaming/StreamConnectionState.cs` — enum with 4 states
- `src/CryptoExchanges.Net.Core/Streaming/StreamLag.cs` — `readonly record struct StreamLag(int DroppedCount)`
- `src/CryptoExchanges.Net.Core/Streaming/StreamHandlers.cs` — `sealed record StreamHandlers<T>(OnUpdate required + 3 optional callbacks)`
- `src/CryptoExchanges.Net.Core/Streaming/IStreamSubscription.cs` — `IAsyncDisposable` with `State` + `IsConnected`
- `src/CryptoExchanges.Net.Core/Interfaces/IStreamClient.cs` — 4 subscribe methods returning `Task<IStreamSubscription>`
- `src/CryptoExchanges.Net.Core/Interfaces/IStreamClientFactory.cs` — mirrors `IExchangeClientFactory` exactly
- `tests/CryptoExchanges.Net.Core.Tests.Unit/Streaming/StreamHandlersTests.cs` — 9 unit tests (TDD)

**Results:**
- Build: 0 warnings, 0 errors (TreatWarningsAsErrors)
- New streaming tests: 9/9 passed
- Existing tests: 497 non-integration tests pass, 0 failures
- No transport, no exchange knowledge in Core (Inv 1 verified)

## Review Results

### Attempt 1

**Verdict: APPROVED — all 4 reviewers unanimous**

| Reviewer | Verdict | Score |
|---|---|---|
| architect-reviewer | APPROVED | 98 |
| code-reviewer | APPROVED | 98 |
| security-reviewer | APPROVED | 98 |
| api-reviewer | APPROVED | 96 |

**Blocking findings**: none.

**Non-blocking concerns (confidence < 80%)**:
- architect-reviewer + api-reviewer (55%): `IsConnected` declared as pure abstract property rather than a default interface member (DIM). Noted for maintainer awareness before N exchange implementations exist. Non-blocking per design doc which explicitly left this choice open.

**Pre-checks**: build 0W/0E, 497/497 non-integration tests pass, no smoke command configured.
