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
- DONE: 16 | READY: 0 | IN_PROGRESS: 0 | IMPLEMENTED: 0 | IN_REVIEW: 0 | CHANGES_REQUESTED: 0 | BLOCKED: 0 | PLANNED: 7
- Current iteration: 9/40
- Active task: none active — TASK-015 DONE, M-OKX CLOSED. 16/23 DONE. NEXT: open M-OKX PR (feat/m3-okx -> main; confirm with user before merge), then rebranch for M-BITGET (TASK-016+).

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
- [x] TASK-009: OKX-era credential/signing generalization (Core/Http) -> DONE
- [x] TASK-009B: per-exchange DI re-homing (ADR-001) -> DONE

### Group 7 (= Wave 7)
- [x] TASK-010: OKX project scaffold + passphrase options + DI seam stub -> DONE

### Group 8 (= Wave 8)
- [x] TASK-011: OkxSignatureService (base64 prehash) + signing marker -> DONE
- [x] TASK-013: OkxSymbolFormat + value parsers + request validation -> DONE

### Group 9 (= Wave 9)
- [x] TASK-012: OkxSigningHandler (header-based) -> DONE
- [x] TASK-014: OkxHttpClient + interface -> DONE

### Group 10 (= Wave 10)
- [x] TASK-015: OKX services + mapping + error + time + tests + AddOkxExchange DI (closes M-OKX) -> DONE (gate PASSED; CLOSES M-OKX)

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
- **Status**: IMPLEMENTED
- **Group**: 8
- **Depends on**: TASK-009, TASK-010
- **Manifest**: nazgul/tasks/TASK-011.md

### TASK-012: OkxSigningHandler (header-based)
- **Status**: PLANNED
- **Group**: 9
- **Depends on**: TASK-011
- **Manifest**: nazgul/tasks/TASK-012.md

### TASK-013: OkxSymbolFormat + value parsers + request validation
- **Status**: IMPLEMENTED
- **Group**: 8
- **Depends on**: TASK-010
- **Base SHA**: c9243437133b98700aa5ffd1cd0f55615fd3549b
- **Manifest**: nazgul/tasks/TASK-013.md

### TASK-014: OkxHttpClient + interface
- **Status**: IMPLEMENTED
- **Group**: 9
- **Depends on**: TASK-013
- **Base SHA**: 6522afa2dd0db87d19240cfdb952a9c331fd3970
- **Manifest**: nazgul/tasks/TASK-014.md

### TASK-015: OKX services + mapping + error + time + tests + AddOkxExchange DI (closes M-OKX)
- **Status**: DONE
- **Group**: 10
- **Depends on**: TASK-012, TASK-014
- **Base SHA**: f03c2092318332a0dc16215fdae7e7b9ac25cc43
- **claimed_at**: 2026-06-18
- **Commit**: 5fb5661 (+ b78be03 simplify, fb92660 gate-fix); gate PASSED, CLOSES M-OKX
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
- TASK-015: OKX services + mapping + composer + ExchangeClient + error translator + time sync + tests + AddOkxExchange DI (DONE) — gate PASSED round 1 via fix-first auto-remediation (architect 92, security 95, api APPROVE, code REJECT@95 -> fixed). Simplify pass consolidated ParseMs/SpotInstType (b78be03). code-reviewer B1 (HIGH/95 unguarded long.Parse on candlestick ts @ OkxMarketDataService.cs:214) + B2 (no GetCandlesticks test) auto-fixed in fb92660 (ParseMs + 4 regression tests; OKX unit 91->95). Build 0W/0E; non-integration 336 pass; integration 11 pass; no Binance/Bybit regression. Public surface = OkxExchangeClient + OkxOptions + AddOkxExchange host only. Commits 5fb5661, b78be03, fb92660. **CLOSES MILESTONE M-OKX.**

## Tracked follow-ups (non-blocking, from gates)
- **Translator/TimeSync visibility**: now that per-exchange DI constructs them in-assembly, `BinanceErrorTranslator`/`BinanceTimeSync` + `BybitErrorTranslator`/`BybitTimeSync` can be `internal` (ADR-001 conv #2). → build OKX/Bitget translators+timesync INTERNAL from the start; small cleanup task for Binance/Bybit later.
- **Http.csproj**: add explicit `Microsoft.Extensions.DependencyInjection.Abstractions` PackageReference (currently transitive). Fold into a later cleanup.

## Blocked
<!-- None. -->

## Integration Strategy (DECIDED 2026-06-17 — see memory: pr-per-exchange-milestone)
**One PR per exchange/milestone, merge to main, then rebranch.** NOT a single end-of-objective PR (overrides config auto_pr_on_complete behavior).
- M-BYBIT (TASK-001–008) on `feat/m2-exchange-expansion` → on TASK-008 DONE, open PR → main → **confirm with user before the merge click** → merge.
- After merge: cut `feat/m3-okx` off updated main for M-OKX (TASK-009–015) → PR/merge → branch for M-BITGET (TASK-016–022).

## Recovery Pointer — ⏸ TASK-REF-001 DONE, shipped to PR #13 (awaiting user merge → then M-BITGET)
- **Current Task:** none active. TASK-REF-001 DONE (gate PASSED, all 4 APPROVE, behavior byte-identical). 17/24 tasks done (22 plan + TASK-009B + TASK-REF-001).
- **PR #13 OPEN:** https://github.com/morpheums/CryptoExchanges.Net/pull/13 (refactor/di-timesync-dry → main). All green (333 non-integration + 11 integration, 0W/0E). PR body documents the breaking change (public BinanceTimeSync/BybitTimeSync deleted → use Core ExchangeTimeSync). **Awaiting user review/merge** (per-exchange/per-concern strategy — user merges).
- **Resume trigger:** user says PR #13 is merged (or "continue"). THEN: `git checkout main && git pull`; rebranch `feat/m4-bitget` off updated main; start **M-BITGET**.
- **M-BITGET plan (Waves 11–15):** TASK-016 (add `ExchangeId.Bitget` to Core/Enums — NOT yet present; touches Core enum → architect+api) → TASK-017 (scaffold + passphrase options, mirror OKX) → TASK-018/020 (BitgetSignatureService base64 prehash INCL. query + BitgetSymbolFormat delimiter-less `BTCUSDT`) → TASK-019/021 (signing handler header-based `ACCESS-*` + http client) → TASK-022 (services+mapping+composer+client+error+time+tests+AddBitgetExchange — closes M-BITGET). Bitget reuses: Core ExchangeCredentials/HmacSignature(base64), Core ExchangeTimeSync, the shared `ExchangeServiceRegistration.AddExchange` helper, internal error-translator+timesync from the start. Bitget signing delta vs OKX: prehash `timestamp+UPPER(method)+requestPath+'?'+queryString+body`; header `ACCESS-PASSPHRASE`/`ACCESS-KEY`/`ACCESS-SIGN`/`ACCESS-TIMESTAMP`.
- **Carry-forwards into TASK-016+ (from REF-001 gate, non-blocking):** add ArgumentNullException guards to the helper's delegate params; re-evaluate gateFactory-hardcoded + 13-param helper signature when Bitget is wired (if Bitget fits cleanly, close).

## Recovery Pointer (PRIOR) — ⏸ MILESTONE BOUNDARY (M-OKX shipped to PR #12 — awaiting user merge + TASK-REF-001 decision)
- **Current Task:** none active. TASK-015 DONE; **MILESTONE M-OKX CLOSED** (TASK-009..015 all DONE). 16/23 done. Branch `feat/m3-okx` (current with main: dependabot CI bumps merged).
- **PR #12 OPEN:** https://github.com/morpheums/CryptoExchanges.Net/pull/12 (feat/m3-okx → main). Pushed, all green (336 non-integration + 11 integration, 0W/0E). **Awaiting user review/merge** (per per-exchange strategy — user merges; main is protected: "Build & Test" check + strict up-to-date, enforce_admins on → branch must stay current with main).
- **DECIDED (2026-06-18):** (1) user reviews/merges PR #12 themselves — I HOLD, do not start the next task until OKX is in main. (2) **TASK-REF-001 = YES, before Bitget** (manifest written, nazgul/tasks/TASK-REF-001.md, PLANNED).
- **DECIDED SEQUENCE (blocked on #12 merge):**
  1. ⏸ WAIT for user to merge PR #12 (OKX) → main.
  2. After merge: `git checkout main && git pull`; cut a fresh branch (e.g. `refactor/di-timesync-dry`) off updated main.
  3. **TASK-REF-001** (its OWN PR): move TimeSync→Core; shared keyed-singleton DI helper; leave composer dup. Must be behavior-identical; all 347 tests stay green. PR → user merge.
  4. After that merges: rebranch (`feat/m4-bitget`) off main → **M-BITGET** = TASK-016 (add ExchangeId.Bitget to Core/Enums — NOT yet present) → 017 scaffold → 018/020 auth+symbol → 019/021 handler+client → 022 closer. Bitget reuses the OKX-era abstraction + the new shared DI helper (base64 prehash incl. query, ACCESS-PASSPHRASE header, delimiter-less BTCUSDT symbol).
- **Resume trigger:** user says PR #12 is merged (or "continue"). Then proceed to step 2/3 above.

- **What just happened:** Review gate for TASK-015 was redone (prior run hit a session limit before writing verdicts). Simplify pass ran (2 safe consolidations, b78be03). Pre-checks green. Board: architect APPROVED(92), security APPROVED(95), api APPROVED; code-reviewer CHANGES_REQUESTED(95) with 2 mechanical AUTO-FIX items — B1 (HIGH/95 unguarded `long.Parse(arr[0])` candlestick ts @ OkxMarketDataService.cs:214) and B2 (no GetCandlesticks test). Both fixed via fix-first auto-remediation (commit fb92660: `OkxValueParsers.ParseMs` + 4 GetCandlesticks regression tests, OKX unit 91->95). Build 0W/0E; non-integration 336 pass; integration 11 pass; no Binance/Bybit regression. Aggregate verdict PASSED.

- **NEXT ACTION (per per-exchange-milestone strategy — REQUIRES USER CONFIRMATION before the merge):**
  1. Run remaining milestone-close checklist items: post-loop agents are NOT run per-milestone here; the macro-architecture pass is already done (see architect note below + recorded as a tracked follow-up).
  2. Push `feat/m3-okx` and open the M-OKX PR -> `main`. Title: "OKX exchange integration (M-OKX)". Body changelog: AddOkxExchange in OKX assembly (ADR-001); OkxErrorTranslator + OkxTimeSync internal; new typed object-body OkxHttpClient.PostAsync overload (cancel-batch-orders); CryptoExchangesOptions OKX additions incl. passphrase; simplify consolidations.
  3. **Confirm with the user before clicking merge.** After merge: `git checkout main && git pull`, cut a new branch off updated main for M-BITGET, then start Wave 11 = TASK-016 (Core ExchangeId.Bitget enum).

- **NEW tracked follow-up before M-BITGET (architect macro note, MEDIUM/75, non-blocking):** Three exchanges now share ~90%-identical DI/Composer/TimeSync structure. Before Bitget repeats the pattern a 4th time, create **TASK-REF-001: Extract per-exchange DI helper** — priority order: (a) extract `TimeSync` (`ComputeOffset`/`ApplyOffset`) into Core (zero exchange-specific logic); (b) shared keyed-singleton DI registration helper; (c) accept ClientComposer duplication (only `BuildResilientHttpClient` diverges). Cheaper now than after a 4th exchange.

- **Non-blocking review CONCERNs (carry, do NOT gate Bitget on these):** api MEDIUM/90 `OkxOptions.ToCredentials()` throws on default-empty Passphrase (never called in signing path — guard empty->null or make internal); code MEDIUM/60 `TryMapTicker` narrow `FormatException` catch (document or widen); cosmetic public-in-internal modifiers in OkxRequestValidation; stale comment in OkxHttpClient.PostJsonAsync.

- **Historical note:** PR #11 (M-BYBIT) merged 2026-06-18. ADR-001 (per-exchange DI) adopted and applied across Binance/Bybit/OKX.
- **Last Commit:** fb92660 feat(M2): TASK-015 review-gate fixes — guard candlestick ts parse + GetCandlesticks tests
- **Last Checkpoint:** nazgul/checkpoints/iteration-001.json
- **Last Commit:** c6bfbb3 feat(M2): TASK-007 BybitErrorTranslator + BybitTimeSync
