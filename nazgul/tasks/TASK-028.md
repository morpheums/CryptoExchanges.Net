---
id: TASK-028
status: DONE
depends_on: []
commit: be9b09f
claimed_at: 2026-06-19T05:00:00Z
---
# TASK-028: Project scaffold + host wiring + env→options binder

## Metadata
- **ID**: TASK-028
- **Group**: 1
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: none
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj, src/CryptoExchanges.Net.Mcp/Program.cs, src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs, tests/CryptoExchanges.Net.Mcp.Tests.Unit/CryptoExchanges.Net.Mcp.Tests.Unit.csproj, tests/CryptoExchanges.Net.Mcp.Tests.Unit/EnvCredentialBinderTests.cs, CryptoExchanges.Net.sln]
- **Wave**: 1
- **Traces to**: Approved design §Architecture + §Credentials & configuration; Approved plan **Task 1**
- **Created at**: 2026-06-19T04:00:00Z
- **Claimed at**: 2026-06-19T05:00:00Z
- **Base SHA**: 3c492b78ab3a4de3186c9ac41d28b184a1669fbd
- **Implemented at**: 2026-06-19T05:30:00Z
- **Completed at**: 2026-06-19T06:00:00Z
- **Blocked at**:
- **Retry count**: 0/3

## Status

Blast radius: NEW project + NEW test project; one solution-file edit. No changes to any
existing source project. Build hygiene is the risk (warnings-as-errors).

## Description

Scaffold the read-only MCP server host. Implement faithfully per **plan Task 1** — do NOT
re-design; the plan contains the exact `.csproj` contents, `Program.cs`, `EnvCredentialBinder`,
and the failing test. Follow the TDD step order (test first, then implement).

**FIRST**, before pinning packages: verify the latest `ModelContextProtocol` version via
`dotnet add src/CryptoExchanges.Net.Mcp package ModelContextProtocol --prerelease` and pin
whatever resolves (the plan's `0.4.0-preview.1` is a placeholder to reconcile). Likewise pin
the latest stable `Microsoft.Extensions.Hosting`.

Key points (see plan Task 1 for exact code): MCP project is `OutputType=Exe`,
`GenerateDocumentationFile=false`, `PackAsTool=true`, `ToolCommandName=crypto-mcp`; references
the 4 exchange libs + the DI extension; `Program.cs` routes logging to **stderr**
(stdout is the MCP channel), calls `AddCryptoExchanges` with `EnvCredentialBinder.Apply`, then
`AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`. `EnvCredentialBinder.Apply`
populates `CryptoExchangesOptions` from per-exchange env vars (10 keys across the 4 exchanges).
Add both projects to `CryptoExchanges.Net.sln`.

## Acceptance Criteria
- [ ] `dotnet build CryptoExchanges.Net.sln -c Release` succeeds with **0 warnings / 0 errors** under TreatWarningsAsErrors.
- [ ] `EnvCredentialBinderTests` pass (2/2) per plan Task 1 Step 2; the existing 455 tests stay green.
- [ ] Both new projects are added to `CryptoExchanges.Net.sln`; MCP `.csproj` sets `GenerateDocumentationFile=false` and the verified `ModelContextProtocol` version is pinned.

## Pattern Reference
- Env-var credential pattern: `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:93-94`
  and `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs:116-120`
  (`BINANCE_API_KEY`/`BINANCE_SECRET_KEY`).
- DI entry point being consumed: `src/CryptoExchanges.Net.DependencyInjection/ServiceCollectionExtensions.cs`
  (`AddCryptoExchanges`).
- Test project conventions (xunit.v3 + FluentAssertions + NSubstitute, `[source].Tests.Unit`):
  see `nazgul/context/test-strategy.md` and any existing `tests/*.Tests.Unit/*.csproj`.
- Exact create/modify code: **plan Task 1, Steps 1–8** (do not re-paraphrase — implement as written, reconciling the SDK version note).

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj
- src/CryptoExchanges.Net.Mcp/Program.cs
- src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs
- tests/CryptoExchanges.Net.Mcp.Tests.Unit/CryptoExchanges.Net.Mcp.Tests.Unit.csproj
- tests/CryptoExchanges.Net.Mcp.Tests.Unit/EnvCredentialBinderTests.cs

**Modifies**:
- CryptoExchanges.Net.sln (add both projects)

## Traceability
- **PRD Acceptance Criteria**: n/a (no PRD) — Approved design §Architecture, §Credentials & configuration
- **TRD Component**: n/a — `CryptoExchanges.Net.Mcp` host (Program.cs) + `EnvCredentialBinder`
- **ADR Reference**: ADR-001 (per-exchange DI; reuse `AddCryptoExchanges` — no new exchange logic)

## Implementation Log

### Attempt 1

Implemented per plan Task 1, all TDD steps followed.

**SDK version deviations from plan placeholders:**
- `ModelContextProtocol` resolved to **1.4.0** (plan placeholder: `0.4.0-preview.1`)
- `Microsoft.Extensions.Hosting` resolved to **10.0.9** (plan placeholder: `9.0.0`)
- Both SDK APIs match the plan's code exactly (attributes, method names, namespaces all confirmed via XML docs)

**CA1515 suppression:** `EnvCredentialBinder` is `public` so the test project (separate assembly) can reference it. CA1515 suppressed with `<NoWarn>CA1515</NoWarn>` in the MCP csproj.

**Results:**
- `dotnet build CryptoExchanges.Net.sln -c Release` → 0W/0E
- `EnvCredentialBinderTests` → 2/2 pass
- Full suite → 457 tests (455 existing + 2 new), all pass, 0 failures

## Commits

- `f56c61b` — feat(FEAT-002): scaffold read-only MCP server host + env credential binder (TASK-028)

## Review Results

### Attempt 1
