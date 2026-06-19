---
id: TASK-029
status: PLANNED
depends_on: [TASK-028]
commit:
claimed_at:
---
# TASK-029: Tool primitives — ToolResult envelope, input parsing, error mapping

## Metadata
- **ID**: TASK-029
- **Group**: 2
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-028
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Mcp/ToolResult.cs, src/CryptoExchanges.Net.Mcp/ToolInputs.cs, src/CryptoExchanges.Net.Mcp/ToolRunner.cs, tests/CryptoExchanges.Net.Mcp.Tests.Unit/ToolPrimitivesTests.cs]
- **Wave**: 2
- **Traces to**: Approved design §Tool surface (symbol normalization) + §Error handling; Approved plan **Task 2**
- **Created at**: 2026-06-19T04:00:00Z
- **Claimed at**:
- **Implemented at**:
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3

## Status

Blast radius: 3 new files in the MCP project + 1 test file. No existing source touched.
Pure helpers — fully unit-testable without the SDK runtime.

## Description

Add the tool primitives the tools build on. Implement faithfully per **plan Task 2** (it has
the exact code + the failing tests). Follow the TDD step order (tests first, then implement).

Produces (see plan Task 2 for signatures + bodies):
- `record ToolError(string Category, string Message)`.
- `record ToolResult<T>(bool Ok, T? Data, ToolError? Error)` with `Success`/`Failure` factories.
- `static class ToolInputs`: `TryParseExchange`, `ParseSymbol` (throws `FormatException`),
  `TryParseInterval` — maps agent strings to `ExchangeId` / `Symbol` / `KlineInterval`.
- `static class ToolRunner.RunAsync<T>(Func<Task<T>>)` — runs the action and maps any
  exception to a `ToolError` category. **Order matters**: specific arms
  (`AuthenticationException`, `RateLimitExceededException`, `ExchangeNotRegisteredException`,
  `ExchangeConnectivityException`, `FormatException`) MUST precede the `ExchangeApiException`
  arm because the rate-limit/auth types derive from it.

NOTE (from plan): confirm each exception's public constructor signature when implementing and
adjust the test's `CreateException` helper to match; the asserted **categories are the contract**.

## Acceptance Criteria
- [ ] `dotnet build CryptoExchanges.Net.sln -c Release` succeeds with **0 warnings / 0 errors** (TreatWarningsAsErrors).
- [ ] `ToolPrimitivesTests` pass (exchange parsing, symbol parse + FormatException paths, interval parsing, RunAsync success, and one mapping per error category); existing 455 tests stay green.
- [ ] Error categories match the contract: AuthRequired, RateLimited, ExchangeUnavailable, Connectivity, SymbolNotSupported, ExchangeError, Unknown.

## Pattern Reference
- Typed exception hierarchy being mapped: `src/CryptoExchanges.Net.Core/Exceptions/ExchangeExceptions.cs`
  (`ExchangeException` → `ExchangeApiException` → `RateLimitExceededException`,
  `AuthenticationException`, etc.; plus `ExchangeConnectivityException`, `ExchangeNotRegisteredException`).
- Symbol/Asset/enum types being parsed into: `src/CryptoExchanges.Net.Core/Models/Models.cs`
  (`Symbol`), `src/CryptoExchanges.Net.Core/Models/Asset.cs` (`Asset.TryOf`),
  `src/CryptoExchanges.Net.Core/Enums/Enums.cs` (`KlineInterval`, `ExchangeId`).
- Exact create code: **plan Task 2, Steps 1–7** — implement as written.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Mcp/ToolResult.cs
- src/CryptoExchanges.Net.Mcp/ToolInputs.cs
- src/CryptoExchanges.Net.Mcp/ToolRunner.cs
- tests/CryptoExchanges.Net.Mcp.Tests.Unit/ToolPrimitivesTests.cs

**Modifies**:
- none

## Traceability
- **PRD Acceptance Criteria**: n/a — Approved design §Tool surface (canonical-model symbol normalization), §Error handling
- **TRD Component**: n/a — `ToolResult` / `ToolInputs` / `ToolRunner`
- **ADR Reference**: ADR-001 (reuse Core models/exceptions; no new exchange logic)

## Implementation Log

### Attempt 1

## Review Results

### Attempt 1
