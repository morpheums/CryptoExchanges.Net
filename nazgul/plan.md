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
| TASK-057 | ✦ DONE     | 2    | KC-API passphrase-v2 signing service + mark-and-strip signing handler        |
| TASK-058 | ✦ DONE     | 2    | Bespoke `ISymbolMapper` + REST wire DTOs + DeltaMapper profiles + parsers    |
| TASK-059 | ✦ DONE     | 3    | REST services (market/account/trading) + http client + composer + entry     |
| TASK-060 | ✦ DONE     | 4    | `AddKucoinExchange` DI + `AddCryptoExchanges` + MCP wiring                   |
| TASK-062 | ✦ DONE     | 5    | `KucoinStreamProtocol` + bullet-public + 4 decoders + `AddKucoinStreams`     |
| TASK-063 | ✦ DONE     | 6    | Live integration smokes — REST + one streaming (self-skip)                   |
| TASK-064 | ✦ DONE     | 6    | Docs — README KuCoin row → supported + MCP/exchanges/streaming reference     |

Tasks: 9/9 DONE.

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

- **Current stage**: ✦ ALL TASKS DONE — FEAT-006 complete. TASK-064 DONE (commit 76a2798). Awaiting post-loop.
- **Next action**: Post-loop phase (documentation, release-manager agents) → PR to main.
- **Active task**: none (all 9/9 DONE).
- **Files are truth**: the task manifests under `nazgul/tasks/` carry full state; each manifest's
  frontmatter is the canonical record.

─── ◈ NEXT ─────────────────────────────────────────────
  ✦ TASK-056 — Scaffold complete; DONE (reviewed 4/4, commit 2b9c308).
  ✦ TASK-061 — ADR-002 seam generalization DONE (reviewed 4/4, commit f04dfc4).
  ✦ TASK-057 — KC-API signing DONE (reviewed 4/4, Cycle 2; simplify 4799140).
  ✦ TASK-058 — KucoinSymbolMapper + wire DTOs + DeltaMapper + parsers DONE (reviewed 4/4, Cycle 1; simplify 5a20da1).
  ✦ TASK-059 — REST services + http client + composer + entry DONE (reviewed 4/4, Cycle 2; fix ee97d43).
  ✦ TASK-060 — AddKucoinExchange DI + AddCryptoExchanges + MCP wiring DONE (reviewed 4/4, Cycle 1; impl commit ad607d6).
  ✦ TASK-062 — KucoinStreamProtocol + bullet-public + 4 decoders + AddKucoinStreams DONE (reviewed 4/4, Cycle 2; completion 2039654).
  ✦ TASK-063 — Live integration smokes (REST + streaming, self-skip) DONE (reviewed 4/4, Fix-First Cycle 1; completion b365dbb).
  ✦ TASK-064 — Docs (README/MCP/exchanges/streaming) DONE (reviewed 4/4, Fix-First Cycle 1 + Cycle 2; completion 76a2798).
  ◇ POST-LOOP — documentation + release-manager → PR to main.
────────────────────────────────────────────────────────

## Completed

- **TASK-056** — DONE (2026-06-21T01:30:00Z). KuCoin scaffold approved unanimously (4/4).
  Impl commit: `2b9c308`. Completion SHA: `40ab130`. Review artifacts: `nazgul/reviews/TASK-056/`.
- **TASK-061** — DONE (2026-06-21T05:00:00Z). ADR-002 streaming endpoint seam approved unanimously (4/4).
  Impl commit: `f25dc9d`. Simplify commit: `f04dfc4`. Completion SHA: `f04dfc4`. Review artifacts: `nazgul/reviews/TASK-061/`.
- **TASK-057** — DONE (2026-06-21T09:48:13Z). KC-API passphrase-v2 signing approved unanimously (4/4, Cycle 2).
  Impl commits: `a754e9f` (initial) + `d3bf817` (DIP fix) + `4799140` (simplify). Completion SHA: ffc7e3f. Review artifacts: `nazgul/reviews/TASK-057/`.
- **TASK-058** — DONE (2026-06-21T11:00:00Z). KucoinSymbolMapper + wire DTOs + DeltaMapper profiles + parsers approved unanimously (4/4, Cycle 1).
  Impl commit: `c59600f`. Simplify commit: `5a20da1`. Review artifacts: `nazgul/reviews/TASK-058/`.
- **TASK-059** — DONE (2026-06-21T12:30:00Z). REST services + http client + composer + entry approved unanimously (4/4, Cycle 2).
  Impl commit: `95a6066`. Simplify commits: `dc8aac9` + `272ded8` + `12fffb6`. Cycle-1 fix: `ee97d43`. Completion SHA: `ee97d43`. Review artifacts: `nazgul/reviews/TASK-059/`.
- **TASK-060** — DONE (2026-06-21T11:33:29Z). AddKucoinExchange DI + AddCryptoExchanges + MCP wiring approved unanimously (4/4, Cycle 1).
  Impl commit: `ad607d6`. Completion SHA: `0940957`. Simplify: 0 fixes (faithful clone). No blocking findings (all CONCERNs <=65, pre-existing cross-exchange patterns).
  Flake note: Http.Tests.Unit streaming-reconnect race is a pre-existing parallel-run harness race (87/87 PASS in isolation), NOT a TASK-061 seam regression. Review artifacts: `nazgul/reviews/TASK-060/`.
- **TASK-062** — DONE (2026-06-21T13:15:00Z). KucoinStreamProtocol + bullet-public negotiation + 4 decoders + AddKucoinStreams approved unanimously (4/4, Cycle 2).
  Impl commit: `af4d08a`. Simplify commit: `d6988f7` (6 fixes). Cycle-1 fix (RestBaseUrl wiring) + completion SHA: `2039654`. Review artifacts: `nazgul/reviews/TASK-062/`.
  Cycle 1: api-reviewer CHANGES_REQUESTED (RestBaseUrl public option silently ignored, REJECT@98%); Fix-First auto-remediation wired RestBaseUrl→bullet-public HttpClient BaseAddress (LR-001 guards) + 4 no-network wiring tests; api-reviewer Cycle 2 APPROVE@99%. Security SSRF (deferred to this task) RESOLVED — wss:// + *.kucoin.com host allowlist enforced pre-URI. Wave 5 convergence (REST + streaming-seam) complete.
- **TASK-063** — DONE (2026-06-21T15:00:00Z). Live integration smokes (REST + streaming, self-skip) — 4/4 reviewers, Fix-First Cycle 1.
  Impl commit: `5dc88fa`. Fix commit: `b365dbb` (removed <remarks> noise + dead reconnect booleans). Completion SHA: `b365dbb`. Review artifacts: `nazgul/reviews/TASK-063/`.
  Cycle 1: code-reviewer CHANGES_REQUESTED (2 REJECTs: <remarks> violates LEAN mandate, dead boolean locals); Fix-First auto-remediated both mechanically. Build 0W/0E post-fix; all tests green. architect/security/api all APPROVE in first cycle.
- **TASK-064** — DONE (2026-06-21T16:00:00Z). Docs KuCoin promotion (README, exchanges.md, mcp-server.md, streaming.md) + MCP kucoin wiring fix — 4/4 reviewers, Fix-First Cycle 1 + Cycle 2.
  Impl commit: `425b66b`. Fix commit: `d54e9f1` (ToolInputs.cs + EnvCredentialBinder.cs kucoin wiring). Completion SHA: `76a2798`. Review artifacts: `nazgul/reviews/TASK-064/`.
  Cycle 1: code-reviewer CHANGES_REQUESTED (REJECT@95% — mcp-server.md claimed kucoin as valid exchange key but ToolInputs + EnvCredentialBinder missing KuCoin routing — gap from TASK-060); Fix-First auto-remediated both files mechanically; 783 tests pass; Cycle 2 code-reviewer APPROVE. architect/security/api all APPROVE in first cycle.


## Archived — FEAT-005 (WebSocket streaming v1) — COMPLETE

> Preserved below for history. FEAT-001..004 archived under `nazgul/archive/`. Active objective is FEAT-006 (above).
