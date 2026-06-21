---
id: TASK-068
status: IMPLEMENTED
depends_on: [TASK-066]
---
# TASK-068: Repoint consumers — MCP (src + tests), samples/BasicUsage, and `.sln`

## Metadata
- **ID**: TASK-068
- **Group**: 4
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-066
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Mcp/Program.cs, src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs, src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj, tests/CryptoExchanges.Net.Mcp.Tests.Unit/EnvCredentialBinderTests.cs, tests/CryptoExchanges.Net.Mcp.Tests.Unit/CryptoExchanges.Net.Mcp.Tests.Unit.csproj, samples/BasicUsage/BasicUsage.csproj]
- **Wave**: 4
- **Traces to**: PRD-FEAT-007 AC-5, AC-6; TRD-FEAT-007 §"Step 4 — Repoint MCP and samples"; FEAT-007 spec §"Scope — In" #4
- **Created at**: 2026-06-21T18:00:00Z
- **Claimed at**: 2026-06-21T00:00:00Z
- **Base SHA**: 41320d1b0cd2002e842f572950ffff912015ae24
- **Implemented at**: 2026-06-21T00:10:00Z
- **Completed at**:
- **Blocked at**:
- **Retry count**: 0/3
- **Test failures**: 0

## Description

Repoint the legitimate aggregator consumers — the MCP server (which wants all exchanges), its test
project, and the BasicUsage sample — at the renamed `CryptoExchanges.Net` package. The `.sln` entries
for the renamed src + test projects are already fixed in TASK-065/066; this task touches the sln only
if a verification reveals a stale path (otherwise the sln is untouched here — the consumer csprojs
keep their own GUIDs and entries).

Steps:
1. `src/CryptoExchanges.Net.Mcp/Program.cs` (line 1) — `using CryptoExchanges.Net.DependencyInjection;`
   → `using CryptoExchanges.Net;`.
2. `src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs` (line 1) — same `using` swap. Any reference to
   `CryptoExchangesOptions` resolves unchanged (same type, new namespace).
3. `src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj` (line 19) — repoint the
   ProjectReference from `../CryptoExchanges.Net.DependencyInjection/CryptoExchanges.Net.DependencyInjection.csproj`
   → `../CryptoExchanges.Net/CryptoExchanges.Net.csproj`.
4. `tests/CryptoExchanges.Net.Mcp.Tests.Unit/EnvCredentialBinderTests.cs` (line 3) — `using` swap.
5. `tests/CryptoExchanges.Net.Mcp.Tests.Unit/CryptoExchanges.Net.Mcp.Tests.Unit.csproj` (line 11) —
   repoint ProjectReference `../../src/CryptoExchanges.Net.DependencyInjection/CryptoExchanges.Net.DependencyInjection.csproj`
   → `../../src/CryptoExchanges.Net/CryptoExchanges.Net.csproj`.
6. `samples/BasicUsage/BasicUsage.csproj` (line 11) — repoint ProjectReference
   `..\..\src\CryptoExchanges.Net.DependencyInjection\CryptoExchanges.Net.DependencyInjection.csproj`
   → `..\..\src\CryptoExchanges.Net\CryptoExchanges.Net.csproj`. `samples/BasicUsage/Program.cs`
   needs NO change — it uses `AddBinanceExchange` directly and imports `CryptoExchanges.Net.Binance`,
   not the aggregator namespace (verified: no `using CryptoExchanges.Net.DependencyInjection;` and no
   `AddCryptoExchanges` call present).

Out of scope here: the scratch project `LoggingTest/` is NOT in the solution and does not reference
the aggregator — leave it untouched.

After edits build the MCP + samples + MCP tests: `dotnet build CryptoExchanges.Net.sln` must compile
the MCP and sample projects (full 0W/0E + suite gate is TASK-070); run
`dotnet test tests/CryptoExchanges.Net.Mcp.Tests.Unit/ --filter 'Category!=Integration'` green —
`CryptoExchangesOptions` still resolves from `CryptoExchanges.Net`; all MCP assertions unchanged.

## Acceptance Criteria
- [ ] `Program.cs`, `EnvCredentialBinder.cs`, and `EnvCredentialBinderTests.cs` use `using CryptoExchanges.Net;` (no `…DependencyInjection` using); the three csprojs (MCP, MCP.Tests.Unit, BasicUsage) reference `…\CryptoExchanges.Net\CryptoExchanges.Net.csproj`; no consumer file references a `DependencyInjection` path.
- [ ] `samples/BasicUsage/Program.cs` is unchanged (no aggregator using existed); the sample compiles against the renamed package.
- [ ] `dotnet test tests/CryptoExchanges.Net.Mcp.Tests.Unit/ --filter 'Category!=Integration'` → green (MCP still resolves all exchanges; `CryptoExchangesOptions` resolves from `CryptoExchanges.Net`); MCP + sample build 0W/0E.

## Pattern Reference
- MCP usings to swap: `src/CryptoExchanges.Net.Mcp/Program.cs:1`, `src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs:1`.
- MCP csproj ProjectReference: `src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj:19`.
- MCP test using + csproj: `tests/CryptoExchanges.Net.Mcp.Tests.Unit/EnvCredentialBinderTests.cs:3`, `tests/CryptoExchanges.Net.Mcp.Tests.Unit/CryptoExchanges.Net.Mcp.Tests.Unit.csproj:11`.
- Sample csproj ProjectReference: `samples/BasicUsage/BasicUsage.csproj:11`.

## File Scope

**Creates**:
- (none)

**Modifies**:
- src/CryptoExchanges.Net.Mcp/Program.cs
- src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs
- src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj
- tests/CryptoExchanges.Net.Mcp.Tests.Unit/EnvCredentialBinderTests.cs
- tests/CryptoExchanges.Net.Mcp.Tests.Unit/CryptoExchanges.Net.Mcp.Tests.Unit.csproj
- samples/BasicUsage/BasicUsage.csproj

## Traceability
- **PRD Acceptance Criteria**: AC-5 (MCP + samples reference `CryptoExchanges.Net`; MCP resolves all exchanges), AC-6 (0W/0E)
- **TRD Component**: §"Step 4 — Repoint MCP and samples"
- **ADR Reference**: ADR-003 (consumers repoint at the renamed package)

## Commits

## Implementation Log

- Swapped `using CryptoExchanges.Net.DependencyInjection;` → `using CryptoExchanges.Net;` in 3 `.cs` files: `Program.cs`, `EnvCredentialBinder.cs`, `EnvCredentialBinderTests.cs`.
- Repointed `<ProjectReference>` from `…DependencyInjection.csproj` → `…CryptoExchanges.Net.csproj` in 3 `.csproj` files: `CryptoExchanges.Net.Mcp.csproj`, `CryptoExchanges.Net.Mcp.Tests.Unit.csproj`, `BasicUsage.csproj`.
- `.sln` verified clean — no stale `DependencyInjection` entries remain.
- `samples/BasicUsage/Program.cs` confirmed unchanged (uses `AddBinanceExchange`, no aggregator using).
- Build: `dotnet build CryptoExchanges.Net.sln` → 0W/0E.
- Tests: `dotnet test --filter 'Category!=Integration'` → all green (778 tests passed, 0 failed).

## Review Results
