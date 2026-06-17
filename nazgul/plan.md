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
- Total tasks: 22
- DONE: 0 | READY: 0 | IN_PROGRESS: 0 | IN_REVIEW: 0 | CHANGES_REQUESTED: 0 | BLOCKED: 0 | PLANNED: 21 | IMPLEMENTED: 1
- Current iteration: 1/40
- Active task: TASK-001 (Wave 1)

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
- [x] TASK-001: Bybit project scaffold + options + DI seam stub -> IMPLEMENTED

### Group 2 (= Wave 2)
- [ ] TASK-002: BybitSignatureService + signing request marker -> PLANNED
- [ ] TASK-004: BybitSymbolFormat + value parsers + request validation -> PLANNED

### Group 3 (= Wave 3)
- [ ] TASK-003: BybitSigningHandler -> PLANNED
- [ ] TASK-005: BybitHttpClient + interface -> PLANNED

### Group 4 (= Wave 4)
- [ ] TASK-006: Bybit services + mapping + composer + ExchangeClient -> PLANNED
- [ ] TASK-007: BybitErrorTranslator + BybitTimeSync -> PLANNED

### Group 5 (= Wave 5)
- [ ] TASK-008: Bybit tests + AddBybitExchange DI (closes M-BYBIT) -> PLANNED

### Group 6 (= Wave 6)
- [ ] TASK-009: OKX-era credential/signing generalization (Core/Http) -> PLANNED

### Group 7 (= Wave 7)
- [ ] TASK-010: OKX project scaffold + passphrase options + DI seam stub -> PLANNED

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
- **Status**: IMPLEMENTED
- **Group**: 1
- **Depends on**: none
- **Manifest**: nazgul/tasks/TASK-001.md

### TASK-002: BybitSignatureService + signing request marker
- **Status**: PLANNED
- **Group**: 2
- **Depends on**: TASK-001
- **Manifest**: nazgul/tasks/TASK-002.md

### TASK-003: BybitSigningHandler
- **Status**: PLANNED
- **Group**: 3
- **Depends on**: TASK-002
- **Manifest**: nazgul/tasks/TASK-003.md

### TASK-004: BybitSymbolFormat + value parsers + request validation
- **Status**: PLANNED
- **Group**: 2
- **Depends on**: TASK-001
- **Manifest**: nazgul/tasks/TASK-004.md

### TASK-005: BybitHttpClient + interface
- **Status**: PLANNED
- **Group**: 3
- **Depends on**: TASK-004
- **Manifest**: nazgul/tasks/TASK-005.md

### TASK-006: Bybit services + mapping + composer + ExchangeClient
- **Status**: PLANNED
- **Group**: 4
- **Depends on**: TASK-005
- **Manifest**: nazgul/tasks/TASK-006.md

### TASK-007: BybitErrorTranslator + BybitTimeSync
- **Status**: PLANNED
- **Group**: 4
- **Depends on**: TASK-005
- **Manifest**: nazgul/tasks/TASK-007.md

### TASK-008: Bybit tests + AddBybitExchange DI (closes M-BYBIT)
- **Status**: PLANNED
- **Group**: 5
- **Depends on**: TASK-003, TASK-006, TASK-007
- **Manifest**: nazgul/tasks/TASK-008.md

### TASK-009: OKX-era credential/signing generalization (Core/Http)
- **Status**: PLANNED
- **Group**: 6
- **Depends on**: TASK-008
- **Manifest**: nazgul/tasks/TASK-009.md

### TASK-010: OKX project scaffold + passphrase options + DI seam stub
- **Status**: PLANNED
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
<!-- Tasks moved here after ALL reviewers approve and status is DONE. -->

## Blocked
<!-- None. -->

## Recovery Pointer
- **Current Task:** TASK-001 (IMPLEMENTED — awaiting review gate)
- **Last Action:** Bybit scaffold created (csproj, GlobalUsings, BybitOptions) + sln updated; build 0 warnings/0 errors
- **Next Action:** Review Gate for TASK-001; then Wave 2 (TASK-002, TASK-004)
- **Last Checkpoint:** nazgul/checkpoints/iteration-001.json
- **Last Commit:** (pending commit)
