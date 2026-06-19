# Nazgul Plan — FEAT-005

─── ◈ NAZGUL ▸ PLANNING ────────────────────────────────

## Objective

**WebSocket streaming v1 (single-exchange first).** Add real-time market-data streaming —
live ticker, trade, raw order-book, and kline updates feeding the existing canonical
`Core.Models` — with auto-reconnect (engine backoff) + auto-resubscribe (replay the stored
subscribe set), invisible to the consumer. Callback API (`IStreamClient`) delivering
`Core.Models` directly. Built on a **shared, generic streaming engine** so later exchanges add
only a small protocol + decode seam.

**Objective type**: Feature — net-new transport capability. New interfaces only; the existing
REST `IExchangeClient` surface is untouched. Opt-in DI (REST-only consumers pay nothing).

Authoritative inputs (read fully before any task):
- Objective spec: `nazgul/context/objectives/FEAT-005-spec.md`
- Locked design: `docs/superpowers/specs/2026-06-19-websocket-streaming-v1-design.md` (local, gitignored)
- Architect rulings (committed): `nazgul/reviews/DESIGN-STREAMING-V1/architect-reviewer.md`,
  `nazgul/reviews/DECISION-STREAMING-SHARED/architect-reviewer.md`

**The architecture is LOCKED.** These tasks translate it into a build sequence. Do NOT redesign.

## Branch

- **Base**: `main`
- **Feature**: `feat/FEAT-005-websocket-streaming` (to be created)
- Ship as one PR to protected `main`.

## Discovery Status

REUSE existing discovery — do NOT re-run.
- Discovery last run: 2026-06-17 (`nazgul/context/`, 83 files scanned).
- Reviewers (4, existing — do NOT regenerate): `architect-reviewer`, `code-reviewer`,
  `security-reviewer`, `api-reviewer`.
- Classification: BROWNFIELD (HIGH confidence). New streaming transport on top of the shipped
  Core → Http → Exchange → DI library (4 REST exchanges).

## Hard Constraints (recorded for implementer + reviewers)

- **Framework**: `net10.0`. Build **0 warnings / 0 errors** under `TreatWarningsAsErrors`.
- **One type per file** (CLAUDE.md / comment-and-interface conventions). XML docs on public/internal
  interfaces; `<inheritdoc/>` on impls. LEAN comments.
- **Prefer interfaces over static-with-behavior** for swappable behavior (Inv 11). `IStreamProtocol`
  and the decode registry are injected **`internal`** types — never `static class`es holding behavior.
  The only permitted `static` carve-outs are the optional thin `CreateStreams` construction-glue
  wrapper (zero behavior) and the DI registration extension methods.
- **DeltaMapper for DTO → model** mapping; reuse the existing per-exchange `IMapper`/response profile
  and the bespoke keyed `ISymbolMapper`. Do NOT hand-roll mapping that DeltaMapper covers.
- **C1 (binding)** — the protocol *describes* heartbeat (policy data + frame `Classify`); the engine
  *executes* it (timers / liveness watchdog / send / pong). No timers or threads in the protocol;
  no `StartHeartbeat()`-style behavioral method on `IStreamProtocol`.
- **K1 (binding, hard REJECT line)** — NO `using CryptoExchanges.Net.Core.Models` and NO DeltaMapper
  reference anywhere under `src/CryptoExchanges.Net.Http/`. The engine handles `byte` / `object` /
  opaque `Func<ReadOnlyMemory<byte>, object>` only. Any such reference is a blocking layering violation.
- **K2 (binding)** — reconnect replays the **stored subscribe set**; `BuildUnsubscribe` removes from
  that set so an unsubscribed stream is not resurrected on reconnect.
- **K3 (binding)** — socket reconnect is the engine's **own bounded backoff loop**, NOT the REST Polly
  resilience pipeline. Retry stays REST-GET-only; do not route reconnect through `ExchangeResiliencePipeline`.
- **REST surface untouched** — `IExchangeClient` / `IMarketDataService` / `ITradingService` /
  `IAccountService` get NO new members. New capability = new interface (Inv 4/5).
- **No "reserved for v1.1" members** on any v1 interface. The order-book-maintenance hook is *additive*
  (a future separate `IOrderBookMaintainer`), not *reserved*.
- **No captive dependency (Inv 9)** — the long-lived transport is owned **inside** the keyed
  `IStreamClient` singleton; DI path uses `ownsTransport: false` for any shared/factory-owned handle.
- **No competitor / library / product names in committed artifacts.** Use generic terms
  ("the venue", "exchange #1", "client-ping-json") in plan.md and task manifests. (The exchange package
  task names the in-repo `Binance` assembly only because that assembly already exists in the tree;
  no third-party venue is named in prose.)
- **No System.Reactive. No `IAsyncEnumerable` surface in v1** (may be added later, non-breaking).
- **Every task is TDD-able with NO network** via an injected fake transport. One live integration
  smoke (Category=Integration) self-skips without connectivity — matching the existing pattern.
- **Existing test suite (499 tests, `Category!=Integration`) stays green** after every task.

## Status Summary

| Task     | Status    | Wave | Description                                                                 |
|----------|-----------|------|-----------------------------------------------------------------------------|
| TASK-042 | ✦ DONE    | 1    | Core streaming abstractions (`IStreamClient` family) — no transport         |
| TASK-043 | ◆ IN_PROGRESS | 2    | Http engine contracts + fake-transport test seam                            |
| TASK-044 | ◇ PLANNED | 3    | Http reconnecting byte-engine (pump/route/backoff/replay/heartbeat/channels)|
| TASK-045 | ◇ PLANNED | 4    | Generic `StreamClient` + `StreamClientFactory` + `AddStreams<TOptions>`      |
| TASK-046 | ◇ PLANNED | 5    | Exchange-#1 package: protocol + 4 decode closures + options + `Add…Streams` |
| TASK-047 | ◇ PLANNED | 6    | Wire 4 public subscribe methods end-to-end + live integration smoke + docs  |

Tasks: 1/6 DONE — TASK-042 DONE, TASK-043 READY (dep satisfied), TASK-044..047 PLANNED.

## Wave Groups

The loop orchestrator reads this section to determine parallel execution order. This objective is a
single **vertical, dependency-ordered slice** — each task layers on the previous one and edits a
distinct part of the tree, so there is no within-wave parallelism. Waves are strictly sequential.

### Wave 1
- **TASK-042** — Core abstractions. DONE. Created files under
  `src/CryptoExchanges.Net.Core/Streaming/` + Core unit tests.

### Wave 2
- **TASK-043** — Http engine contracts + fake-transport seam. Depends on TASK-042. Creates only files
  under `src/CryptoExchanges.Net.Http/Streaming/` + Http unit-test fakes.

### Wave 3
- **TASK-044** — Reconnecting byte-engine. Depends on TASK-043. Adds the engine + per-subscription
  channels + heartbeat execution under `src/CryptoExchanges.Net.Http/Streaming/`; tested via the fake.

### Wave 4
- **TASK-045** — Generic `StreamClient` + factory + `AddStreams<TOptions>` + decode-registry plumbing.
  Depends on TASK-044.

### Wave 5
- **TASK-046** — Exchange-#1 streaming package (protocol + 4 decode closures + options + 5-line
  registration). Depends on TASK-045. First consumer of the seam.

### Wave 6
- **TASK-047** — 4 public subscribe methods wired end-to-end + live integration smoke + README/docs note.
  Depends on TASK-046.

## Dependency Order

```
TASK-042 ──► TASK-043 ──► TASK-044 ──► TASK-045 ──► TASK-046 ──► TASK-047
```

State machine: TASK-042 is **DONE**; TASK-043 is now **READY** (dependency satisfied);
TASK-044..047 remain **PLANNED** with explicit `depends_on` chain.

## Traceability

No formal PRD/TRD/ADR document set was generated for FEAT-005 (`nazgul/docs/manifest.md` absent for
this objective). The authoritative acceptance source is the spec + the two committed architect rulings.
Each task's **Traces to** field points to the exact spec section / ruling refinement / binding
constraint it fulfills. Coverage of the spec Success Criteria:

- "`IStreamClient` delivers live ticker/trade/order-book/kline `Core.Models`" → public surface defined
  in **TASK-042**, decoders in **TASK-046**, wired + verified live in **TASK-047**.
- "auto-reconnect + auto-resubscribe verified" → engine backoff + replay set in **TASK-044** (K2/K3),
  live verification in **TASK-047**.
- "shared engine/client/factory are exchange-agnostic; the exchange contributes only protocol + decode +
  options + registration" → engine **TASK-044**, generic client/factory/registration **TASK-045**,
  per-exchange seam **TASK-046**.
- "Build 0W/0E; existing 499 tests stay green; new unit tests via injected fake transport (no network);
  one live integration smoke that self-skips" → fake seam **TASK-043**, fake-driven unit tests
  **TASK-044/045/046**, integration smoke **TASK-047**; the 0W/0E + 499-green bar is an acceptance
  criterion on EVERY task.
- "No `Core.Models`/DeltaMapper references under Http" (K1) → enforced as an acceptance criterion on
  **TASK-043, TASK-044, TASK-045** (the only Http-touching tasks).

Every spec scope-in item maps to a task; nothing in Scope-Out (order-book maintenance, private streams,
other exchanges, `IAsyncEnumerable`, System.Reactive) is planned.

## Completed

- **TASK-042** — DONE (2026-06-19T18:30:00Z). Core streaming abstractions approved unanimously (4/4).
  Commit: `1c041b5`. Review artifacts: `nazgul/reviews/TASK-042/`.

## Recovery Pointer

- **Current stage**: TASK-042 DONE — review gate passed unanimously.
- **Next action**: Claim TASK-043 (Http engine contracts + fake-transport seam). TASK-043 is now READY.
- **Active task**: none (TASK-042 complete; TASK-043 is next).
- **Files are truth**: task manifests in `nazgul/tasks/TASK-042..047.md` carry full state;
  frontmatter `status:` is canonical.

─── ◈ NEXT ─────────────────────────────────────────────
  ✦ TASK-042 — Core streaming abstractions. DONE.
  ◇ TASK-043 — Http engine contracts + fake-transport seam. READY (claim next).
────────────────────────────────────────────────────────
