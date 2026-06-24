# Nazgul Plan — FEAT-009

## Recovery Pointer
**Active task**: TASK-076 (READY) — BybitStreamProtocol + protocol unit tests. TASK-075 is DONE: reviewed (4 verdicts in `nazgul/reviews/TASK-075/`), the one blocking finding (missing Bybit `turnover` field) fixed in commit 4fe3ff2; trivial one-line re-review skipped per maintainer's consolidated-review preference. Build 0W/0E, all tests green. 15 tasks across 3 exchange groups; strict sequential merge order Bybit → OKX → Bitget (one PR per exchange).
**Next action**: Implement TASK-076 (BybitStreamProtocol: subscribe/unsubscribe/batch wire format, Classify, RoutingKeyFor, ResolveConnectionAsync, HeartbeatPolicy, MinOutboundInterval — confirm Bybit v5 heartbeat direction + order-book depth) with protocol unit tests mirroring BinanceStreamProtocolTests/KucoinStreamProtocolTests.

─── ◈ NAZGUL ▸ PLANNING ────────────────────────────────

## Objective

**FEAT-009 — Public WebSocket market-data streaming for Bybit, OKX, and Bitget.**
Add ticker / trade / order-book (L2) / kline streams to the three REST-only exchanges, in strict
priority order (1) Bybit, (2) OKX, (3) Bitget, reaching full parity with the existing Binance/KuCoin
streaming clients through the same public `IStreamClient` surface via opt-in `AddXxxStreams()` DI
extensions. Clone the verified shared-streaming design exactly — **no new transport or engine**. The
shared `StreamEngine` + `StreamServiceRegistration.AddStreams<TOptions>()` in `CryptoExchanges.Net.Http`
is reused UNCHANGED. Each exchange supplies only its five variation points: `XxxStreamOptions`,
`XxxStreamProtocol : IStreamProtocol`, `XxxStreamDecoders` (DeltaMapper DTO→Core.Models closures),
internal `{Concept}Dto` wire DTOs under `Dtos/Streaming/`, and the `AddXxxStreams()` extension.

Spot-only, public streams only, delta callbacks only (no local order-book maintenance). Zero breaking
change to `IStreamClient`/`IStreamClientFactory`. One PR per exchange, merged in order (Bybit before OKX,
OKX before Bitget).

**Objective type**: Feature parity / brownfield clone. No public API surface change. No shared-engine change.

Authoritative inputs (read fully before any task):
- Objective: `nazgul/config.json` → `objectives_history[FEAT-009].objective`.
- PRD: `nazgul/docs/PRD-FEAT-009.md` — acceptance criteria + success metrics.
- TRD: `nazgul/docs/TRD-FEAT-009.md` — variation points + per-venue WS specifics + file layout.
- ADRs: `nazgul/docs/ADR-FEAT-009.md` — ADR-009-001…006.
- Test plan: `nazgul/docs/TEST-PLAN-FEAT-009.md` — per-exchange test breakdown.

## Branch

- **Base**: `main` (protected — ship via PR; squash-merge).
- **Feature**: `feat/FEAT-009-ws-streaming-bybit-okx-bitget`.
- **Commit prefix**: `feat(FEAT-009):`.
- **Merge model (ADR-009-005)**: ONE PR per exchange. Bybit PR merges to `main` first; the OKX work
  re-branches from updated `main` and ships its own PR; Bitget re-branches from `main` after OKX and
  ships the final PR. Hard sequential dependency — no cross-exchange parallelism.

## Hard Constraints (recorded for implementer + reviewers)

- **K1**: `CryptoExchanges.Net.Http` never references `Core.Models` or DeltaMapper. Decode closures are
  opaque `Func<ReadOnlyMemory<byte>, object>` living in each exchange package.
- **K2**: Subscribe-set replay on reconnect — protocol subscribe/batch frames must be idempotent (the
  engine replays them, paced).
- **K3**: The engine owns the reconnect backoff loop. Protocols do not implement reconnect.
- **C1**: Heartbeat execution (timers/watchdog/send) lives in the engine. Protocols only DESCRIBE the
  policy via `HeartbeatPolicy` returned from `ResolveConnectionAsync`.
- **No shared-engine change.** `StreamEngine`, `IStreamProtocol`, `StreamConnectionInfo`,
  `StreamServiceRegistration`, `StreamDecoderRegistry` are consumed read-only.
- **`data`-envelope unwrap (FEAT-008 / TASK-074 lesson).** Decoders MUST unwrap the venue's data
  element before deserializing the leaf DTO — never deserialize the raw envelope directly.
- **House rules.** One type per file; canonical `{Concept}Dto` names (vendor vocabulary only in
  `[JsonPropertyName]`); LEAN comments (only non-obvious venue quirks); full XML docs on public members
  + `<inheritdoc/>` on impls.
- **Build 0W/0E** every task under `TreatWarningsAsErrors` + `AnalysisLevel=latest-all` +
  `GenerateDocumentationFile=true`; `dotnet test --filter 'Category!=Integration'` green before review.

## Per-Venue Implementor-Confirm Items (TRD flags — carried on the protocol tasks)

- **Bybit (TASK-076)**: heartbeat direction + ping interval; order-book depth levels (1/50/200) + chosen
  default; `type:snapshot` vs `delta` does not change `Classify` routing.
- **OKX (TASK-081)**: `books5` (top-5) vs `books` (full) preferred order-book channel + chosen default;
  exact text ping/pong + interval (bare-text `pong` Classify branch).
- **Bitget (TASK-086)**: heartbeat direction (control-frame Ping vs text `"ping"`/`"pong"`); order-book
  channel names + depth levels + chosen default; kline channel naming.

Each implementor records the confirmed values in the protocol class summary and the test assertions.

## Status Summary

Tasks: 0/15 complete. TASK-075 in retry cycle (1/3). Groups 2 (OKX) + 3 (Bitget) gated on prior merges.

| Task     | Status | Description                                   |
|----------|--------|-----------------------------------------------|
| TASK-075 | ⚠ CHANGES_REQUESTED | Bybit DTOs + BybitStreamOptions    |
| TASK-076 | ◇      | BybitStreamProtocol + protocol tests          |
| TASK-077 | ◇      | BybitStreamDecoders + decode tests            |
| TASK-078 | ◇      | AddBybitStreams() DI + DI tests               |
| TASK-079 | ◇      | Bybit multi-symbol L2 smoke (+ Bybit PR)      |
| TASK-080 | ◇      | OKX DTOs + OkxStreamOptions                    |
| TASK-081 | ◇      | OkxStreamProtocol (text-ping/pong) + tests     |
| TASK-082 | ◇      | OkxStreamDecoders + decode tests               |
| TASK-083 | ◇      | AddOkxStreams() DI + DI tests                  |
| TASK-084 | ◇      | OKX multi-symbol L2 smoke (+ OKX PR)           |
| TASK-085 | ◇      | Bitget DTOs + BitgetStreamOptions              |
| TASK-086 | ◇      | BitgetStreamProtocol + protocol tests          |
| TASK-087 | ◇      | BitgetStreamDecoders + decode tests            |
| TASK-088 | ◇      | AddBitgetStreams() DI + DI tests               |
| TASK-089 | ◇      | Bitget multi-symbol L2 smoke (+ Bitget PR)     |

## Task Groups (dependency-ordered, strict sequential merge order)

### Group 1 — Bybit (PR #1 to `main`, merged FIRST)
- **TASK-075** — wire DTOs + `BybitStreamOptions`. Depends on: none. **CHANGES_REQUESTED (retry 1/3).**
- **TASK-076** — `BybitStreamProtocol : IStreamProtocol` + protocol unit tests. Depends on: TASK-075.
- **TASK-077** — `BybitStreamDecoders` + decode unit tests. Depends on: TASK-075, TASK-076.
- **TASK-078** — `AddBybitStreams()` DI extension + DI wiring tests. Depends on: TASK-076, TASK-077.
- **TASK-079** — Bybit multi-symbol L2 integration smoke test; **opens the Bybit PR to `main`.**
  Depends on: TASK-078.

### Group 2 — OKX (PR #2 to `main`, gated on Bybit merge)
- **TASK-080** — wire DTOs + `OkxStreamOptions`. Depends on: TASK-079 (Bybit merged; branch from `main`).
- **TASK-081** — `OkxStreamProtocol` (client text-ping + bare-text-`pong` Classify) + protocol tests.
  Depends on: TASK-080.
- **TASK-082** — `OkxStreamDecoders` (`data`-array + positional kline + symbol-from-`arg.instId`) +
  decode tests. Depends on: TASK-080, TASK-081.
- **TASK-083** — `AddOkxStreams()` DI extension + DI wiring tests. Depends on: TASK-081, TASK-082.
- **TASK-084** — OKX multi-symbol L2 integration smoke test; **opens the OKX PR to `main`.**
  Depends on: TASK-083.

### Group 3 — Bitget (PR #3 to `main`, gated on OKX merge; final)
- **TASK-085** — wire DTOs + `BitgetStreamOptions`. Depends on: TASK-084 (OKX merged; branch from `main`).
- **TASK-086** — `BitgetStreamProtocol` (`instType:SPOT` args, confirmed heartbeat) + protocol tests.
  Depends on: TASK-085.
- **TASK-087** — `BitgetStreamDecoders` (`action`+`data`-array + positional kline) + decode tests.
  Depends on: TASK-085, TASK-086.
- **TASK-088** — `AddBitgetStreams()` DI extension + DI wiring tests. Depends on: TASK-086, TASK-087.
- **TASK-089** — Bitget multi-symbol L2 integration smoke test; **opens the Bitget PR to `main`** (final).
  Depends on: TASK-088.

## Dependency / Merge Ordering

```
TASK-075 → TASK-076 → TASK-077 → TASK-078 → TASK-079  ──[Bybit PR merges to main]──┐
                                                                                    ▼
TASK-080 → TASK-081 → TASK-082 → TASK-083 → TASK-084  ──[OKX PR merges to main]──┐
                                                                                  ▼
TASK-085 → TASK-086 → TASK-087 → TASK-088 → TASK-089  ──[Bitget PR merges to main]
```

Within each exchange, the DTO task is the root; protocol and decoders both depend on the DTOs;
DI depends on protocol + decoders; the integration smoke test depends on DI and opens the PR. The
first task of each later exchange depends on the prior exchange's smoke/PR task, encoding the strict
merge order from ADR-009-005.

## Wave Groups

**None.** ADR-009-005 mandates a hard sequential merge order across exchanges (one PR per exchange,
each merged to `main` before the next begins), and within an exchange the five tasks form a linear
dependency chain (DTOs → protocol/decoders → DI → smoke). There are no two tasks that may safely run
in parallel, so no wave groups are defined — execution is strictly sequential TASK-075 → … → TASK-089.

## PRD Traceability Check

- **PRD AC#1** (live Bybit Ticker after `AddBybitExchange().AddBybitStreams()`): TASK-075, 076, 077, 078, 079.
- **PRD AC#2** (all four stream kinds for OKX + Bitget too): TASK-080–084 (OKX), TASK-085–089 (Bitget),
  plus Bybit group for the four kinds.
- **PRD AC#3** (10+ symbols, no reconnect loop — FEAT-008 regression): TASK-079, TASK-084, TASK-089
  (multi-symbol L2 smoke per venue).
- **PRD AC#4** (`dotnet test --filter 'Category!=Integration'` exits 0 before each PR): build/test gate
  on every task; explicitly on the smoke tasks 079/084/089 before each PR.
- **Success Metrics** (≥1 OrderBook within 30 s, multi-symbol; zero Binance/KuCoin regression; each PR
  passes reviewers + merges cleanly): the three smoke tasks + the non-modification scope guardrail
  (no existing Binance/KuCoin test files touched).

Every PRD acceptance criterion maps to at least one task. No criterion uncovered.
