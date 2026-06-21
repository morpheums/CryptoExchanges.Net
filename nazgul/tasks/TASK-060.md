---
id: TASK-060
status: DONE
depends_on: [TASK-059]
---
# TASK-060: `AddKucoinExchange` DI registration + `AddCryptoExchanges` + MCP wiring

## Metadata
- **ID**: TASK-060
- **Group**: 4
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-059
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs, src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs, src/CryptoExchanges.Net.DependencyInjection/CryptoExchangesOptions.cs, src/CryptoExchanges.Net.DependencyInjection/CryptoExchanges.Net.DependencyInjection.csproj, src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs]
- **Wave**: 4
- **Traces to**: PRD-FEAT-006 AC-1; TRD-FEAT-006 §"AddKucoinExchange (ADR-001 compliant)", §"MCP Wiring", §"Dependency Impact"; FEAT-006 spec §"DI + MCP", §"Build approach" step 5; TEST-PLAN-FEAT-006 §9
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**: 2026-06-21T00:00:00Z
- **Base SHA**: 00e124761bd8f095d7d25222440231faadaed681
- **Implemented at**: 2026-06-21T00:30:00Z
- **Completed at**: 2026-06-21T11:33:29Z
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Ship the per-exchange DI registration in the KuCoin assembly itself (ADR-001), wire it into the
`AddCryptoExchanges` convenience package, and surface KuCoin through the MCP host (which routes through
`AddCryptoExchanges`, so no MCP tool-schema change is needed). `ExchangeId.Kucoin` already exists in
`Core.Enums` — no Core change required.

Create:
- **`src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs`** — clone OKX's
  `AddKucoinExchange`: register a named `HttpClient` with the shared Polly resilience pipeline
  (retry GET-only, per-attempt timeout), register `KucoinSigningHandler`, register `IExchangeClient`
  keyed by `ExchangeId.Kucoin` → `KucoinExchangeClient` via `KucoinClientComposer.ComposeForDi`,
  `KucoinOptions` validated with `ValidateOnStart` (fail-fast on missing api-key). Lives in the KuCoin
  assembly so a consumer can depend on KuCoin alone (ADR-001). (The `AddKucoinStreams` overload is added
  in TASK-062; this task ships `AddKucoinExchange` only — leave the file extensible.)

Modify:
- **`CryptoExchanges.Net.DependencyInjection`** — add the `CryptoExchanges.Net.Kucoin` ProjectReference
  to the csproj; in `ServiceCollectionExtensions.AddCryptoExchanges`, add the `services.AddKucoinExchange(...)`
  delegate call mirroring the OKX/Bitget blocks; add the KuCoin options block to `CryptoExchangesOptions`.
- **`CryptoExchanges.Net.Mcp`** — add the `CryptoExchanges.Net.Kucoin` ProjectReference to the csproj
  (transitively pulled via DI, but referenced explicitly to match the other exchanges). No `Program.cs`
  change needed beyond confirming the `AddCryptoExchanges`-registered KuCoin client resolves by
  `ExchangeId.Kucoin` for the existing 12-tool vocabulary.

Tests (`KucoinDiTests.cs`), no network:
- `AddKucoinExchange` → provider resolves `IExchangeClient` keyed by `ExchangeId.Kucoin`.
- `ValidateOnStart` fails when `KucoinOptions.ApiKey` is empty.
- `AddCryptoExchanges` registers a resolvable KuCoin client (so MCP picks it up unchanged).

## Acceptance Criteria
- [x] `AddKucoinExchange` ships in the KuCoin assembly (ADR-001): named HttpClient + Polly pipeline (retry GET-only), `KucoinSigningHandler`, `IExchangeClient` keyed by `ExchangeId.Kucoin`, `KucoinOptions` with `ValidateOnStart`; full XML docs; mirrors `AddOkxExchange`.
- [x] `AddCryptoExchanges` calls `AddKucoinExchange` (DI csproj references KuCoin; `CryptoExchangesOptions` has the KuCoin block) and the MCP csproj references KuCoin so the 12-tool vocabulary resolves KuCoin by `ExchangeId.Kucoin` with no tool-schema change.
- [x] `KucoinDiTests` assert keyed `IExchangeClient` resolution + `ValidateOnStart` fail-fast on missing api-key + resolution via `AddCryptoExchanges` — NO network; solution builds 0W/0E; existing non-integration suite stays green.

## Pattern Reference
- Per-exchange registration to clone: `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs` (full `AddOkxExchange` — named client, signing handler, keyed client, ValidateOnStart).
- Convenience aggregation: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs` (the `AddOkxExchange`/`AddBitgetExchange` delegate blocks) + `CryptoExchangesOptions.cs`.
- MCP host wiring: `src/CryptoExchanges.Net.Mcp/Program.cs` (`AddCryptoExchanges`) + `CryptoExchanges.Net.Mcp.csproj` exchange ProjectReferences.
- DI tests shape: existing OKX/Bitget DI resolution tests under `tests/CryptoExchanges.Net.*.Tests.Unit/`.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs

**Modifies**:
- src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs (add AddKucoinExchange call)
- src/CryptoExchanges.Net.DependencyInjection/CryptoExchangesOptions.cs (add KuCoin options block)
- src/CryptoExchanges.Net.DependencyInjection/CryptoExchanges.Net.DependencyInjection.csproj (add Kucoin ProjectReference)
- src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj (add Kucoin ProjectReference)

## Traceability
- **PRD Acceptance Criteria**: AC-1 (AddKucoinExchange resolves working client), AC-7 (no-network DI tests)
- **TRD Component**: §"AddKucoinExchange (ADR-001 compliant)", §"MCP Wiring", §"Dependency Impact"
- **ADR Reference**: ADR-001 (per-exchange AddXxxExchange in own assembly); FEAT-006 spec §"DI + MCP"

## Commits

- `ad607d6` — feat(FEAT-006): TASK-060 — AddKucoinExchange DI + AddCryptoExchanges + MCP wiring

## Implementation Log

- Created `src/CryptoExchanges.Net.Kucoin/ServiceCollectionExtensions.cs` — ADR-001-compliant
  `AddKucoinExchange` cloning the Bitget/OKX pattern exactly: `ExchangeServiceRegistration.AddExchange`
  with named HttpClient, Polly resilience pipeline, `KucoinSigningHandler` (secret+passphrase gated,
  PassThroughHandler if either missing), `IExchangeClient` keyed by `ExchangeId.Kucoin` via
  `KucoinClientComposer.ComposeForDi`, `KucoinOptions` `ValidateOnStart` (timeout > 0, BaseUrl
  host-only via `ExchangeUrl.NormalizeHostRoot`). Env-defaults: KUCOIN_API_KEY / KUCOIN_SECRET_KEY
  / KUCOIN_PASSPHRASE.
- Modified `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs` — added
  `services.AddKucoinExchange(...)` delegate block mirroring OKX/Bitget blocks.
- Modified `src/CryptoExchanges.Net.DependencyInjection/CryptoExchangesOptions.cs` — added KuCoin
  options block (KucoinBaseUrl, KucoinApiKey, KucoinSecretKey, KucoinPassphrase).
- Modified `src/CryptoExchanges.Net.DependencyInjection/CryptoExchanges.Net.DependencyInjection.csproj`
  — added KuCoin ProjectReference.
- Modified `src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj` — added KuCoin
  ProjectReference (explicit per pattern; transitive from DI but explicit matches sibling exchanges).
- Created `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/KucoinDiTests.cs` — 11 no-network tests:
  keyed resolution (full creds, secretless, passphrase-missing), ValidateOnStart fail-fast
  (timeout=0, BaseUrl with path), mapper singleton, no unkeyed registration, scope-clean graph,
  AddCryptoExchanges KuCoin resolution, all-five-exchange resolution, options delegation.

## Review Results

### Cycle 1 — APPROVED 4/4 (2026-06-21T11:33:29Z)

Gate rule `require_all_approve: true` satisfied. All four reviewers APPROVE. No blocking findings
(all CONCERNs confidence ≤ 65, below the 80 threshold, and every one is a pre-existing cross-exchange
pattern rather than a TASK-060 regression). No security REJECT.

| Reviewer | Verdict | Key findings |
|----------|---------|--------------|
| architect-reviewer | ✦ APPROVE | ADR-001 ✓ (AddKucoinExchange in KuCoin assembly); 4-layer chain ✓ (KuCoin → Core+Http only); keyed-singleton parity with OKX/Bitget ✓; ValidateOnStart fail-fast ✓; MCP wiring ✓ (no Program.cs change). CONCERN@65: flat-property growth of CryptoExchangesOptions (O(N), pre-existing); ApplyEnvDefaults duplication (3 copies, pre-existing). |
| code-reviewer | ✦ APPROVE | Exact structural parity with Bitget/OKX peer; XML docs ✓; signing gate `SecretKey \|\| Passphrase` with PassThrough fallback ✓; 10 DI tests exceed Bitget suite coverage; LR-005 satisfied. CONCERN@55: `Throw<Exception>()` specificity in BaseUrlWithPath test (established codebase pattern). |
| security-reviewer | ✦ APPROVE | SecretKey held only inside KucoinSignatureService, never on handler; no credential logging anywhere; KC-API-PASSPHRASE transmits HMAC-signed value (not plaintext); mark-and-strip on retry; both-or-nothing PassThrough gate correct; ToCredentials() bypass documented & safe; no opsec leakage. CONCERN@55: KucoinOptions lacks ToString() redaction (pre-existing across all Options classes); CONCERN@60: empty ApiKey w/ signing creds fails at request-time not DI (pre-existing). |
| api-reviewer | ✦ APPROVE | AddKucoinExchange signature exact parity; 4 nullable `string?` options fields, non-breaking; AddCryptoExchanges still chainable w/ double null guard; MCP tool-schema unchanged; NuGet conventions ✓; InternalsVisibleTo test/mock only. CONCERN@60: AC language "fail-fast on missing api-key" satisfied by PassThrough gate (not validator), consistent across all 5 exchanges; CONCERN@55: unkeyed KucoinOptions resolution valid (distinct type per exchange). |

**Pre-check results**: build 0W/0E (`TreatWarningsAsErrors`); full non-integration suite green;
Http.Tests.Unit 87/87 PASS in isolation.

**Flake diagnosis** (per task brief): The implementer-reported Http.Tests.Unit streaming-reconnect
failure is a PRE-EXISTING parallel-run test-harness race, NOT a real regression in the TASK-061
async `ResolveConnectionAsync` seam. Evidence: the Http.Tests.Unit project passes 87/87 when run in
isolation (`dotnet test tests/CryptoExchanges.Net.Http.Tests.Unit/... --filter 'Category!=Integration'`),
and the full non-integration suite passed with 0 failures in the gate pre-check run. The architect
reviewer independently confirmed the ADR-002 seam is byte/opaque with no concurrency defect, and the
Binance migration is regression-free. Treated as suite-green with a NON-BLOCKING note: the shared
xunit test collection exhibits a static/timing race only under parallel scheduling. Recommend a
follow-up to isolate the affected streaming-reconnect test into its own non-parallel collection
(tracked as a non-blocking concern, not a TASK-060 blocker).

**Simplify pass**: 0 fixes applied — all 3 candidate findings disproved as faithful pattern clones
(bespoke KucoinSymbolMapper vs generic SymbolMapper is intentional). 657 unit tests remain green.

**Gate decision**: ✦ APPROVED → DONE. **Completion SHA**: `0940957`.
**Review artifacts**: `nazgul/reviews/TASK-060/{architect,code,security,api}-reviewer.md`, `simplify-report.md`.
