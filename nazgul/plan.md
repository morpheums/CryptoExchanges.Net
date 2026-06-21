# Nazgul Plan — FEAT-006

─── ◈ NAZGUL ▸ PLANNING ────────────────────────────────

## Objective

**FEAT-006 — KuCoin Exchange Integration (full parity: REST + WebSocket streaming).** Add KuCoin as
the 5th exchange at full parity: REST market data + account + trading (KC-API passphrase-v2 HMAC
signing, bespoke `ISymbolMapper` for `BTC-USDT`, DeltaMapper DTO→model, `AddKucoinExchange` DI, MCP
wiring) **plus** public WebSocket streaming (ticker / trade / order book / kline) with auto-reconnect +
token re-negotiation + auto-resubscribe. This objective also generalizes the shared streaming endpoint
seam (ADR-002) to support KuCoin's token-negotiated `bullet-public` connection while keeping Binance's
static-URL path unchanged. Spot-only; public streams only; no order-book maintenance.

**Objective type**: Feature (brownfield extension) — clones the verified OKX/Bitget REST template and
the Binance streaming template; one targeted change to the shared streaming engine's endpoint seam.

Authoritative inputs (read fully before any task):
- Objective spec: `nazgul/context/objectives/FEAT-006-spec.md` (PRIMARY)
- `nazgul/docs/PRD-FEAT-006.md`, `nazgul/docs/TRD-FEAT-006.md`,
  `nazgul/docs/ADR-002-streaming-async-endpoint-seam.md`, `nazgul/docs/TEST-PLAN-FEAT-006.md`

## Branch

- **Base**: `main` (protected — ship via PR).
- **Feature**: `feat/FEAT-006-kucoin` (to be created).

## Hard Constraints (recorded for implementer + reviewers)

- **Framework**: `net10.0`. Build **0 warnings / 0 errors** under `TreatWarningsAsErrors`;
  `AnalysisLevel=latest-all`; `GenerateDocumentationFile=true` (full XML docs on public + `internal`
  interfaces; `<inheritdoc/>` on impls). **One type per file.** LEAN comments.
- **4-layer chain** (Core → Http → Exchange → DI) preserved. KuCoin references Core + Http only.
- **K1 (hard REJECT line)** — NO `Core.Models` and NO DeltaMapper reference anywhere under
  `src/CryptoExchanges.Net.Http/`. The endpoint-seam change (`StreamConnectionInfo`) carries only
  `Uri` + `HeartbeatPolicy`; the engine stays byte/opaque.
- **C1** — protocol *describes* heartbeat; engine *executes* it. No timer/thread in any `IStreamProtocol`.
- **K2/K3** — reconnect replays the stored subscribe set; socket reconnect is the engine's own bounded
  backoff (NOT the REST Polly pipeline). Retry stays **REST-GET-only**; signed requests re-sign per
  attempt (mark-and-strip).
- **DeltaMapper** for DTO→model (project mandate — do not hand-roll mapping it covers). Bespoke keyed
  `ISymbolMapper` for `BTC-USDT`.
- **House DTO-naming** — internal `{Concept}Dto` wire DTOs in `Dtos/`; vendor names only in
  `[JsonPropertyName]`; reserved `ResponseDto<T>`/`ListDto<T>` wrappers only.
- **ADR-001** — per-exchange `AddKucoinExchange` ships in the KuCoin assembly.
- **ADR-002** — `IStreamProtocol.Endpoint`/`Heartbeat` → async `ResolveConnectionAsync`; Binance
  migrated with zero behavior change; Binance streaming regression-free.
- **No opsec leakage** — README/commits/PRs/MCP metadata stay strictly technical.
- Every task is TDD-able with **NO network** (fake transport / stub HTTP handler). Live integration
  smokes (Category=Integration) self-skip without `KUCOIN_API_KEY`/`KUCOIN_SECRET_KEY`/`KUCOIN_PASSPHRASE`.
- Existing non-integration suite stays green after every task (`dotnet test --filter 'Category!=Integration'`).

## Status Summary

| Task     | Status     | Wave | Description                                                                  |
|----------|------------|------|------------------------------------------------------------------------------|
| TASK-056 | ✦ DONE     | 1    | Scaffold `CryptoExchanges.Net.Kucoin` + Unit/Integration test projects (OKX clone) |
| TASK-061 | ✦ DONE     | 1    | ADR-002 streaming endpoint seam → async `ResolveConnectionAsync` + migrate Binance |
| TASK-057 | ◇ READY    | 2    | KC-API passphrase-v2 signing service + mark-and-strip signing handler        |
| TASK-058 | ◇ READY    | 2    | Bespoke `ISymbolMapper` + REST wire DTOs + DeltaMapper profiles + parsers    |
| TASK-059 | ◇ PLANNED  | 3    | REST services (market/account/trading) + http client + composer + entry     |
| TASK-060 | ◇ PLANNED  | 4    | `AddKucoinExchange` DI + `AddCryptoExchanges` + MCP wiring                   |
| TASK-062 | ◇ PLANNED  | 5    | `KucoinStreamProtocol` + bullet-public + 4 decoders + `AddKucoinStreams`     |
| TASK-063 | ◇ PLANNED  | 6    | Live integration smokes — REST + one streaming (self-skip)                   |
| TASK-064 | ◇ PLANNED  | 6    | Docs — README KuCoin row → supported + MCP/exchanges/streaming reference     |

Tasks: 2/9 DONE.

## Wave Groups

The loop orchestrator reads this section to determine parallel execution order. Tasks in the same wave
have NO dependency on each other AND NO file overlap, so they can run in parallel. Two independent
work streams exist: the KuCoin REST package (TASK-056 -> 057/058 -> 059 -> 060) and the shared
streaming-seam generalization (TASK-061, Binance-only files), which converge at TASK-062.

### Wave 1
- **TASK-056** — Scaffold Kucoin package + test projects. No deps. Touches only new
  `src/CryptoExchanges.Net.Kucoin/*` + new test projects + `CryptoExchanges.Net.sln`.
- **TASK-061** — ADR-002 endpoint-seam generalization + Binance migration. No deps. Touches only
  `src/CryptoExchanges.Net.Http/Streaming/*` + `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs`
  + `tests/CryptoExchanges.Net.Http.Tests.Unit/*`. No file overlap with TASK-056 -> parallel-safe.

### Wave 2
- **TASK-057** — Signing service + handler. Depends on TASK-056. Touches `Auth/` + `Resilience/`.
- **TASK-058** — Symbol mapper + DTOs + DeltaMapper profiles + parsers. Depends on TASK-056. Touches
  `Dtos/` + `Mapping/` + `Internal/`. No file overlap with TASK-057 -> parallel-safe within Wave 2.

### Wave 3
- **TASK-059** — REST services + http client + composer + entry. Depends on TASK-057 + TASK-058
  (needs signing handler + DTOs/mapper). Touches `Services/` + `Internal/composer` + entry/http client.

### Wave 4
- **TASK-060** — `AddKucoinExchange` DI + `AddCryptoExchanges` + MCP wiring. Depends on TASK-059.
  Touches the KuCoin `ServiceCollectionExtensions` + DI/MCP csprojs.

### Wave 5
- **TASK-062** — `KucoinStreamProtocol` + bullet-public + 4 decoders + `AddKucoinStreams`. Depends on
  TASK-058 (mapper/profiles), TASK-060 (keyed DI to mirror), TASK-061 (the generalized seam).
  Convergence point of the two work streams.

### Wave 6
- **TASK-063** — Live integration smokes (REST + streaming). Depends on TASK-060 + TASK-062. Touches
  only the Integration test project.
- **TASK-064** — Docs (README/MCP/exchanges/streaming). Depends on TASK-060 + TASK-062. Touches only
  docs/README. No file overlap with TASK-063 -> parallel-safe within Wave 6.

## Dependency Order

```
TASK-056 -> TASK-057 -+
TASK-056 -> TASK-058 -+-> TASK-059 -> TASK-060 -+
TASK-061 ----------------------------------------+-> TASK-062 -> TASK-063
TASK-058 ----------------------------------------+               TASK-062 -> TASK-064
```

## Traceability (PRD -> tasks)

Every PRD-FEAT-006 acceptance criterion maps to at least one task:

- **AC-1** (REST -> Core.Models, AddKucoinExchange resolves working client) -> TASK-056, 058, 059, 060;
  live-verified TASK-063.
- **AC-2** (passphrase-v2 signing; per-attempt re-sign; retry GET-only) -> TASK-057; exercised in 059;
  live-verified 063.
- **AC-3** (ticker/trade/order-book/kline public streams -> Core.Models) -> TASK-062; live-verified 063.
- **AC-4** (forced disconnect -> reconnect + token re-negotiation + resubscribe) -> seam in TASK-061,
  `ResolveConnectionAsync` in 062; live-verified 063.
- **AC-5** (Binance streaming regression-free after seam change) -> TASK-061 (regression coverage).
- **AC-6** (0W/0E under TreatWarningsAsErrors; full XML docs) -> acceptance criterion on EVERY task.
- **AC-7** (non-integration suite green; fake/stub no-network unit tests) -> unit tests on 056-062.
- **AC-8** (live integration smokes self-skip without env vars) -> TASK-063.
- **AC-9** (README KuCoin supported badge + MCP reference) -> TASK-064.

Nothing in PRD "Out of Scope" (futures/margin, private streams, order-book maintenance) is planned.

## Recovery Pointer

- **Current stage**: ✦ TASK-061 DONE (reviewed 4/4 APPROVED, completion SHA f04dfc4). Wave 1 complete — both TASK-056 and TASK-061 are DONE.
- **Next action**: Begin Wave 2 in parallel — TASK-057 (signing service + handler) and TASK-058 (symbol mapper + DTOs) are both READY (TASK-056 dependency satisfied; TASK-061 no deps).
- **Active task**: none.
- **Files are truth**: the task manifests under `nazgul/tasks/` carry full state; each manifest's
  frontmatter is the canonical record.

─── ◈ NEXT ─────────────────────────────────────────────
  ✦ TASK-056 — Scaffold complete; DONE (reviewed 4/4, commit 2b9c308).
  ✦ TASK-061 — ADR-002 seam generalization DONE (reviewed 4/4, commit f04dfc4).
  ◆ TASK-057 — KC-API passphrase-v2 signing service + handler (Wave 2, READY — 056 unblocked).
  ◆ TASK-058 — Symbol mapper + DTOs + DeltaMapper profiles (Wave 2, READY — 056 unblocked).
────────────────────────────────────────────────────────

## Completed

- **TASK-056** — DONE (2026-06-21T01:30:00Z). KuCoin scaffold approved unanimously (4/4).
  Impl commit: `2b9c308`. Completion SHA: `40ab130`. Review artifacts: `nazgul/reviews/TASK-056/`.
- **TASK-061** — DONE (2026-06-21T05:00:00Z). ADR-002 streaming endpoint seam approved unanimously (4/4).
  Impl commit: `f25dc9d`. Simplify commit: `f04dfc4`. Completion SHA: `f04dfc4`. Review artifacts: `nazgul/reviews/TASK-061/`.


## Archived — FEAT-005 (WebSocket streaming v1) — COMPLETE

> Preserved below for history. FEAT-001..004 archived under `nazgul/archive/`. Active objective is FEAT-006 (above).


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
| TASK-043 | ✦ DONE    | 2    | Http engine contracts + fake-transport test seam                            |
| TASK-044 | ✦ DONE    | 3    | Http reconnecting byte-engine (pump/route/backoff/replay/heartbeat/channels)|
| TASK-045 | ✦ DONE    | 4    | Generic `StreamClient` + `StreamClientFactory` + `AddStreams<TOptions>`      |
| TASK-046 | ✦ DONE    | 5    | Exchange-#1 package: protocol + 4 decode closures + options + `Add…Streams` |
| TASK-047 | ✦ DONE    | 6    | Wire 4 public subscribe methods end-to-end + live integration smoke + docs  |
| TASK-048 | ✦ DONE    | 7    | Lean trim of low-value streaming tests                                       |
| TASK-049 | ✦ DONE    | 7    | Fix routing-key keyspace mismatch + liveness reset (consolidated review)    |
| TASK-050 | ✦ DONE    | 7    | PR #26 review findings — streaming hardening + house-rule cleanups          |

Tasks: 9/9 DONE. Shipped in PR #26 (squash `5a50a8b`) + closeout. FEAT-005 complete — **NAZGUL_COMPLETE**.

## Wave Groups

The loop orchestrator reads this section to determine parallel execution order. This objective is a
single **vertical, dependency-ordered slice** — each task layers on the previous one and edits a
distinct part of the tree, so there is no within-wave parallelism. Waves are strictly sequential.

### Wave 1
- **TASK-042** — Core abstractions. DONE. Created files under
  `src/CryptoExchanges.Net.Core/Streaming/` + Core unit tests.

### Wave 2
- **TASK-043** — Http engine contracts + fake-transport seam. DONE. Created 10 files under
  `src/CryptoExchanges.Net.Http/Streaming/` + Http unit-test fakes. Reviewed 4/4 APPROVED.

### Wave 3
- **TASK-044** — Reconnecting byte-engine. DONE. Adds the engine + per-subscription
  channels + heartbeat execution under `src/CryptoExchanges.Net.Http/Streaming/`; tested via the fake.
  Reviewed 4/4 APPROVED (Cycle 2). Commit: 501ad13.

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

State machine: TASK-042 is **DONE**; TASK-043 is now **DONE**; TASK-044 is now **DONE**;
TASK-045 is now **READY** (dependency satisfied); TASK-046..047 remain **PLANNED** with explicit
`depends_on` chain.

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
- **TASK-043** — DONE (2026-06-19T19:00:00Z). Http engine contracts + fake-transport seam approved unanimously (4/4).
  Commits: `547f2f8` (impl) + `e1e87d0` (simplify). Review artifacts: `nazgul/reviews/TASK-043/`.
- **TASK-044** — DONE (2026-06-19T23:45:00Z). Http reconnecting byte-engine approved unanimously (4/4, Cycle 2).
  Commit: `501ad13`. Completion SHA: `8e5021c`. Review artifacts: `nazgul/reviews/TASK-044/`.

## Recovery Pointer

- **Current stage**: ✦ OBJECTIVE COMPLETE — **NAZGUL_COMPLETE**. FEAT-005 WebSocket streaming v1
  shipped via PR #26 (squash `5a50a8b` on `main`). All closeout bookkeeping tasks (051–055) also
  shipped to `main` via PRs #28/#30/#31/#32/#33 and reviewed on GitHub.
- **Next action**: none — all tasks (042–055) DONE. Objective fully closed; ready for next objective
  (`/nazgul:plan`).
- **Active task**: none.
- **Files are truth**: task manifests in `nazgul/tasks/TASK-042..055.md` carry full state;
  frontmatter `status:` is canonical (all DONE). 051–055 reconciled to DONE from merged-PR evidence.

─── ◈ NEXT ─────────────────────────────────────────────
  ✦ TASK-042 — Core streaming abstractions. DONE.
  ✦ TASK-043 — Http engine contracts + fake-transport seam. DONE.
  ✦ TASK-044 — Http reconnecting byte-engine. DONE.
  ✦ TASK-045 — Generic StreamClient + factory + AddStreams<TOptions>. IMPLEMENTED (906c568).
  ✦ TASK-046 — Exchange-#1 streaming package. IMPLEMENTED (27169ea).
  ✦ TASK-047 — Wire 4 subscribe methods end-to-end + integration smoke. IMPLEMENTED (58d5216).
  ✦ TASK-049 — Fix routing-key keyspace mismatch + liveness reset. IMPLEMENTED (5195052).
────────────────────────────────────────────────────────
