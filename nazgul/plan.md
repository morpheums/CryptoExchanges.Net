# Nazgul Plan — FEAT-007

## Recovery Pointer
**Active task**: TASK-069 (IN_PROGRESS) — Docs + CHANGELOG + version bump → `0.5.0-preview.1`
**Next action**: Edit all consumer-facing docs + CHANGELOG + Directory.Build.props; then set IMPLEMENTED.

─── ◈ NAZGUL ▸ PLANNING ────────────────────────────────

## Objective

**FEAT-007 — Rename DI aggregator → root meta-package `CryptoExchanges.Net`.** Rename the
all-exchanges DI aggregator from `CryptoExchanges.Net.DependencyInjection` to the bare root id
`CryptoExchanges.Net` so the "install one package → get all exchanges + one-call
`AddCryptoExchanges()`" bundle is honestly named and discoverable. Move `AddCryptoExchanges` +
`CryptoExchangesOptions` to the `CryptoExchanges.Net` namespace (method name + options shape
unchanged). Decouple the five per-exchange `.Tests.Unit` projects from the aggregator and consolidate
the all-exchanges resolution test into the renamed `CryptoExchanges.Net.Tests.Unit`. Repoint MCP +
samples + sln. Bump to `0.5.0-preview.1`; published set stays 9 (DI out, `CryptoExchanges.Net` in).
Clean swap — no consumers yet, no shim. **No runtime behavior change.**

**Objective type**: Refactor (brownfield package rename + namespace move + test decoupling). Public
surface changes only in package id and namespace.

Authoritative inputs (read fully before any task):
- Objective spec: `nazgul/context/objectives/FEAT-007-spec.md` (PRIMARY)
- `nazgul/docs/PRD-FEAT-007.md`, `nazgul/docs/TRD-FEAT-007.md`,
  `nazgul/docs/ADR-003-root-packageid-for-all-exchanges-meta-bundle.md`,
  `nazgul/docs/TEST-PLAN-FEAT-007.md`
- Approved design: `docs/superpowers/specs/2026-06-21-rename-di-aggregator-root-metapackage-design.md`

## Branch

- **Base**: `main` (protected — ship via PR).
- **Feature**: `feat/FEAT-007-root-metapackage` (to be created; current working branch is
  `feat/FEAT-006-kucoin`).

## Hard Constraints (recorded for implementer + reviewers)

- **4-layer chain** preserved (Core → Http → Exchange → DI/meta). The meta-package may reference all
  exchange layers; nothing downstream adds a new transitive dependency.
- **One type per file** for all moved/renamed source files.
- **LEAN comments + LEAN XML docs**: short `<summary>`, one `<param>` per parameter, `<exception>`
  where thrown; `<inheritdoc/>` on impls. The aggregator's two source files implement no interface, so
  they keep concrete-class doc comments (no `<inheritdoc/>`), and NO `<remarks>` (the LEAN mandate that
  bit FEAT-006).
- **Build 0W/0E**: `dotnet build CryptoExchanges.Net.sln` under `TreatWarningsAsErrors`,
  `AnalysisLevel=latest-all`, `GenerateDocumentationFile=true`.
- **Clean swap — NO shim / type-forwarder** (no consumers yet). Stop producing
  `…DependencyInjection`; nuget.org deprecate/unlist is a documented manual post-merge step, NOT a
  build action.
- **Method/options shape unchanged** — only the package id + namespace change. `AddCryptoExchanges`
  name and `CryptoExchangesOptions` properties are byte-stable.
- **Published set stays 9** — `…DependencyInjection` out, `CryptoExchanges.Net` in; `release.yml`
  packs by solution glob (no workflow change).
- **No opsec leakage** in public artifacts (README/CHANGELOG/commits/PR).
- Non-integration suite stays green after every task (`dotnet test --filter 'Category!=Integration'`);
  aggregator-resolution coverage exists exactly once.

## Real-code corrections vs. the docs (recorded for implementers)

- **All five** per-exchange `.Tests.Unit` csprojs reference the aggregator (line 15) — including
  Binance, which the TRD baseline said had no reference. Binance needs csproj-ref removal only (no
  aggregator `using`/test in its `.cs`). Handled in TASK-067.
- `samples/BasicUsage/Program.cs` uses `AddBinanceExchange` directly and imports
  `CryptoExchanges.Net.Binance`; it has NO aggregator `using` and NO `AddCryptoExchanges` call — only
  its csproj ProjectReference needs repointing (TASK-068).
- `docs/library-usage.md` also contains a `…DependencyInjection` reference (not enumerated in the TRD)
  — included in TASK-069 so AC-8 holds.
- `LoggingTest/` is a scratch project NOT in the solution and does not reference the aggregator —
  left untouched.

## Status Summary

| Task     | Status     | Wave | Description                                                                  |
|----------|------------|------|------------------------------------------------------------------------------|
| TASK-065 | ✦ IMPLEMENTED | 1    | Rename aggregator project → `CryptoExchanges.Net` (folder/csproj/ids/namespace + 2 src files + sln) |
| TASK-066 | ✦ IMPLEMENTED | 2    | Rename + consolidate aggregator test project → `CryptoExchanges.Net.Tests.Unit` (+ `AddCryptoExchangesTests`) |
| TASK-067 | ✦ IMPLEMENTED | 3    | Decouple the 5 per-exchange `.Tests.Unit` projects (drop ref + using + moved tests) |
| TASK-068 | ✦ IMPLEMENTED | 4    | Repoint consumers — MCP (src+tests), samples/BasicUsage, sln                  |
| TASK-069 | ◇ PLANNED  | 5    | Docs (README/NUGET/docs/*) + CHANGELOG + version bump → `0.5.0-preview.1`     |
| TASK-070 | ◇ PLANNED  | 6    | Final gate — build 0W/0E, suite green, `dotnet pack` 9-package swap verified  |

Tasks: 0/6 DONE.

## Wave Groups

The loop orchestrator reads this section to determine parallel execution order. Tasks in the same
wave have NO dependency on each other AND NO file overlap. For this small refactor there is **no
genuine parallelism**: the `.sln` is a shared file edited by steps 1, 2, and 4, and each step depends
serially on the previous one staying green. Every wave therefore holds exactly one task and runs
sequentially.

### Wave 1
- **TASK-065** — Rename the aggregator project (folder, csproj, ids, namespace, 2 source files) +
  its `.sln` project entry. No deps. Touches `src/CryptoExchanges.Net/*` + `CryptoExchanges.Net.sln`
  (line 10).

### Wave 2
- **TASK-066** — Rename + consolidate the aggregator test project; add the single
  `AddCryptoExchanges_ResolvesAllFiveExchanges` (+ options-flow) test. Depends on TASK-065 (refs the
  renamed src; edits the shared `.sln` line 28).

### Wave 3
- **TASK-067** — Decouple the 5 per-exchange test projects (remove aggregator ProjectReference +
  `using` + moved tests). Depends on TASK-066 (the consolidated test must exist before deleting the
  per-exchange copies). Touches 5 csprojs + 4 `.cs` files; does NOT touch the `.sln`.

### Wave 4
- **TASK-068** — Repoint MCP (src + tests), samples/BasicUsage, sln. Depends on TASK-066 (renamed src
  + sln baseline). Touches MCP/sample files; shares the `.sln` lineage → sequenced after 065/066.

### Wave 5
- **TASK-069** — Docs + CHANGELOG + version bump. Depends on TASK-068 (consumer-facing text accurate).
  Touches `Directory.Build.props` + docs + CHANGELOG only.

### Wave 6
- **TASK-070** — Final verification gate (build/test/pack/grep). Depends on TASK-067 + TASK-068 +
  TASK-069 (all product changes landed). Verification-only.

## Dependency Order

```
TASK-065 -> TASK-066 -> TASK-067 -+
                     \-> TASK-068 -+-> TASK-070
                                    |
            TASK-068 -> TASK-069 --+
```

(TASK-067 and TASK-068 both depend on TASK-066; TASK-069 depends on TASK-068; TASK-070 depends on
067 + 068 + 069. Executed single-lane sequentially 065 → 066 → 067 → 068 → 069 → 070 to avoid `.sln`
contention and keep each boundary green.)

## Traceability (PRD -> tasks)

Every PRD-FEAT-007 acceptance criterion maps to at least one task:

- **AC-1** (`dotnet pack` → 9 `.nupkg` incl. `CryptoExchanges.Net.0.5.0-preview.1`, none
  `…DependencyInjection`) -> TASK-069 (version), TASK-070 (pack verification).
- **AC-2** (single `AddCryptoExchanges_ResolvesAllFiveExchanges` test) -> TASK-066.
- **AC-3** (no `…DependencyInjection` project remains) -> TASK-065 (src), TASK-066 (test), verified
  TASK-070.
- **AC-4** (per-exchange `.Tests.Unit` has no aggregator ref/using; `AddXxxExchange` tests pass) ->
  TASK-067.
- **AC-5** (MCP + MCP tests + samples reference `CryptoExchanges.Net`; MCP resolves all) -> TASK-068.
- **AC-6** (0W/0E) -> acceptance criterion on EVERY task; final gate TASK-070.
- **AC-7** (non-integration suite green; coverage exactly once) -> TASK-066 (consolidate) + TASK-067
  (remove dups); verified TASK-070.
- **AC-8** (README/NUGET/docs/CHANGELOG reference `CryptoExchanges.Net`) -> TASK-069.

Nothing in PRD "Out of Scope" (shim/type-forwarder, `AddXxxExchange`/signing/mapping/streaming
changes, plugin auto-discovery, method/options-shape change, the manual nuget.org unlist) is planned.

## Recovery Pointer

- **Current stage**: TASK-067 IMPLEMENTED (commit `31207d5`). Awaiting review gate.
- **Next action**: Review gate for TASK-067; then begin TASK-068 (repoint MCP + samples + sln).
- **Active task**: TASK-067 (IMPLEMENTED — pending review).
- **Files are truth**: the task manifests under `nazgul/tasks/TASK-065..070.md` carry full state; each
  manifest's frontmatter `status:` is the canonical record.

─── ◈ NEXT ─────────────────────────────────────────────
  ◆ TASK-065 — Rename aggregator project → CryptoExchanges.Net (Wave 1, no deps).
  ◇ TASK-066 — Rename + consolidate aggregator test project (Wave 2, after 065).
  ◇ TASK-067 — Decouple 5 per-exchange test projects (Wave 3, after 066).
  ◇ TASK-068 — Repoint MCP + samples + sln (Wave 4, after 066).
  ◇ TASK-069 — Docs + CHANGELOG + version 0.5.0-preview.1 (Wave 5, after 068).
  ◇ TASK-070 — Final build/test/pack verification gate (Wave 6, after 067+068+069).
────────────────────────────────────────────────────────

## Completed

- (none yet for FEAT-007)

---

## Archived — FEAT-006 (KuCoin Exchange Integration) — COMPLETE

> Preserved below for history. FEAT-006 shipped via PR #35 (9/9 tasks DONE, post-loop complete).
> FEAT-001..005 archived under `nazgul/archive/`. Active objective is FEAT-007 (above).

### FEAT-006 Objective

**FEAT-006 — KuCoin Exchange Integration (full parity: REST + WebSocket streaming).** Added KuCoin as
the 5th exchange at full parity: REST market data + account + trading (KC-API passphrase-v2 HMAC
signing, bespoke `ISymbolMapper` for `BTC-USDT`, DeltaMapper DTO→model, `AddKucoinExchange` DI, MCP
wiring) plus public WebSocket streaming (ticker/trade/order book/kline) with auto-reconnect + token
re-negotiation + auto-resubscribe; generalized the shared streaming endpoint seam (ADR-002).

### FEAT-006 Status Summary

| Task     | Status     | Wave | Description                                                                  |
|----------|------------|------|------------------------------------------------------------------------------|
| TASK-056 | ✦ DONE     | 1    | Scaffold `CryptoExchanges.Net.Kucoin` + Unit/Integration test projects (OKX clone) |
| TASK-061 | ✦ DONE     | 1    | ADR-002 streaming endpoint seam → async `ResolveConnectionAsync` + migrate Binance |
| TASK-057 | ✦ DONE     | 2    | KC-API passphrase-v2 signing service + mark-and-strip signing handler        |
| TASK-058 | ✦ DONE     | 2    | Bespoke `ISymbolMapper` + REST wire DTOs + DeltaMapper profiles + parsers    |
| TASK-059 | ✦ DONE     | 3    | REST services (market/account/trading) + http client + composer + entry     |
| TASK-060 | ✦ DONE     | 4    | `AddKucoinExchange` DI + `AddCryptoExchanges` + MCP wiring                   |
| TASK-062 | ✦ DONE     | 5    | `KucoinStreamProtocol` + bullet-public + 4 decoders + `AddKucoinStreams` (bugfix: snapshot channel) |
| TASK-063 | ✦ DONE     | 6    | Live integration smokes — REST + one streaming (self-skip)                   |
| TASK-064 | ✦ DONE     | 6    | Docs — README KuCoin row → supported + MCP/exchanges/streaming reference     |

FEAT-006 Tasks: 9/9 DONE. PR #35. Completion SHAs recorded in each `nazgul/tasks/TASK-056..064.md`.

### FEAT-006 Completed (key SHAs)

- **TASK-056** — DONE. Impl `2b9c308`, completion `40ab130`.
- **TASK-061** — DONE. Impl `f25dc9d`, simplify/completion `f04dfc4`.
- **TASK-057** — DONE (Cycle 2). Impl `a754e9f` + `d3bf817` + simplify `4799140`, completion `ffc7e3f`.
- **TASK-058** — DONE (Cycle 1). Impl `c59600f`, simplify `5a20da1`.
- **TASK-059** — DONE (Cycle 2). Impl `95a6066`, simplifies `dc8aac9`/`272ded8`/`12fffb6`, fix/completion `ee97d43`.
- **TASK-060** — DONE (Cycle 1). Impl `ad607d6`, completion `0940957`.
- **TASK-062** — DONE (Bugfix Cycle 3). Impl `af4d08a`, simplify `d6988f7`, fixes `2039654`/`32f75f7`, completion `d34f1b8`.
- **TASK-063** — DONE (Fix-First Cycle 1). Impl `5dc88fa`, fix/completion `b365dbb`.
- **TASK-064** — DONE (Fix-First Cycle 2). Impl `425b66b`, fix `d54e9f1`, completion `76a2798`.
- **POST-LOOP** — documentation + release-manager → PR to main (#35).
