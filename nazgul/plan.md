# Nazgul Plan

## Objective
Add three new exchange integrations to CryptoExchanges.Net in strict priority order — **(1) Bybit, (2) OKX, (3) Bitget** — for a global (non-US) audience. Each exchange clones the verified Binance pattern (`Core → Http → Exchange → DI`, DeltaMapper DTO→model, bespoke `ISymbolMapper`, retry-only-on-GET, per-attempt re-signing). The signing/credential abstraction is generalized **after Bybit, against OKX** (passphrase-capable credential model + base64-vs-hex output + header-based signature), and Bitget then reuses that abstraction. See `nazgul/context/exchange-expansion-research.md` for rationale and signing deltas.

## Discovery Status
- [x] Discovery run: 2026-06-17T00:00:00Z
- [x] Classification: brownfield (HIGH confidence)
- [x] Reviewers generated: architect-reviewer, code-reviewer, security-reviewer, api-reviewer
- [x] Context collected: feature-scope (exchange-expansion)
- [x] Documents generated: none (PRD/TRD/ADR absent — research doc is the source of truth)

## Status Summary
- Total tasks: 23 (added TASK-009B per ADR-001)
- DONE: 10 | READY: 0 | IN_PROGRESS: 0 | IMPLEMENTED: 1 | IN_REVIEW: 0 | CHANGES_REQUESTED: 0 | BLOCKED: 0 | PLANNED: 12
- Current iteration: 7/40
- Active task: **TASK-010 IMPLEMENTED** (OKX project scaffold) on branch `feat/m3-okx` — awaiting review gate.

## Scoping Decisions (HITL — committed, not open questions)
The objective is fully prescriptive on scope/sequence/signing; these are the choices made decisively:
1. **In scope per exchange:** spot REST parity with Binance — market data, trading, account services; HMAC signing; error translation; time sync; symbol format; DI + factory-free composition; unit tests; integration tests (Category=Integration, stub-handler based).
2. **Out of scope (explicit):** WebSocket/streaming, derivatives/futures-specific endpoints, Coinbase/Kraken/other exchanges, CI pipeline creation, logging implementation.
3. **Milestone gating:** Bybit (M-BYBIT) ships fully — all tasks DONE — before OKX (M-OKX) starts. OKX ships before Bitget (M-BITGET). This is a hard gate, enforced by wave ordering.
4. **Abstraction timing:** Bybit reuses the existing shape with a thin `BybitSignatureService` + `BybitSigningHandler` and NO Core/Http change (the Http `requestFinalizerFactory` is already a generic `Func<IServiceProvider, DelegatingHandler>`). The credential/signing generalization is a dedicated task (TASK-009) executed at the start of M-OKX, Binance-compatible and non-breaking.
5. **Symbol formats:** Bybit uses delimiter-less upper (like Binance, e.g. `BTCUSDT`); OKX uses `-` delimiter upper (`BTC-USDT`); Bitget uses delimiter-less upper (`BTCUSDT`). Configured via existing `SymbolFormat`/`SymbolCasing` in Core — no Core change needed for symbol formats.

## Blast-Radius / Architect-Review Flags
Tasks touching shared Core/Http/DI projects are higher blast radius and REQUIRE architect-reviewer scrutiny:
- **TASK-009** (OKX-era credential/signing generalization) — touches Core abstractions + Http finalizer conventions. HIGHEST blast radius. Must not break Binance.
- **TASK-016** (Core `ExchangeId.Bitget` enum member) — touches Core enum. Low logic risk, public-API surface change (api-reviewer too).
- **TASK-001, TASK-008, TASK-015** (DI registration per exchange) — touch the DI project's shared `ServiceCollectionExtensions`/`CryptoExchangesOptions`. Additive but shared-file.

## Wave Groups

### Wave 1 — Bybit scaffolding (M-BYBIT)
- TASK-001 (Bybit project + options + ExchangeId reuse + DI seam stub)

### Wave 2 — Bybit auth + transport (M-BYBIT)
- TASK-002 (BybitSignatureService + signing request marker)
- TASK-004 (BybitSymbolFormat + value parsers + request validation)

### Wave 3 — Bybit signing handler + http client (M-BYBIT)
- TASK-003 (BybitSigningHandler — depends on TASK-002)
- TASK-005 (BybitHttpClient + IBybitHttpClient — depends on TASK-004)

### Wave 4 — Bybit services/mapping + error/time (M-BYBIT)
- TASK-006 (Bybit services + mapping profiles + composer + ExchangeClient)
- TASK-007 (BybitErrorTranslator + BybitTimeSync)

### Wave 5 — Bybit tests + DI wire-up (M-BYBIT, milestone close)
- TASK-008 (Bybit unit tests, integration tests, AddBybitExchange DI wiring) — closes M-BYBIT

### Wave 6 — OKX credential/signing generalization (M-OKX, starts only after M-BYBIT DONE)
- TASK-009 (passphrase-capable credential model + base64/hex output + header-signature convention; Binance-safe)
- **TASK-009B (NEW — per ADR-001): DI re-homing.** Move `AddBinanceExchange`/`AddBybitExchange` into their own assemblies; reduce `CryptoExchanges.Net.DependencyInjection` to a thin `AddCryptoExchanges` aggregator; implement OKX/Bitget DI in-assembly from the start. Also fold in the two pre-tracked follow-ups (harmonize Binance signing types to internal; back-fill `BinanceHttpClient` endpoint guard). Create a proper manifest when M-OKX starts.

### Wave 7 — OKX scaffolding (M-OKX)
- TASK-010 (OKX project + options w/ passphrase + ExchangeId.Okx reuse + DI seam stub)

### Wave 8 — OKX auth + transport (M-OKX)
- TASK-011 (OkxSignatureService base64 prehash + signing request marker — depends on TASK-009)
- TASK-013 (OkxSymbolFormat + value parsers + request validation)

### Wave 9 — OKX signing handler + http client (M-OKX)
- TASK-012 (OkxSigningHandler header-based — depends on TASK-011)
- TASK-014 (OkxHttpClient + IOkxHttpClient — depends on TASK-013)

### Wave 10 — OKX services/mapping/error/time + tests + DI (M-OKX, milestone close)
- TASK-015 (OKX services + mapping + composer + ExchangeClient + error translator + time sync + tests + AddOkxExchange DI) — closes M-OKX

### Wave 11 — Bitget Core enum (M-BITGET, starts only after M-OKX DONE)
- TASK-016 (add ExchangeId.Bitget member to Core)

### Wave 12 — Bitget scaffolding (M-BITGET)
- TASK-017 (Bitget project + options w/ passphrase + DI seam stub — depends on TASK-016)

### Wave 13 — Bitget auth + transport (M-BITGET)
- TASK-018 (BitgetSignatureService base64 prehash incl. query — reuses TASK-009 abstraction)
- TASK-020 (BitgetSymbolFormat + value parsers + request validation)

### Wave 14 — Bitget signing handler + http client (M-BITGET)
- TASK-019 (BitgetSigningHandler header-based — depends on TASK-018)
- TASK-021 (BitgetHttpClient + IBitgetHttpClient — depends on TASK-020)

### Wave 15 — Bitget services/mapping/error/time + tests + DI (M-BITGET, milestone close)
- TASK-022 (Bitget services + mapping + composer + ExchangeClient + error translator + time sync + tests + AddBitgetExchange DI) — closes M-BITGET

## Parallel Groups
(Equivalent to Wave Groups above; waves are the canonical parallel-execution grouping read by the orchestrator.)

### Group 1 (= Wave 1)
- [x] TASK-001: Bybit project scaffold + options + DI seam stub -> DONE

### Group 2 (= Wave 2)
- [x] TASK-002: BybitSignatureService + signing request marker -> DONE
- [x] TASK-004: BybitSymbolFormat + value parsers + request validation -> DONE

### Group 3 (= Wave 3)
- [x] TASK-003: BybitSigningHandler -> DONE
- [x] TASK-005: BybitHttpClient + interface -> DONE

### Group 4 (= Wave 4)
- [x] TASK-006: Bybit services + mapping + composer + ExchangeClient -> DONE
- [x] TASK-007: BybitErrorTranslator + BybitTimeSync -> DONE

### Group 5 (= Wave 5)
- [x] TASK-008: Bybit tests + AddBybitExchange DI (closes M-BYBIT) -> DONE

### Group 6 (= Wave 6)
- [ ] TASK-009: OKX-era credential/signing generalization (Core/Http) -> PLANNED

### Group 7 (= Wave 7)
- [ ] TASK-010: OKX project scaffold + passphrase options + DI seam stub -> IMPLEMENTED (awaiting review; commit af64279)

### Group 8 (= Wave 8)
- [ ] TASK-011: OkxSignatureService (base64 prehash) + signing marker -> PLANNED
- [ ] TASK-013: OkxSymbolFormat + value parsers + request validation -> PLANNED

### Group 9 (= Wave 9)
- [ ] TASK-012: OkxSigningHandler (header-based) -> PLANNED
- [ ] TASK-014: OkxHttpClient + interface -> PLANNED

### Group 10 (= Wave 10)
- [ ] TASK-015: OKX services + mapping + error + time + tests + AddOkxExchange DI (closes M-OKX) -> PLANNED

### Group 11 (= Wave 11)
- [ ] TASK-016: Core ExchangeId.Bitget enum member -> PLANNED

### Group 12 (= Wave 12)
- [ ] TASK-017: Bitget project scaffold + passphrase options + DI seam stub -> PLANNED

### Group 13 (= Wave 13)
- [ ] TASK-018: BitgetSignatureService (base64 prehash incl. query) + signing marker -> PLANNED
- [ ] TASK-020: BitgetSymbolFormat + value parsers + request validation -> PLANNED

### Group 14 (= Wave 14)
- [ ] TASK-019: BitgetSigningHandler (header-based) -> PLANNED
- [ ] TASK-021: BitgetHttpClient + interface -> PLANNED

### Group 15 (= Wave 15)
- [ ] TASK-022: Bitget services + mapping + error + time + tests + AddBitgetExchange DI (closes M-BITGET) -> PLANNED

## Tasks
### TASK-001: Bybit project scaffold + options + DI seam stub
- **Status**: DONE
- **Group**: 1
- **Depends on**: none
- **Manifest**: nazgul/tasks/TASK-001.md

### TASK-002: BybitSignatureService + signing request marker
- **Status**: DONE
- **Group**: 2
- **Depends on**: TASK-001
- **Manifest**: nazgul/tasks/TASK-002.md

### TASK-003: BybitSigningHandler
- **Status**: DONE
- **Group**: 3
- **Depends on**: TASK-002
- **Manifest**: nazgul/tasks/TASK-003.md

### TASK-004: BybitSymbolFormat + value parsers + request validation
- **Status**: DONE
- **Group**: 2
- **Depends on**: TASK-001
- **Manifest**: nazgul/tasks/TASK-004.md

### TASK-005: BybitHttpClient + interface
- **Status**: DONE
- **Group**: 3
- **Depends on**: TASK-004
- **Manifest**: nazgul/tasks/TASK-005.md

### TASK-006: Bybit services + mapping + composer + ExchangeClient
- **Status**: DONE
- **Group**: 4
- **Depends on**: TASK-005
- **Manifest**: nazgul/tasks/TASK-006.md

### TASK-007: BybitErrorTranslator + BybitTimeSync
- **Status**: DONE
- **Group**: 4
- **Depends on**: TASK-005
- **Manifest**: nazgul/tasks/TASK-007.md

### TASK-008: Bybit tests + AddBybitExchange DI (closes M-BYBIT)
- **Status**: DONE
- **Group**: 5
- **Depends on**: TASK-003, TASK-006, TASK-007
- **Manifest**: nazgul/tasks/TASK-008.md

### TASK-009: OKX-era credential/signing generalization (Core/Http)
- **Status**: PLANNED
- **Group**: 6
- **Depends on**: TASK-008
- **Manifest**: nazgul/tasks/TASK-009.md

### TASK-010: OKX project scaffold + passphrase options + DI seam stub
- **Status**: IMPLEMENTED
- **Group**: 7
- **Depends on**: TASK-009
- **Manifest**: nazgul/tasks/TASK-010.md

### TASK-011: OkxSignatureService (base64 prehash) + signing marker
- **Status**: PLANNED
- **Group**: 8
- **Depends on**: TASK-009, TASK-010
- **Manifest**: nazgul/tasks/TASK-011.md

### TASK-012: OkxSigningHandler (header-based)
- **Status**: PLANNED
- **Group**: 9
- **Depends on**: TASK-011
- **Manifest**: nazgul/tasks/TASK-012.md

### TASK-013: OkxSymbolFormat + value parsers + request validation
- **Status**: PLANNED
- **Group**: 8
- **Depends on**: TASK-010
- **Manifest**: nazgul/tasks/TASK-013.md

### TASK-014: OkxHttpClient + interface
- **Status**: PLANNED
- **Group**: 9
- **Depends on**: TASK-013
- **Manifest**: nazgul/tasks/TASK-014.md

### TASK-015: OKX services + mapping + error + time + tests + AddOkxExchange DI (closes M-OKX)
- **Status**: PLANNED
- **Group**: 10
- **Depends on**: TASK-012, TASK-014
- **Manifest**: nazgul/tasks/TASK-015.md

### TASK-016: Core ExchangeId.Bitget enum member
- **Status**: PLANNED
- **Group**: 11
- **Depends on**: TASK-015
- **Manifest**: nazgul/tasks/TASK-016.md

### TASK-017: Bitget project scaffold + passphrase options + DI seam stub
- **Status**: PLANNED
- **Group**: 12
- **Depends on**: TASK-016
- **Manifest**: nazgul/tasks/TASK-017.md

### TASK-018: BitgetSignatureService (base64 prehash incl. query) + signing marker
- **Status**: PLANNED
- **Group**: 13
- **Depends on**: TASK-017
- **Manifest**: nazgul/tasks/TASK-018.md

### TASK-019: BitgetSigningHandler (header-based)
- **Status**: PLANNED
- **Group**: 14
- **Depends on**: TASK-018
- **Manifest**: nazgul/tasks/TASK-019.md

### TASK-020: BitgetSymbolFormat + value parsers + request validation
- **Status**: PLANNED
- **Group**: 13
- **Depends on**: TASK-017
- **Manifest**: nazgul/tasks/TASK-020.md

### TASK-021: BitgetHttpClient + interface
- **Status**: PLANNED
- **Group**: 14
- **Depends on**: TASK-020
- **Manifest**: nazgul/tasks/TASK-021.md

### TASK-022: Bitget services + mapping + error + time + tests + AddBitgetExchange DI (closes M-BITGET)
- **Status**: PLANNED
- **Group**: 15
- **Depends on**: TASK-019, TASK-021
- **Manifest**: nazgul/tasks/TASK-022.md

## Completed
- TASK-001: Bybit project scaffold + options + DI seam stub (DONE) — review gate PASSED (architect 98, code 72, security 97, api 97); commit c782aed
- TASK-004: BybitSymbolFormat + value parsers + request validation (DONE) — review gate PASSED round 1 (architect APPROVE, code 82, security 91, api APPROVE); commit c1007cd
- TASK-002: BybitSignatureService + signing request marker (DONE) — review gate PASSED round 2 (code-reviewer REJECT@85 → APPROVE@98 after guard fix; architect/security/api APPROVE); commits 5654d93, e9fabc5
- TASK-003: BybitSigningHandler (DONE) — gate PASSED round 2 (api-reviewer REJECT@95 internal-visibility → APPROVE; architect 97, code, security APPROVE); commits 283bcf0, 60b55e3
- TASK-005: BybitHttpClient + interface (DONE) — gate PASSED round 2 (code-reviewer REJECT@97 endpoint-guard → APPROVE@100; architect 97, security 95, api 93); commits 2a598c8, fdbf2c5
- TASK-007: BybitErrorTranslator + BybitTimeSync (DONE) — gate PASSED round 2 (api-reviewer REJECT@95 offsetHolder length-guard → APPROVE@99; architect 93, code 88, security APPROVE); commits c6bfbb3, 456a208
- TASK-006: Bybit services + mapping + composer + ExchangeClient (DONE) — gate PASSED round 2 (api REJECT@95 + code REJECT@95×2 → APPROVE@98/APPROVE after limit-clamp + cancel-by-clientId id fix; architect 88, security 95); commits 057d6d2, 48fb17b
- TASK-008: Bybit tests + AddBybitExchange DI (DONE) — gate PASSED round 1 (all 4 APPROVE; code@96 confirmed regression tests valid); 77 unit + 5 integration tests; commit f60bd18. **CLOSES MILESTONE M-BYBIT.** (Merged to main via PR #11 → e7c0268.)
- TASK-009: OKX-era Core auth generalization (DONE) — gate PASSED round 1 (architect 97, security 99, api 96, code PASS); additive ExchangeCredentials + HmacSignature (hex|base64), non-breaking; +25 Core tests; commit 63b0006 (+polish).
- TASK-009B: per-exchange DI re-homing, ADR-001 (DONE) — gate PASSED round 1 (architect 92, security 100, api 95, code 97); AddXxxExchange moved into exchange assemblies, thin AddCryptoExchanges aggregator, ExchangeClientFactory→Http, Binance signing→internal + endpoint guards; 241+5 tests; commit 1a56835. BREAKING (pre-v1.0) namespace move.

## Tracked follow-ups (non-blocking, from gates)
- **Translator/TimeSync visibility**: now that per-exchange DI constructs them in-assembly, `BinanceErrorTranslator`/`BinanceTimeSync` + `BybitErrorTranslator`/`BybitTimeSync` can be `internal` (ADR-001 conv #2). → build OKX/Bitget translators+timesync INTERNAL from the start; small cleanup task for Binance/Bybit later.
- **Http.csproj**: add explicit `Microsoft.Extensions.DependencyInjection.Abstractions` PackageReference (currently transitive). Fold into a later cleanup.

## Blocked
<!-- None. -->

## Integration Strategy (DECIDED 2026-06-17 — see memory: pr-per-exchange-milestone)
**One PR per exchange/milestone, merge to main, then rebranch.** NOT a single end-of-objective PR (overrides config auto_pr_on_complete behavior).
- M-BYBIT (TASK-001–008) on `feat/m2-exchange-expansion` → on TASK-008 DONE, open PR → main → **confirm with user before the merge click** → merge.
- After merge: cut `feat/m3-okx` off updated main for M-OKX (TASK-009–015) → PR/merge → branch for M-BITGET (TASK-016–022).

## Recovery Pointer — ▶ ACTIVE (M-OKX)
- **Current Task:** TASK-010 IMPLEMENTED (OKX project scaffold + passphrase options + DI seam stub), awaiting review gate. Branch `feat/m3-okx` off main (PR #11 merged as e7c0268; main protected: required check "Build & Test" + strict up-to-date — keep this branch current with main).
- **Status:** TASK-009 + TASK-009B DONE. TASK-010 IMPLEMENTED (commit af64279; base 1e5300b). 10/23 DONE, 1 IMPLEMENTED. Created `src/CryptoExchanges.Net.Okx` (csproj mirroring post-ADR-001 Bybit: Core+Http refs only, DeltaMapper + Http/Options/DI.Abstractions, OKX test-project IVT + DynamicProxyGenAssembly2), GlobalUsings, OkxOptions (BaseUrl=https://www.okx.com, ApiKey, SecretKey, **Passphrase** as 3rd credential, TimeoutSeconds=30, ToCredentials()→Core ExchangeCredentials; NO ReceiveWindow — OKX signs with ISO-8601 timestamp). Added to sln nested under src. Build 0W/0E; all unit tests green; refs = Core+Http only. Diff at nazgul/reviews/TASK-010/diff.patch. **NEXT:** review gate for TASK-010, then Wave 8 = TASK-011 (OkxSignatureService base64 prehash + signing marker; consumes Core ExchangeCredentials + base64 HmacSignature) and TASK-013 (OkxSymbolFormat — `BTC-USDT` dash delimiter). OKX ships `AddOkxExchange` in-assembly (ADR-001) with INTERNAL error-translator + time-sync (tracked follow-up). Keep feat/m3-okx current with main. OKX PR changelog notes: AddXxxExchange namespace moved to exchange assemblies; Binance signing types now internal.
- **Historical note:** prior HOLD (await user merge of PR #11) is resolved — merged 2026-06-18.
- **PR-review fixes applied (pushed, not merged):** GitHub Copilot reviewer found a real bug in BybitErrorTranslator.Parse (retMsg GetString() w/o ValueKind guard → InvalidOperationException escapes catch). Fixed + 3 regression tests in commit 5643ff5; Copilot thread resolved. CodeRabbit was rate-limited (no review). Bybit unit tests now 80.
- **DI DESIGN — DECIDED (ADR-001, 2026-06-18):** adopt per-exchange DI (option b). Move `AddBinanceExchange`/`AddBybitExchange` into their own assemblies; `AddCryptoExchanges` becomes a thin aggregator. Apply at **M-OKX start, folded with TASK-009** (cheaper at 2 exchanges than 4); implement OKX/Bitget DI in-assembly from day one. Pre-v1.0 → breaking namespace move acceptable (optional `[Obsolete]` forwarders). See `nazgul/docs/ADR-001-per-exchange-di-and-conventions.md`.
- **Process hardening applied (PR #11 branch):** architect-reviewer gained invariant #10 (package coupling) + "question the reference pattern" mandate + milestone-boundary macro-architecture pass; code-reviewer gained JSON-ValueKind-guard + clamp-don't-throw rules; ADR-001 committed.

## Milestone-close checklist (run at every milestone boundary)
1. All milestone tasks DONE (gates passed).
2. **Macro-architecture pass** (architect-reviewer, ADR-001): review the milestone's AGGREGATE change for duplication/coupling/public-surface growth that will compound when the next milestone repeats the pattern — not just the last diff.
3. Open the milestone PR; confirm with user before merge.
4. After merge: rebranch off main for the next milestone.
- **Resume trigger:** user says the Bybit PR is merged (or asks to continue). THEN: `git checkout main && git pull`, cut `feat/m3-okx` off updated main, start Wave 6 = TASK-009 (OKX credential/signing generalization; HIGHEST blast radius — Core/Http; non-breaking for Binance + Bybit). Fold in the two tracked follow-ups: harmonize Binance signing types to internal + back-fill BinanceHttpClient endpoint guard.
- **Last Commit:** df3f411 (M-BYBIT complete); PR #11 open, unmerged. (Bybit unit + integration tests + AddBybitExchange DI wiring) — CLOSES MILESTONE M-BYBIT. Depends on TASK-003/006/007 (all DONE). TASK-008 notes: (1) create the Bybit test project(s) and ensure the csproj InternalsVisibleTo grants the UNIT-test project name (internal signing types, internal services, ApplyOffset); (2) cover signature hex vector + GET/POST sign-string, symbol round-trip, parser invariants + validation rejects, signing-handler header presence/re-sign-on-retry (stub handler, mirror BinancePipelineEndToEndTests), service DTO→model mapping via mocked IBybitHttpClient, error-code→exception mapping, time-offset sign/magnitude, AND the two round-1 bug fixes (limit clamp to 50, cancel-by-linkId id fallback); (3) wire AddBybitExchange in the DI project mirroring AddBinanceExchange. FOLLOW-UP for TASK-009: harmonize Binance signing types to internal + back-fill BinanceHttpClient endpoint guard.
- **Last Checkpoint:** nazgul/checkpoints/iteration-001.json
- **Last Commit:** c6bfbb3 feat(M2): TASK-007 BybitErrorTranslator + BybitTimeSync
