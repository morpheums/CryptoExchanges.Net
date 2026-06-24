# Nazgul Plan — FEAT-008

## Recovery Pointer
**Active task**: FEAT-008 COMPLETE — all 4/4 tasks DONE; consolidated review APPROVED 4/4 (`nazgul/reviews/FEAT-008-consolidated/`; one HIGH code-review finding fixed in `9866a21` and re-approved). Post-loop + PR pending.
**Next action**: Run post-loop (documentation, release-manager) then open PR `feat/FEAT-008-stream-control-msg-rate-limit` → `main`. Both venues PASS the multi-symbol live L2 regression (Binance 18 / KuCoin 14). Build 0W/0E; non-integration suite green (754 tests). Scope grew by one user-approved task: TASK-074 fixed a pre-existing Binance combined-stream decode bug (data-envelope unwrap) — the second cause of "no Binance order book delivered". Commits: TASK-071 `2d2a3aa`, TASK-072 `4563126`, TASK-073 `dc49327`, TASK-074 `da818db` + review-fix `9866a21`.

─── ◈ NAZGUL ▸ PLANNING ────────────────────────────────

## Objective

**FEAT-008 — Fix the multi-symbol WebSocket streaming burst that Binance rejects with PolicyViolation.**
The shared `StreamEngine` sends one control frame per subscription with no pacing. Subscribing to N
symbols fires an N-frame burst; Binance enforces 5 inbound msgs/sec and closes the socket (~300 ms,
before any data) with PolicyViolation "Too many requests". `ReconnectCoreAsync` then replays the whole
`_subscribeSet` as another burst on every reconnect → infinite reconnect loop, zero order-book data.
KuCoin shares the design (more lenient). Single-symbol works, so existing single-symbol smoke tests
never caught it. Transport/wire/URL/keep-alive are healthy and **out of scope**.

The fix is two composing steps, both required: **(1) per-venue outbound throttling** —
`StreamConnectionInfo.MinOutboundInterval` + a serialized, paced `SendControlAsync` in `StreamEngine`
that every send site routes through; **(2) batched reconnect-replay** — additive `IStreamProtocol`
batch builders (default-null) implemented for Binance/KuCoin, with a chunked batched-replay path in
`ReconnectCoreAsync` that still sends each frame through `SendControlAsync`. Plus a multi-symbol live
L2 regression test for both venues. **Zero public API change** — every type touched is `internal`;
public `IStreamClient` is unchanged.

**Objective type**: Bug fix (correctness) + scale optimisation. Brownfield. No public surface change.

**Approach is architect-validated — do NOT re-evaluate.** Full pre-implementation advisory (APPROVED):
`nazgul/reviews/FEAT-008/architect-reviewer.md`. The design below is mandated, not a proposal.

Authoritative inputs (read fully before any task):
- Objective: `nazgul/config.json` → `objectives_history[FEAT-008].objective`.
- Architect advisory (mandated design): `nazgul/reviews/FEAT-008/architect-reviewer.md`.

## Branch

- **Base**: `main` (protected — ship via PR).
- **Feature**: `feat/FEAT-008-stream-control-msg-rate-limit` (already created + checked out).
- **Commit prefix**: `feat(FEAT-008):`.

## Hard Constraints (recorded for implementer + reviewers)

- **Zero public API change.** `IStreamProtocol`, `StreamConnectionInfo`, `StreamEngineOptions`,
  `StreamEngine` are all `internal`; public `IStreamClient` (Core) is untouched. The rate-limit value
  lives on `StreamConnectionInfo` (a venue property, like `HeartbeatPolicy`) — NOT `StreamEngineOptions`
  (a consumer setting that could break exchange policy).
- **One type per file.** No new top-level types are expected; the new members are a private method
  (`SendControlAsync`), a record parameter (`MinOutboundInterval`), and two interface default members.
- **LEAN comments** — only for non-obvious venue quirks (e.g. the 5/sec Binance cap, the ping→send-
  semaphore race fix). No banner separators, no restating code. Full XML docs on the new **interface**
  members (incl. defaults); `<inheritdoc/>` on impls.
- **Build 0W/0E** every task: `dotnet build CryptoExchanges.Net.sln` under `TreatWarningsAsErrors`,
  `AnalysisLevel=latest-all`, `GenerateDocumentationFile=true`. Mind CA2007 (ConfigureAwait), CA1031
  (the intentional broad catches are already `#pragma`-suppressed — keep that pattern), CA2213
  (dispose `_sendSemaphore`).
- **New logging** uses `LoggerMessage` delegates following the existing `s_log*` pattern.
- **Strict layering Core → Http → Exchange → DI.** The pacing primitive + batch **dispatch** live in
  Http (`StreamEngine` + `StreamConnectionInfo` + `IStreamProtocol`); per-venue **values** (200 ms /
  bullet-derived) + batch **wire format** live in each exchange protocol.
- **Composition:** batched replay frames are still sent through `SendControlAsync`, so they remain
  throttled. 300-symbol replay → ~3 frames × 200 ms ≈ 600 ms.
- **Tests mandatory, gated:** fast unit tests (fakes, no network) prove pacing/serialization/batching/
  chunking/zero-preserves-behavior; the multi-symbol live regression is `[Trait("Category","Integration")]`
  and excluded from `dotnet test --filter 'Category!=Integration'`, self-skipping offline.
- Non-integration suite stays green after every task.

## Status Summary

| Task     | Status     | Wave | Description                                                                  |
|----------|------------|------|------------------------------------------------------------------------------|
| TASK-071 | ✦ DONE     | 1    | `StreamConnectionInfo.MinOutboundInterval` + `SendControlAsync` (throttle/serialize) + route all send sites + Binance 200ms/KuCoin 100ms values + unit tests. Review ✦ APPROVED (4/4). Commits `2d2a3aa` + simplify `a45059f6`. |
| TASK-072 | ◆ IMPLEMENTED | 2 | `IStreamProtocol` batch builders (default-null) + Binance/KuCoin impls + chunked batched `ReconnectCoreAsync` replay + unit tests. Impl commit `4563126`; build 0W0E, non-integration suite green. Awaiting review gate. |
| TASK-073 | ◆ IMPLEMENTED | 3 | Multi-symbol Binance (18) + KuCoin (14) L2 order-book LIVE regression test. KuCoin live PASS; Binance self-skips (pre-existing decode bug). Impl commit `dc49327`. Awaiting review gate. |
| TASK-074 | ◆ IMPLEMENTED | 4 | Fix Binance combined-stream `data`-envelope decode (mirror KuCoin `DeserializeData<T>`; OrderBook symbol from `stream` token for partial-book `@depthN`) + restore clean reachability probe + envelope-level unit tests. Binance live multi-symbol now PASSES (real book); KuCoin still PASS. Impl commit `da818db`; build 0W0E, non-integration green. Awaiting review gate. |

Tasks: 1/4 DONE. ◆ TASK-072 + TASK-073 + TASK-074 IMPLEMENTED (awaiting review gate).

## Wave Groups

The loop orchestrator reads this section to determine parallel execution order. Tasks in the same wave
have NO dependency on each other AND NO file overlap. FEAT-008 is a single dependency chain (each task
edits `StreamEngine.cs` and the two protocol files, and each builds on the prior step), so every wave
holds exactly one task and runs sequentially.

### Wave 1
- **TASK-071** — Throttling primitive (`MinOutboundInterval`) + `SendControlAsync` + route every send
  site + per-venue interval values + unit tests. No deps. Touches `StreamConnectionInfo.cs`,
  `StreamEngine.cs`, both protocol files, `StreamEngineTests.cs`. **DONE.**

### Wave 2
- **TASK-072** — Batch builders + chunked batched reconnect replay + unit tests. **Depends on
  TASK-071** (`SendControlAsync` is the dispatch path for batched frames). File overlap with TASK-071
  on `StreamEngine.cs` + both protocol files → must follow sequentially. **READY.**

### Wave 3
- **TASK-073** — Multi-symbol live L2 regression test (both venues). **Depends on TASK-071 + TASK-072**
  (the fix must be in place to pass). Tests-only; no file overlap with 071/072 (separate integration
  test projects), but logically gated on the fix being complete.

## Dependency Order

```
TASK-071 -> TASK-072 -> TASK-073
```

(TASK-072 depends on TASK-071's `SendControlAsync`; TASK-073 depends on both 071 and 072. Single-lane
sequential execution avoids `StreamEngine.cs` / protocol-file contention and keeps every boundary
green and individually reviewable.)

## Traceability (objective -> tasks)

The FEAT-008 objective decomposes into three verifiable outcomes; each maps to a task:

- **"respect a per-venue message-rate limit (pacing)"** → TASK-071 (`MinOutboundInterval` +
  `SendControlAsync`; Binance 200 ms / KuCoin bullet-or-default; serialize all sends, fixing the
  KuCoin concurrent-send race).
- **"batching ... per exchange" for the reconnect replay** → TASK-072 (`IStreamProtocol` batch
  builders + chunked batched `ReconnectCoreAsync`, frames still throttled via `SendControlAsync`).
- **"multi-symbol Binance+KuCoin L2 order-book regression test asserting ≥1 book update is
  delivered"** → TASK-073 (≥17 Binance / ≥13 KuCoin live L2 streams; reproduces the original burst
  failure; Integration-gated + self-skip).
- **"do not change transport/wire/URL/keep-alive"** → out of scope in all tasks (no edits to those
  layers).

Architect advisory mapping: Step 1 (Throttling) → TASK-071; Step 2 (Batching) → TASK-072; Bug-confirmed
reproducer → TASK-073. Risk register: concurrent-send + dispose-during-delay → TASK-071;
reconnect-replay burst → TASK-072.

## Completed

- **TASK-071** — Throttle + serialize outbound control frames in `StreamEngine`. DONE 2026-06-23.
  Review gate ✦ APPROVED (architect / code / security / api — all 4). Pre-checks: test 197✦ / lint 0W0E✦ /
  build 0W0E✦ / smoke n-a. Commits `2d2a3aa` (impl) + `a45059f6` (per-task simplify). Evidence:
  `nazgul/reviews/TASK-071/{architect,code,security,api}-reviewer.md`.

## Recovery Pointer

- **Current stage**: Loop — TASK-071 DONE; TASK-072 IMPLEMENTED; TASK-073 IMPLEMENTED (Wave 3), awaiting review gate.
- **Next action**: Run the review gate for TASK-072 (`4563126`) and TASK-073 (`dc49327`) on branch `feat/FEAT-008-stream-control-msg-rate-limit`.
- **Active task**: TASK-073.
- **Files are truth**: `nazgul/tasks/TASK-071..073.md` carry full state; each manifest's frontmatter
  `status:` is the canonical record.

─── ◈ NEXT ─────────────────────────────────────────────
  ◆ TASK-072 — Batched reconnect-replay (`IStreamProtocol` batch builders + chunked replay).
  ◇ TASK-073 — Multi-symbol live L2 regression test (after 072).
────────────────────────────────────────────────────────

## Patches

- [x] PATCH-003: Fix reconnect-vs-subscribe socket-disposal race in StreamEngine (sha: d970e91)
- [x] PATCH-004: Await fire-and-forget reconnect task on dispose (CI follow-up to PATCH-003) (sha: 46e1608)
- [x] PATCH-005: Release prep for v0.5.0-preview.3 (sha: b22d3c1)
