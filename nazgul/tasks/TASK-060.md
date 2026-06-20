---
id: TASK-060
status: PLANNED
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
- **Claimed at**:
- **Base SHA**:
- **Implemented at**:
- **Completed at**:
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
- [ ] `AddKucoinExchange` ships in the KuCoin assembly (ADR-001): named HttpClient + Polly pipeline (retry GET-only), `KucoinSigningHandler`, `IExchangeClient` keyed by `ExchangeId.Kucoin`, `KucoinOptions` with `ValidateOnStart`; full XML docs; mirrors `AddOkxExchange`.
- [ ] `AddCryptoExchanges` calls `AddKucoinExchange` (DI csproj references KuCoin; `CryptoExchangesOptions` has the KuCoin block) and the MCP csproj references KuCoin so the 12-tool vocabulary resolves KuCoin by `ExchangeId.Kucoin` with no tool-schema change.
- [ ] `KucoinDiTests` assert keyed `IExchangeClient` resolution + `ValidateOnStart` fail-fast on missing api-key + resolution via `AddCryptoExchanges` — NO network; solution builds 0W/0E; existing non-integration suite stays green.

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

<!-- implementer fills SHAs -->

## Implementation Log

## Review Results
